namespace MessageToolkit.Abstractions;

/// <summary>
/// 原生协议编解码器接口 - 仅地址映射，无类型转换
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
/// <typeparam name="TData">原生数据类型</typeparam>
public interface INativeProtocolCodec<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编码：协议结构体 → 原生数组（仅地址映射）
    /// </summary>
    TData[] Encode(TProtocol protocol);

    /// <summary>
    /// 解码：原生数组 → 协议结构体（仅地址映射）
    /// </summary>
    TProtocol Decode(ReadOnlySpan<TData> data);

    /// <summary>
    /// 提取协议中所有布尔字段的值
    /// </summary>
    Dictionary<int, bool> ExtractBooleanValues(TProtocol protocol);

    /// <summary>
    /// 映射的属性数量
    /// </summary>
    int MappedPropertyCount { get; }
}
