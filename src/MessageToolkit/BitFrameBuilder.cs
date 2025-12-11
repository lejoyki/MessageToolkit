using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 位帧构建器 - 用于构建 IO 点位帧
/// </summary>
/// <typeparam name="TProtocol">协议类型（仅包含 bool 属性）</typeparam>
public sealed class BitFrameBuilder<TProtocol> : IFrameBuilder<TProtocol, bool>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    public IProtocolCodec<TProtocol, bool> Codec { get; }

    public BitFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new BitProtocolCodec<TProtocol>(schema))
    {
    }

    public BitFrameBuilder(IProtocolSchema<TProtocol> schema, IProtocolCodec<TProtocol, bool> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    #region 写入帧构建

    public IFrame<bool> BuildWriteFrame(TProtocol protocol)
    {
        return new BitWriteFrame(Schema.StartAddress, Codec.Encode(protocol));
    }

    /// <summary>
    /// 构建单个字段的写入帧
    /// </summary>
    public BitWriteFrame BuildWriteFrame(Expression<Func<TProtocol, bool>> fieldSelector, bool value)
    {
        var address = Schema.GetAddress(fieldSelector);
        return new BitWriteFrame(address, value);
    }

    /// <summary>
    /// 构建指定地址的写入帧
    /// </summary>
    public BitWriteFrame BuildWriteFrame(ushort address, bool value)
    {
        return new BitWriteFrame(address, value);
    }

    #endregion

    #region 读取请求构建

    public IReadFrame BuildReadRequest()
    {
        return new ReadFrame(Schema.StartAddress, Schema.TotalSize);
    }

    /// <summary>
    /// 构建单个字段的读取请求
    /// </summary>
    public ReadFrame BuildReadRequest(Expression<Func<TProtocol, bool>> fieldSelector)
    {
        var address = Schema.GetAddress(fieldSelector);
        return new ReadFrame(address, 1);
    }

    public IReadFrame BuildReadRequest(ushort startAddress, ushort count)
    {
        return new ReadFrame(startAddress, count);
    }

    #endregion

    public IDataMapping<TProtocol, bool> CreateDataMapping()
    {
        return new BitDataMapping<TProtocol>(Schema);
    }
}
