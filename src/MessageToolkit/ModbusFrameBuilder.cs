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

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new ProtocolCodec<TProtocol>(schema))
    {
    }

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema, IProtocolCodec<TProtocol, byte> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    #region 写入帧构建

    public IWriteFrame<byte> BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusWriteFrame(
            (ushort)Schema.StartAddress,
            Codec.Encode(protocol));
    }

    public IWriteFrame<byte> BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    public IWriteFrame<byte> BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusWriteFrame(
            address,
            Codec.EncodeValue(value));
    }

    #endregion

    #region 读取请求构建

    public IReadRequest BuildReadRequest()
    {
        return new ModbusReadRequest(
            (ushort)Schema.StartAddress,
            (ushort)Schema.RegisterCount);
    }

    public IReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var fieldInfo = Schema.GetFieldInfo(GetMemberName(fieldSelector));
        var registerCount = (ushort)((fieldInfo.Size + 1) / 2);

        return new ModbusReadRequest(
            fieldInfo.Address,
            registerCount);
    }

    public IReadRequest BuildReadRequest(ushort startAddress, ushort count)
    {
        return new ModbusReadRequest(startAddress, count);
    }

    #endregion

    public IDataMapping<TProtocol, byte> CreateDataMapping()
    {
        return new ModbusBatchFrameBuilder<TProtocol>(this);
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
