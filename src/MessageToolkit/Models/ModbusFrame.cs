namespace MessageToolkit.Models;

/// <summary>
/// Modbus 帧信息
/// </summary>
public sealed class ModbusFrame
{
    /// <summary>
    /// 功能码
    /// </summary>
    public ModbusFunctionCode FunctionCode { get; init; }

    /// <summary>
    /// 起始地址（字节地址）
    /// </summary>
    public ushort StartAddress { get; init; }

    /// <summary>
    /// 寄存器地址（StartAddress / 2）
    /// </summary>
    public ushort RegisterAddress => (ushort)(StartAddress / 2);

    /// <summary>
    /// 寄存器数量
    /// </summary>
    public ushort RegisterCount => (ushort)(Data.Length / 2);

    /// <summary>
    /// 数据载荷
    /// </summary>
    public required byte[] Data { get; init; }

    /// <summary>
    /// 数据长度（字节）
    /// </summary>
    public int DataLength => Data.Length;
}

