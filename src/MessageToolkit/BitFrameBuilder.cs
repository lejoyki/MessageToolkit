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

    public IWriteFrame<bool> BuildWriteFrame(TProtocol protocol)
    {
        return new BitWriteFrame(Schema.StartAddress, Codec.Encode(protocol));
    }

    public IWriteFrame<bool> BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    public IWriteFrame<bool> BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new BitWriteFrame(address, Codec.EncodeValue(value));
    }

    #endregion

    #region 读取请求构建

    public IReadRequest BuildReadRequest()
    {
        return new BitReadRequest(Schema.StartAddress, Schema.TotalSize);
    }

    public IReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return new BitReadRequest(address, 1);
    }

    public IReadRequest BuildReadRequest(ushort startAddress, ushort count)
    {
        return new BitReadRequest(startAddress, count);
    }

    #endregion

    public IDataMapping<TProtocol, bool> CreateDataMapping()
    {
        return new BitDataMapping<TProtocol>(this.Schema);
    }
}
