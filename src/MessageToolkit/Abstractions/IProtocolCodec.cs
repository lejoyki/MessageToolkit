namespace MessageToolkit.Abstractions;

/// <summary>
/// 泛型协议编解码器接口
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IProtocolCodec<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 序列化整个协议为数据数组
    /// </summary>
    TData[] Encode(TProtocol protocol);

    /// <summary>
    /// 从数据数组反序列化协议
    /// </summary>
    TProtocol Decode(ReadOnlySpan<TData> data);

    /// <summary>
    /// 序列化单个值
    /// </summary>
    TData[] EncodeValue<TValue>(TValue value) where TValue : unmanaged;

    /// <summary>
    /// 反序列化单个值
    /// </summary>
    TValue DecodeValue<TValue>(ReadOnlySpan<TData> data) where TValue : unmanaged;
}
