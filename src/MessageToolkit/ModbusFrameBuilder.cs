using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 帧构建器 - 用于构建字节协议帧
/// </summary>
public sealed class ModbusFrameBuilder<TProtocol> : IModbusFrameBuilder<TProtocol>
    where TProtocol : struct
{
    private readonly IModbusProtocolCodec<TProtocol> _codec;

    /// <summary>
    /// 协议模式
    /// </summary>
    public IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编解码器
    /// </summary>
    public IModbusProtocolCodec<TProtocol> Codec => _codec;

    /// <summary>
    /// 创建 Modbus 帧构建器
    /// </summary>
    /// <param name="schema">协议模式</param>
    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new ModbusProtocolCodec<TProtocol>(schema))
    {
    }

    /// <summary>
    /// 创建 Modbus 帧构建器
    /// </summary>
    /// <param name="schema">协议模式</param>
    /// <param name="codec">字节编解码器</param>
    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema, IModbusProtocolCodec<TProtocol> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    #region 写入帧构建

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    /// <param name="protocol">协议数据</param>
    /// <returns>写入帧，包含起始地址和编码后的字节数据</returns>
    public ModbusWriteFrame BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusWriteFrame((ushort)Schema.StartAddress, _codec.Encode(protocol));
    }

    /// <summary>
    /// 构建单个字段的写入帧
    /// </summary>
    /// <remarks>
    /// <typeparam name="TValue">字段值类型</typeparam>
    /// <param name="fieldSelector">字段选择器</param>
    /// <param name="value">要写入的值</param>
    /// <returns>写入帧</returns>
    public ModbusWriteFrame BuildWriteFrame<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector, TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }

    /// <summary>
    /// 构建指定地址的写入帧
    /// </summary>
    /// <typeparam name="TValue">值类型</typeparam>
    /// <param name="address">目标地址</param>
    /// <param name="value">要写入的值</param>
    /// <returns>写入帧</returns>
    public ModbusWriteFrame BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusWriteFrame(address, _codec.EncodeValue(value));
    }

    #endregion

    #region 读取请求构建

    /// <summary>
    /// 构建读取整个协议的请求
    /// </summary>
    /// <returns>读取请求，包含起始地址和寄存器数量</returns>
    public ModbusReadRequest BuildReadRequest()
    {
        // TotalSize 是字节数，寄存器数量 = 字节数 / 2（向上取整）
        var registerCount = (ushort)((Schema.TotalSize + 1) / 2);
        return new ModbusReadRequest((ushort)Schema.StartAddress, registerCount);
    }

    /// <summary>
    /// 构建单个字段的读取请求
    /// </summary>
    /// <typeparam name="TValue">字段值类型</typeparam>
    /// <param name="fieldSelector">字段选择器</param>
    /// <returns>读取请求</returns>
    public ModbusReadRequest BuildReadRequest<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var fieldInfo = Schema.GetFieldInfo(GetMemberName(fieldSelector));
        var registerCount = (ushort)((fieldInfo.Size + 1) / 2);

        return new ModbusReadRequest(fieldInfo.Address, registerCount);
    }

    /// <summary>
    /// 构建指定地址和数量的读取请求
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="registerCount">寄存器数量</param>
    /// <returns>读取请求</returns>
    public ModbusReadRequest BuildReadRequest(ushort startAddress, ushort registerCount)
    {
        return new ModbusReadRequest(startAddress, registerCount);
    }

    #endregion

    /// <summary>
    /// 创建数据映射（批量写入构建器）
    /// </summary>
    /// <returns>数据映射实例，支持链式 API</returns>
    public IModbusDataMapping<TProtocol> CreateDataMapping()
    {
        return new ModbusDataMapping<TProtocol>(Schema, _codec);
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
