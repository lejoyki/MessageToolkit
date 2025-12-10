using System.Linq.Expressions;
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

    public ModbusFrame BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusFrame
        {
            FunctionCode = ModbusFunctionCode.WriteMultipleRegisters,
            StartAddress = (ushort)Schema.StartAddress,
            Data = Codec.Encode(protocol)
        };
    }

    public ModbusFrame BuildWriteFrame<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector, TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    public ModbusFrame BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusFrame
        {
            FunctionCode = ModbusFunctionCode.WriteMultipleRegisters,
            StartAddress = address,
            Data = Codec.EncodeValue(value)
        };
    }

    public ModbusFrame BuildReadFrame()
    {
        return new ModbusFrame
        {
            FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
            StartAddress = (ushort)Schema.StartAddress,
            Data = Array.Empty<byte>() // 读取帧不需要数据
        };
    }

    public ModbusFrame BuildReadFrame<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var fieldInfo = Schema.GetFieldInfo(GetMemberName(fieldSelector));
        return new ModbusFrame
        {
            FunctionCode = ModbusFunctionCode.ReadHoldingRegisters,
            StartAddress = fieldInfo.ByteAddress,
            Data = new byte[fieldInfo.Size]
        };
    }

    public IBatchFrameBuilder<TProtocol> CreateBatchBuilder()
    {
        return new BatchFrameBuilder<TProtocol>(this);
    }

    private static string GetMemberName<TValue>(Expression<Func<TProtocol, TValue>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("无效的字段访问表达式", nameof(expression));
    }
}

