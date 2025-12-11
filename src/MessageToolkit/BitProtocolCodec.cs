using System.Reflection;
using System.Runtime.CompilerServices;
using MessageToolkit.Abstractions;

namespace MessageToolkit;

/// <summary>
/// 位协议编解码器 - 用于 bool 字段的协议
/// </summary>
/// <typeparam name="TProtocol">协议类型（仅包含 bool 属性）</typeparam>
public sealed class BitProtocolCodec<TProtocol, TData> : IProtocolCodec<TProtocol, TData>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }

    private readonly (string Name, ushort Address, PropertyInfo Property)[] _booleanProperties;

    public BitProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));

        // 缓存布尔属性信息
        _booleanProperties = Schema.Properties
            .Where(kvp => kvp.Value.FieldType == typeof(TData))
            .Select(kvp => (kvp.Key, kvp.Value.Address, typeof(TProtocol).GetProperty(kvp.Key)!))
            .ToArray();
    }

    public  TData[] Encode(TProtocol protocol)
    {
        var buffer = new TData[Schema.TotalSize];

        foreach (var (_, address, property) in _booleanProperties)
        {
            if (property.GetValue(protocol) is TData value)
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

    public TProtocol Decode(ReadOnlySpan<TData> data)
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
}
