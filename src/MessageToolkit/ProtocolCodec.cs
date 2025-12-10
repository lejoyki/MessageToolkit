using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using MessageToolkit.Abstractions;
using MessageToolkit.Internal;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 协议编解码器实现
/// </summary>
public sealed class ProtocolCodec<TProtocol> : IProtocolCodec<TProtocol>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }

    public ProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public byte[] Encode(TProtocol protocol)
    {
        var buffer = new byte[Schema.TotalSize];

        foreach (var fieldInfo in Schema.Fields.Values.OrderBy(f => f.ByteAddress))
        {
            var value = GetFieldValue(protocol, fieldInfo.Name);
            var bytes = EncodeValueInternal(value, fieldInfo.FieldType);
            var offset = fieldInfo.ByteAddress - Schema.StartAddress;
            bytes.CopyTo(buffer.AsSpan(offset));
        }

        return buffer;
    }

    public TProtocol Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < Schema.TotalSize)
        {
            throw new ArgumentException($"数据长度不足: 需要 {Schema.TotalSize} 字节, 实际 {data.Length} 字节", nameof(data));
        }

        var result = new TProtocol();
        object boxed = result;

        foreach (var fieldInfo in Schema.Fields.Values.OrderBy(f => f.ByteAddress))
        {
            var offset = fieldInfo.ByteAddress - Schema.StartAddress;
            var valueBytes = data.Slice(offset, fieldInfo.Size);
            var value = DecodeValueInternal(valueBytes, fieldInfo.FieldType);
            SetFieldValue(boxed, fieldInfo.Name, value);
        }

        return (TProtocol)boxed;
    }

    public byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged
    {
        return EncodeValueInternal(value, typeof(TValue));
    }

    public TValue DecodeValue<TValue>(ReadOnlySpan<byte> data) where TValue : unmanaged
    {
        var expectedSize = GetValueSize(typeof(TValue));
        if (data.Length < expectedSize)
        {
            throw new ArgumentException($"数据长度不足: 需要 {expectedSize} 字节, 实际 {data.Length} 字节", nameof(data));
        }

        var value = DecodeValueInternal(data[..expectedSize], typeof(TValue));
        return (TValue)value;
    }

    public byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector)
    {
        var memberName = GetMemberName(fieldSelector);
        var value = GetFieldValue(protocol, memberName);
        var fieldInfo = Schema.GetFieldInfo(memberName);
        return EncodeValueInternal(value, fieldInfo.FieldType);
    }

    private byte[] EncodeValueInternal(object? value, Type fieldType)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return fieldType switch
        {
            var t when t == typeof(short) => ByteConverter.GetBytes((short)value, Schema.Endianness),
            var t when t == typeof(ushort) => ByteConverter.GetBytes((ushort)value, Schema.Endianness),
            var t when t == typeof(int) => ByteConverter.GetBytes((int)value, Schema.Endianness),
            var t when t == typeof(uint) => ByteConverter.GetBytes((uint)value, Schema.Endianness),
            var t when t == typeof(float) => ByteConverter.GetBytes((float)value, Schema.Endianness),
            var t when t == typeof(bool) => Schema.BooleanType switch
            {
                BooleanRepresentation.Int16 => ByteConverter.GetBytes((short)((bool)value ? 1 : 0), Schema.Endianness),
                BooleanRepresentation.Int32 => ByteConverter.GetBytes((int)((bool)value ? 1 : 0), Schema.Endianness),
                _ => throw new NotSupportedException("不支持的布尔表示方式")
            },
            _ when fieldType.IsEnum => EncodeValueInternal(Convert.ChangeType(value, Enum.GetUnderlyingType(fieldType))!, Enum.GetUnderlyingType(fieldType)),
            _ => throw new NotSupportedException($"不支持的类型 {fieldType.Name}")
        };
    }

    private object DecodeValueInternal(ReadOnlySpan<byte> data, Type fieldType)
    {
        return fieldType switch
        {
            var t when t == typeof(short) => ByteConverter.ToInt16(data, Schema.Endianness),
            var t when t == typeof(ushort) => ByteConverter.ToUInt16(data, Schema.Endianness),
            var t when t == typeof(int) => ByteConverter.ToInt32(data, Schema.Endianness),
            var t when t == typeof(uint) => ByteConverter.ToUInt32(data, Schema.Endianness),
            var t when t == typeof(float) => ByteConverter.ToSingle(data, Schema.Endianness),
            var t when t == typeof(bool) => Schema.BooleanType switch
            {
                BooleanRepresentation.Int16 => ByteConverter.ToInt16(data, Schema.Endianness) == 1,
                BooleanRepresentation.Int32 => ByteConverter.ToInt32(data, Schema.Endianness) == 1,
                _ => throw new NotSupportedException("不支持的布尔表示方式")
            },
            _ when fieldType.IsEnum => Enum.ToObject(fieldType, DecodeValueInternal(data, Enum.GetUnderlyingType(fieldType))),
            _ => throw new NotSupportedException($"不支持的类型 {fieldType.Name}")
        };
    }

    private int GetValueSize(Type type)
    {
        if (type == typeof(bool))
        {
            return Schema.BooleanType == BooleanRepresentation.Int32 ? 4 : 2;
        }

        if (type.IsEnum)
        {
            return Marshal.SizeOf(Enum.GetUnderlyingType(type));
        }

        return Marshal.SizeOf(type);
    }

    private static object GetFieldValue(TProtocol protocol, string memberName)
    {
        var type = typeof(TProtocol);
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            return field.GetValue(protocol)!;
        }

        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null)
        {
            return property.GetValue(protocol)!
                   ?? throw new InvalidOperationException($"属性 {memberName} 的值为 null");
        }

        throw new ArgumentException($"找不到字段或属性 {memberName}");
    }

    private static void SetFieldValue(object target, string memberName, object value)
    {
        var type = typeof(TProtocol);
        var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.SetMethod != null)
        {
            property.SetValue(target, value);
            return;
        }

        throw new ArgumentException($"找不到字段或属性 {memberName}");
    }

    private static string GetMemberName<TValue>(Expression<Func<TProtocol, TValue>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("无效的字段访问表达式", nameof(expression));
    }
}

