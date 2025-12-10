using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
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
    private readonly Dictionary<string, PropertyInfo> _propertyMap;
    private readonly PropertyAccessor[] _orderedProperties;
    private readonly PropertyAccessor[] _booleanProperties;
    
    // 编码器委托缓存 (解码器因使用 ReadOnlySpan 无法缓存为委托)
    private readonly Func<object, byte[]>[] _encoders;
    private readonly Type[] _fieldTypes;

    public ProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));

        _propertyMap = typeof(TProtocol)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);

        _orderedProperties = BuildPropertyAccessors(Schema.Properties.Values);
        _booleanProperties = BuildBooleanPropertyAccessors();
        
        // 预编译编码器和缓存字段类型
        _encoders = new Func<object, byte[]>[_orderedProperties.Length];
        _fieldTypes = new Type[_orderedProperties.Length];
        
        for (int i = 0; i < _orderedProperties.Length; i++)
        {
            var fieldType = _orderedProperties[i].Info.FieldType;
            _encoders[i] = CreateEncoder(fieldType);
            _fieldTypes[i] = fieldType;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Encode(TProtocol protocol)
    {
        var buffer = new byte[Schema.TotalSize];
        var boxed = (object)protocol;

        for (int i = 0; i < _orderedProperties.Length; i++)
        {
            var accessor = _orderedProperties[i];
            var value = accessor.Getter(boxed)
                        ?? throw new InvalidOperationException($"属性 {accessor.Info.Name} 的值为 null");
            var bytes = _encoders[i](value);
            var offset = accessor.Info.ByteAddress - Schema.StartAddress;
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

        for (int i = 0; i < _orderedProperties.Length; i++)
        {
            var accessor = _orderedProperties[i];
            var offset = accessor.Info.ByteAddress - Schema.StartAddress;
            var valueBytes = data.Slice(offset, accessor.Info.Size);
            var value = DecodeValueFast(valueBytes, _fieldTypes[i]);
            accessor.Setter(ref boxed, value);
        }

        return (TProtocol)boxed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged
    {
        var type = typeof(TValue);
        return EncodeValueFast(value, type);
    }
    
    private byte[] EncodeValueFast(object value, Type fieldType)
    {
        if (fieldType == typeof(short))
            return ByteConverter.GetBytes((short)value, Schema.Endianness);
        if (fieldType == typeof(ushort))
            return ByteConverter.GetBytes((ushort)value, Schema.Endianness);
        if (fieldType == typeof(int))
            return ByteConverter.GetBytes((int)value, Schema.Endianness);
        if (fieldType == typeof(uint))
            return ByteConverter.GetBytes((uint)value, Schema.Endianness);
        if (fieldType == typeof(float))
            return ByteConverter.GetBytes((float)value, Schema.Endianness);
        if (fieldType == typeof(bool))
            return Schema.BooleanType switch
            {
                BooleanRepresentation.Int16 => ByteConverter.GetBytes((short)((bool)value ? 1 : 0), Schema.Endianness),
                BooleanRepresentation.Int32 => ByteConverter.GetBytes((int)((bool)value ? 1 : 0), Schema.Endianness),
                _ => throw new NotSupportedException("不支持的布尔表示方式")
            };
        if (fieldType.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(fieldType);
            return EncodeValueFast(Convert.ChangeType(value, underlyingType)!, underlyingType);
        }
        
        throw new NotSupportedException($"不支持的类型 {fieldType.Name}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue DecodeValue<TValue>(ReadOnlySpan<byte> data) where TValue : unmanaged
    {
        var expectedSize = GetValueSize(typeof(TValue));
        if (data.Length < expectedSize)
        {
            throw new ArgumentException($"数据长度不足: 需要 {expectedSize} 字节, 实际 {data.Length} 字节", nameof(data));
        }

        var value = DecodeValueFast(data[..expectedSize], typeof(TValue));
        return (TValue)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector)
    {
        var memberName = GetMemberName(fieldSelector);
        var fieldInfo = Schema.GetFieldInfo(memberName);
        var accessor = Array.Find(_orderedProperties, a => a.Info.Name == memberName);
        
        var value = accessor.Getter(protocol)
                    ?? throw new InvalidOperationException($"属性 {memberName} 的值为 null");
        return EncodeValueFast(value, fieldInfo.FieldType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<int, bool> ExtractBooleanValues(TProtocol protocol)
    {
        var result = new Dictionary<int, bool>(_booleanProperties.Length);
        var boxed = (object)protocol;
        
        for (int i = 0; i < _booleanProperties.Length; i++)
        {
            var accessor = _booleanProperties[i];
            var value = accessor.Getter(boxed);
            if (value is bool b)
            {
                result[accessor.Info.ByteAddress] = b;
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

    private PropertyAccessor[] BuildPropertyAccessors(IEnumerable<ProtocolFieldInfo> fields)
    {
        var orderedInfos = fields.OrderBy(f => f.ByteAddress).ToArray();
        var result = new PropertyAccessor[orderedInfos.Length];

        for (var i = 0; i < orderedInfos.Length; i++)
        {
            var info = orderedInfos[i];
            var property = GetProperty(info.Name);
            result[i] = new PropertyAccessor(info, property);
        }

        return result;
    }

    private PropertyAccessor[] BuildBooleanPropertyAccessors()
    {
        var booleanFields = Schema.BooleanProperties
            .Select(kvp => Schema.GetFieldInfo(kvp.Key))
            .OrderBy(f => f.ByteAddress)
            .ToArray();

        var result = new PropertyAccessor[booleanFields.Length];
        for (var i = 0; i < booleanFields.Length; i++)
        {
            var info = booleanFields[i];
            var property = GetProperty(info.Name);
            result[i] = new PropertyAccessor(info, property);
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
    
    private Func<object, byte[]> CreateEncoder(Type fieldType)
    {
        if (fieldType == typeof(short))
            return obj => ByteConverter.GetBytes((short)obj, Schema.Endianness);
        if (fieldType == typeof(ushort))
            return obj => ByteConverter.GetBytes((ushort)obj, Schema.Endianness);
        if (fieldType == typeof(int))
            return obj => ByteConverter.GetBytes((int)obj, Schema.Endianness);
        if (fieldType == typeof(uint))
            return obj => ByteConverter.GetBytes((uint)obj, Schema.Endianness);
        if (fieldType == typeof(float))
            return obj => ByteConverter.GetBytes((float)obj, Schema.Endianness);
        if (fieldType == typeof(bool))
            return Schema.BooleanType switch
            {
                BooleanRepresentation.Int16 => obj => ByteConverter.GetBytes((short)((bool)obj ? 1 : 0), Schema.Endianness),
                BooleanRepresentation.Int32 => obj => ByteConverter.GetBytes((int)((bool)obj ? 1 : 0), Schema.Endianness),
                _ => throw new NotSupportedException("不支持的布尔表示方式")
            };
        if (fieldType.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(fieldType);
            var underlyingEncoder = CreateEncoder(underlyingType);
            return obj => underlyingEncoder(Convert.ChangeType(obj, underlyingType)!);
        }
        
        throw new NotSupportedException($"不支持的类型 {fieldType.Name}");
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private object DecodeValueFast(ReadOnlySpan<byte> data, Type fieldType)
    {
        if (fieldType == typeof(short))
            return ByteConverter.ToInt16(data, Schema.Endianness);
        if (fieldType == typeof(ushort))
            return ByteConverter.ToUInt16(data, Schema.Endianness);
        if (fieldType == typeof(int))
            return ByteConverter.ToInt32(data, Schema.Endianness);
        if (fieldType == typeof(uint))
            return ByteConverter.ToUInt32(data, Schema.Endianness);
        if (fieldType == typeof(float))
            return ByteConverter.ToSingle(data, Schema.Endianness);
        if (fieldType == typeof(bool))
            return Schema.BooleanType switch
            {
                BooleanRepresentation.Int16 => ByteConverter.ToInt16(data, Schema.Endianness) == 1,
                BooleanRepresentation.Int32 => ByteConverter.ToInt32(data, Schema.Endianness) == 1,
                _ => throw new NotSupportedException("不支持的布尔表示方式")
            };
        if (fieldType.IsEnum)
        {
            var underlyingType = Enum.GetUnderlyingType(fieldType);
            return Enum.ToObject(fieldType, DecodeValueFast(data, underlyingType));
        }
        
        throw new NotSupportedException($"不支持的类型 {fieldType.Name}");
    }
    
    /// <summary>
    /// 属性访问器结构体，使用编译的委托提高性能
    /// </summary>
    private readonly struct PropertyAccessor
    {
        public readonly ProtocolFieldInfo Info;
        public readonly Func<object, object?> Getter;
        public readonly SetterDelegate Setter;

        public delegate void SetterDelegate(ref object instance, object? value);

        public PropertyAccessor(ProtocolFieldInfo info, PropertyInfo property)
        {
            Info = info;
            Getter = CreateGetter(property);
            Setter = CreateSetter(property);
        }

        private static Func<object, object?> CreateGetter(PropertyInfo property)
        {
            var instance = Expression.Parameter(typeof(object), "instance");
            var typedInstance = Expression.Convert(instance, property.DeclaringType!);
            var propertyAccess = Expression.Property(typedInstance, property);
            var convertToObject = Expression.Convert(propertyAccess, typeof(object));
            return Expression.Lambda<Func<object, object?>>(convertToObject, instance).Compile();
        }

        private static SetterDelegate CreateSetter(PropertyInfo property)
        {
            if (property.SetMethod == null)
            {
                throw new InvalidOperationException($"属性 {property.Name} 不可写");
            }

            // 对于 struct，我们需要使用 ref 参数来修改装箱的值
            // 由于 Expression 树不支持 ref object，我们使用反射但缓存 PropertyInfo
            var setMethod = property.SetMethod;
            return (ref object instance, object? value) =>
            {
                setMethod.Invoke(instance, [value]);
            };
        }
    }
}

