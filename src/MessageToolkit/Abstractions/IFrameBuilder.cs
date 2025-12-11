using System.Linq.Expressions;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 帧构建器接口
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IFrameBuilder<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编解码器
    /// </summary>
    IProtocolCodec<TProtocol, TData> Codec { get; }

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    IFrame<TData> BuildWriteFrame(TProtocol protocol);

    /// <summary>
    /// 构建读取整个协议的请求
    /// </summary>
    IReadFrame BuildReadRequest();

    /// <summary>
    /// 构建读取指定地址和数量的请求
    /// </summary>
    IReadFrame BuildReadRequest(ushort startAddress, ushort count);

    /// <summary>
    /// 创建数据映射（批量写入构建器）
    /// </summary>
    IDataMapping<TProtocol, TData> CreateDataMapping();
}
