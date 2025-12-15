using System.Reflection;
using System.Runtime.CompilerServices;
using MessageToolkit.Abstractions;

namespace MessageToolkit;

/// <summary>
/// 原生协议编解码器 - 直接地址映射，无类型转换
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
/// <typeparam name="TData">原生数据类型（bool、byte、int 等）</typeparam>
public sealed class NativeProtocolCodec<TProtocol, TData> : INativeProtocolCodec<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    public IProtocolSchema<TProtocol> Schema { get; }

    private readonly (string Name, ushort Address, PropertyInfo Property)[] _mappedProperties;

    /// <summary>
    /// 创建原生协议编解码器
    /// </summary>
    /// <param name="schema">协议模式</param>
    public NativeProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));

        // 缓存匹配 TData 类型的属性信息
        _mappedProperties = Schema.Properties
            .Where(kvp => kvp.Value.FieldType == typeof(TData))
            .Select(kvp => (kvp.Key, kvp.Value.Address, typeof(TProtocol).GetProperty(kvp.Key)!))
            .OrderBy(x => x.Address)
            .ToArray();
    }

    /// <summary>
    /// 编码：协议结构体 → 原生数组（仅地址映射，无类型转换）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TData[] Encode(TProtocol protocol)
    {
        var buffer = new TData[Schema.TotalSize];

        foreach (var (_, address, property) in _mappedProperties)
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

    /// <summary>
    /// 解码：原生数组 → 协议结构体（仅地址映射，无类型转换）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TProtocol Decode(ReadOnlySpan<TData> data)
    {
        var result = new TProtocol();
        object boxed = result;

        foreach (var (_, address, property) in _mappedProperties)
        {
            var offset = address - Schema.StartAddress;
            if (offset >= 0 && offset < data.Length && property.SetMethod != null)
            {
                property.SetValue(boxed, data[offset]);
            }
        }

        return (TProtocol)boxed;
    }

    // <summary>
    /// 提取协议中所有布尔字段的值
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<int, bool> ExtractBooleanValues(TProtocol protocol)
    {
        var result = new Dictionary<int, bool>();
        foreach (var (_, address, property) in _mappedProperties)
        {
            if (property.PropertyType == typeof(bool))
            {
                var value = property.GetValue(protocol);
                if (value is bool b)
                {
                    result[address] = b;
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 获取映射的属性数量
    /// </summary>
    public int MappedPropertyCount => _mappedProperties.Length;
}
