using System.Linq.Expressions;

namespace MessageToolkit.Abstractions;

/// <summary>
/// Modbus 协议编解码器接口 - 字节协议，包含类型转换
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
public interface IModbusProtocolCodec<TProtocol>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编码：协议结构体 → 字节数组
    /// </summary>
    byte[] Encode(TProtocol protocol);

    /// <summary>
    /// 解码：字节数组 → 协议结构体
    /// </summary>
    TProtocol Decode(ReadOnlySpan<byte> data);

    /// <summary>
    /// 编码单个值
    /// </summary>
    byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged;

    /// <summary>
    /// 解码单个值
    /// </summary>
    TValue DecodeValue<TValue>(ReadOnlySpan<byte> data) where TValue : unmanaged;

    /// <summary>
    /// 编码协议中的指定字段
    /// </summary>
    byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector);

    /// <summary>
    /// 提取协议中所有布尔字段的值
    /// </summary>
    Dictionary<int, bool> ExtractBooleanValues(TProtocol protocol);
}
