using System.Reflection;
using System.Runtime.CompilerServices;
using MessageToolkit.Abstractions;

namespace MessageToolkit;

/// <summary>
/// 位协议编解码器 - 用于 bool 字段的协议
/// </summary>
/// <typeparam name="TProtocol">协议类型（仅包含 bool 属性）</typeparam>
public sealed class BitProtocolCodec<TProtocol> : IProtocolCodec<TProtocol, bool>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }

    private readonly (string Name, ushort Address, PropertyInfo Property)[] _booleanProperties;

    public BitProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));

        // 缓存布尔属性信息
        _booleanProperties = Schema.BooleanProperties
            .Select(kvp => (kvp.Key, kvp.Value, typeof(TProtocol).GetProperty(kvp.Key)!))
            .ToArray();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool[] Encode(TProtocol protocol)
    {
        var buffer = new bool[Schema.TotalSize];

        foreach (var (_, address, property) in _booleanProperties)
        {
            if (property.GetValue(protocol) is bool value)
            {
                var offset = address - Schema.StartAddress;
                if (offset >= 0 && offset < buffer.Length)
                {
                    buffer[offset] = value;
                }
            }
        }

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TProtocol Decode(ReadOnlySpan<bool> data)
    {
        var result = new TProtocol();
        object boxed = result;

        foreach (var (_, address, property) in _booleanProperties)
        {
            var offset = address - Schema.StartAddress;
            if (offset >= 0 && offset < data.Length && property.SetMethod != null)
            {
                property.SetValue(boxed, data[offset]);
            }
        }

        return (TProtocol)boxed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool[] EncodeValue<TValue>(TValue value) where TValue : unmanaged
    {
        if (value is bool b)
        {
            return [b];
        }

        throw new NotSupportedException($"BitProtocolCodec 仅支持 bool 类型，不支持 {typeof(TValue).Name}");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue DecodeValue<TValue>(ReadOnlySpan<bool> data) where TValue : unmanaged
    {
        if (typeof(TValue) == typeof(bool) && data.Length > 0)
        {
            return (TValue)(object)data[0];
        }

        throw new NotSupportedException($"BitProtocolCodec 仅支持 bool 类型，不支持 {typeof(TValue).Name}");
    }
}
