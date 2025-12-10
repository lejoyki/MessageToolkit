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

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    ModbusFrame BuildWriteFrame(TProtocol protocol);

    /// <summary>
    /// 构建写入单个字段的帧
    /// </summary>
    ModbusFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 构建写入指定地址的帧
    /// </summary>
    ModbusFrame BuildWriteFrame<TValue>(
        ushort address,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 构建读取整个协议的帧信息
    /// </summary>
    ModbusFrame BuildReadFrame();

    /// <summary>
    /// 构建读取单个字段的帧信息
    /// </summary>
    ModbusFrame BuildReadFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged;

    /// <summary>
    /// 创建批量写入构建器
    /// </summary>
    IBatchFrameBuilder<TProtocol> CreateBatchBuilder();
}

