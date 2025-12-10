using System.Linq.Expressions;
using System.Runtime.InteropServices;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 帧构建器实现
/// </summary>
public sealed class ModbusFrameBuilder<TProtocol> : IFrameBuilder<TProtocol>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    public IProtocolCodec<TProtocol> Codec { get; }

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new ProtocolCodec<TProtocol>(schema))
    {
    }

    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema, IProtocolCodec<TProtocol> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    #region 写入帧构建

    public ModbusWriteFrame BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusWriteFrame(
            ModbusFunctionCode.WriteMultipleRegisters,
            (ushort)Schema.StartAddress,
            Codec.Encode(protocol));
    }

    public ModbusWriteFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    public ModbusWriteFrame BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusWriteFrame(
            ModbusFunctionCode.WriteMultipleRegisters,
            address,
            Codec.EncodeValue(value));
    }

    #endregion

    #region 读取请求构建

    public ModbusReadRequest BuildReadRequest()
    {
        return new ModbusReadRequest(
            ModbusFunctionCode.ReadHoldingRegisters,
            (ushort)Schema.StartAddress,
            (ushort)Schema.RegisterCount);
    }

    public ModbusReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var fieldInfo = Schema.GetFieldInfo(GetMemberName(fieldSelector));
        var registerCount = (ushort)((fieldInfo.Size + 1) / 2); // 向上取整

        return new ModbusReadRequest(
            ModbusFunctionCode.ReadHoldingRegisters,
            fieldInfo.ByteAddress,
            registerCount);
    }

    public ModbusReadRequest BuildReadRequest(ushort startAddress, ushort registerCount)
    {
        return new ModbusReadRequest(
            ModbusFunctionCode.ReadHoldingRegisters,
            startAddress,
            registerCount);
    }

    #endregion

    public IBatchFrameBuilder<TProtocol> CreateBatchBuilder()
    {
        return new BatchFrameBuilder<TProtocol>(this);
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
