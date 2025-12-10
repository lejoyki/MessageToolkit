using System.Linq.Expressions;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 协议编解码器接口
/// </summary>
public interface IProtocolCodec<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 序列化整个协议
    /// </summary>
    byte[] Encode(TProtocol protocol);

    /// <summary>
    /// 反序列化整个协议
    /// </summary>
    TProtocol Decode(ReadOnlySpan<byte> data);

    /// <summary>
    /// 序列化单个字段值
    /// </summary>
    byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged;

    /// <summary>
    /// 反序列化单个字段值
    /// </summary>
    TValue DecodeValue<TValue>(ReadOnlySpan<byte> data) where TValue : unmanaged;

    /// <summary>
    /// 获取字段的序列化字节
    /// </summary>
    byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector);
}

