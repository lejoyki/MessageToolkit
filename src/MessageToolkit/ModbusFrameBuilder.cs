using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 帧构建器 - 用于构建字节帧
/// </summary>
public sealed class ModbusFrameBuilder<TProtocol> : IFrameBuilder<TProtocol, byte>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    public IProtocolCodec<TProtocol, byte> Codec { get; }
    
    private readonly ByteProtocolCodec<TProtocol> _modbusCodec;

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new ByteProtocolCodec<TProtocol>(schema))
    {
    }

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema, ByteProtocolCodec<TProtocol> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _modbusCodec = codec ?? throw new ArgumentNullException(nameof(codec));
        Codec = codec;
    }

    #region 写入帧构建

    public IFrame<byte> BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusWriteFrame(
            (ushort)Schema.StartAddress,
            Codec.Encode(protocol));
    }

    /// <summary>
    /// 构建单个字段的写入帧
    /// </summary>
    public ModbusWriteFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    /// <summary>
    /// 构建指定地址的写入帧
    /// </summary>
    public ModbusWriteFrame BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusWriteFrame(address, _modbusCodec.EncodeValue(value));
    }

    #endregion

    #region 读取请求构建

    public IReadFrame BuildReadRequest()
    {
        return new ModbusReadRequest(
            (ushort)Schema.StartAddress,
            (ushort)Schema.TotalSize);
    }

    /// <summary>
    /// 构建单个字段的读取请求
    /// </summary>
    public ModbusReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var fieldInfo = Schema.GetFieldInfo(GetMemberName(fieldSelector));
        var registerCount = (ushort)((fieldInfo.Size + 1) / 2);

        return new ModbusReadRequest(
            fieldInfo.Address,
            registerCount);
    }

    public IReadFrame BuildReadRequest(ushort startAddress, ushort count)
    {
        return new ModbusReadRequest(startAddress, count);
    }

    #endregion

    public IDataMapping<TProtocol, byte> CreateDataMapping()
    {
        return new ModbusDataMapping<TProtocol>(Schema, _modbusCodec);
    }

    private static string GetMemberName<TValue>(Expression<Func<TProtocol, TValue>> expression)
    {
        return expression.Body switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            UnaryExpression { Operand: MemberExpression unaryMember } => unaryMember.Member.Name,
            _ => throw new ArgumentException("无效的字段访问表达式", nameof(expression))
        };
    }
}
