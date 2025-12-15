using MessageToolkit.Abstractions;
using MessageToolkit.Models;
using System.Linq.Expressions;

namespace MessageToolkit;

/// <summary>
/// 原生协议帧构建器 - 用于原生类型数据的帧构建
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
/// <typeparam name="TData">原生数据类型</typeparam>
public sealed class NativeFrameBuilder<TProtocol, TData> : INativeFrameBuilder<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    public IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编解码器
    /// </summary>
    public INativeProtocolCodec<TProtocol, TData> Codec { get; }

    /// <summary>
    /// 创建原生协议帧构建器
    /// </summary>
    public NativeFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new NativeProtocolCodec<TProtocol, TData>(schema))
    {
    }

    /// <summary>
    /// 创建原生协议帧构建器
    /// </summary>
    public NativeFrameBuilder(IProtocolSchema<TProtocol> schema, INativeProtocolCodec<TProtocol, TData> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    #region 写入帧构建

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    public WriteFrame<TData> BuildWriteFrame(TProtocol protocol)
    {
        return new WriteFrame<TData>(Schema.StartAddress, Codec.Encode(protocol));
    }

    /// <summary>
    /// 构建单个字段的写入帧
    /// </summary>
    public WriteFrame<TData> BuildWriteFrame(
        Expression<Func<TProtocol, TData>> fieldSelector,
        TData value)
    {
        var address = Schema.GetAddress(fieldSelector);
        return new WriteFrame<TData>(address, value);
    }

    /// <summary>
    /// 构建指定地址的写入帧
    /// </summary>
    public WriteFrame<TData> BuildWriteFrame(ushort address, TData value)
    {
        return new WriteFrame<TData>(address, value);
    }

    /// <summary>
    /// 构建多值写入帧
    /// </summary>
    public WriteFrame<TData> BuildWriteFrame(ushort startAddress, TData[] values)
    {
        return new WriteFrame<TData>(startAddress, values);
    }

    #endregion

    #region 读取请求构建

    /// <summary>
    /// 构建读取整个协议的请求
    /// </summary>
    public ReadFrame BuildReadRequest()
    {
        return new ReadFrame(Schema.StartAddress, Schema.TotalSize);
    }

    /// <summary>
    /// 构建单个字段的读取请求
    /// </summary>
    public ReadFrame BuildReadRequest(Expression<Func<TProtocol, TData>> fieldSelector)
    {
        var address = Schema.GetAddress(fieldSelector);
        return new ReadFrame(address, 1);
    }

    /// <summary>
    /// 构建指定地址和数量的读取请求
    /// </summary>
    public ReadFrame BuildReadRequest(ushort startAddress, ushort count)
    {
        return new ReadFrame(startAddress, count);
    }

    #endregion

    /// <summary>
    /// 创建数据映射（批量写入构建器）
    /// </summary>
    public INativeDataMapping<TProtocol, TData> CreateDataMapping()
    {
        return new NativeDataMapping<TProtocol, TData>(Schema);
    }
}
