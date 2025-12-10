using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using MessageToolkit.Abstractions;
using MessageToolkit.Internal;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 协议编解码器 - 用于字节数据的编解码
/// </summary>
public sealed class ProtocolCodec<TProtocol> : IProtocolCodec<TProtocol, byte>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    private readonly Dictionary<string, PropertyInfo> _propertyMap;
    private readonly (ProtocolFieldInfo Info, PropertyInfo Property)[] _orderedProperties;

    public ProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));

        _propertyMap = typeof(TProtocol)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

        _orderedProperties = BuildPropertyAccessors(Schema.Properties.Values);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Encode(TProtocol protocol)
    {
        var buffer = new byte[Schema.TotalSize];

        foreach (var (fieldInfo, property) in _orderedProperties)
        {
            var value = property.GetValue(protocol)
                        ?? throw new InvalidOperationException($"属性 {fieldInfo.Name} 的值为 null");
            var bytes = EncodeValueInternal(value, fieldInfo.FieldType);
            var offset = fieldInfo.Address - Schema.StartAddress;
            bytes.CopyTo(buffer.AsSpan(offset));
        }

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TProtocol Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < Schema.TotalSize)
        {
            throw new ArgumentException($"数据长度不足: 需要 {Schema.TotalSize} 字节, 实际 {data.Length} 字节", nameof(data));
        }

        var result = new TProtocol();
        object boxed = result;

        foreach (var (fieldInfo, property) in _orderedProperties)
        {
            var offset = fieldInfo.Address - Schema.StartAddress;
            var valueBytes = data.Slice(offset, fieldInfo.Size);
            var value = DecodeValueInternal(valueBytes, fieldInfo.FieldType);
            if (property.SetMethod == null)
            {
                throw new InvalidOperationException($"属性 {fieldInfo.Name} 不可写");
            }
            property.SetValue(boxed, value);
        }

        return (TProtocol)boxed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged
    {
        var type = typeof(TValue);
        return EncodeValueInternal(value, type);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector)
    {
        var memberName = GetMemberName(fieldSelector);
        var property = GetProperty(memberName);
        var value = property.GetValue(protocol)
                    ?? throw new InvalidOperationException($"属性 {memberName} 的值为 null");
        var fieldInfo = Schema.GetFieldInfo(memberName);
        return EncodeValueInternal(value, fieldInfo.FieldType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<int, bool> ExtractBooleanValues(TProtocol protocol)
    {
        var result = new Dictionary<int, bool>();
        foreach (var (Info, Property) in _orderedProperties)
        {
            if (Info.FieldType == typeof(bool))
            {
                var value = Property.GetValue(protocol);
                if (value is not bool b)
                {
                    continue;
                }

                result[Info.Address] = b;
            }
        }

        return result;
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

    private (ProtocolFieldInfo Info, PropertyInfo Property)[] BuildPropertyAccessors(IEnumerable<ProtocolFieldInfo> fields)
    {
        var orderedInfos = fields.OrderBy(f => f.Address).ToArray();
        var result = new (ProtocolFieldInfo Info, PropertyInfo Property)[orderedInfos.Length];

        for (var i = 0; i < orderedInfos.Length; i++)
        {
            var info = orderedInfos[i];
            var property = GetProperty(info.Name);
            result[i] = (info, property);
        }

        return result;
    }


    private PropertyInfo GetProperty(string name)
    {
        if (_propertyMap.TryGetValue(name, out var property))
        {
            return property;
        }

        throw new ArgumentException($"找不到属性 {name}，请确认协议模型仅包含带 Address 特性的公共属性。");
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
}

