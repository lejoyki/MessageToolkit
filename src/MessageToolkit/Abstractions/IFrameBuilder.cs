using System.Linq.Expressions;
using MessageToolkit.Models;

namespace MessageToolkit.Abstractions;

/// <summary>
/// Modbus 帧构建器接口
/// </summary>
public interface IFrameBuilder<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 协议编解码器
    /// </summary>
    IProtocolCodec<TProtocol> Codec { get; }

    #region 写入帧构建

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    ModbusWriteFrame BuildWriteFrame(TProtocol protocol);

    /// <summary>
    /// 构建写入单个字段的帧
    /// </summary>
    ModbusWriteFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 构建写入指定地址的帧
    /// </summary>
    ModbusWriteFrame BuildWriteFrame<TValue>(
        ushort address,
        TValue value) where TValue : unmanaged;

    #endregion

    #region 读取请求构建

    /// <summary>
    /// 构建读取整个协议的请求
    /// </summary>
    ModbusReadRequest BuildReadRequest();

    /// <summary>
    /// 构建读取单个字段的请求
    /// </summary>
    ModbusReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged;

    /// <summary>
    /// 构建读取指定地址和长度的请求
    /// </summary>
    ModbusReadRequest BuildReadRequest(ushort startAddress, ushort registerCount);

    #endregion

    /// <summary>
    /// 创建批量写入构建器
    /// </summary>
    IBatchFrameBuilder<TProtocol> CreateBatchBuilder();
}
