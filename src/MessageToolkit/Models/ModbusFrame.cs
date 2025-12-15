using System.Buffers;
using MessageToolkit.Abstractions;

namespace MessageToolkit.Models;

/// <summary>
/// Modbus 写入帧 - 字节数据帧实现
/// </summary>
public readonly struct ModbusWriteFrame : IFrame<byte>
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public ushort StartAddress { get; }

    /// <summary>
    /// IFrame.StartAddress 显式实现
    /// </summary>
    int IFrame.StartAddress => StartAddress;

    /// <summary>
    /// 寄存器地址（StartAddress / 2）
    /// </summary>
    public ushort RegisterAddress => (ushort)(StartAddress / 2);

    /// <summary>
    /// 数据载荷
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// 数据长度（字节）
    /// </summary>
    public int DataLength => Data.Length;

    /// <summary>
    /// 寄存器数量
    /// </summary>
    public ushort RegisterCount => (ushort)(Data.Length / 2);

    /// <summary>
    /// 创建写入帧（使用已有数据）
    /// </summary>
    public ModbusWriteFrame(ushort startAddress, byte[] data)
    {
        StartAddress = startAddress;
        Data = data;
    }
}

/// <summary>
/// Modbus 读取请求 - 只包含地址和数量信息
/// </summary>
public readonly struct ModbusReadRequest : IReadFrame
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public ushort StartAddress { get; }

    /// <summary>
    /// IFrame.StartAddress 显式实现
    /// </summary>
    int IFrame.StartAddress => StartAddress;

    /// <summary>
    /// 寄存器地址（StartAddress / 2）
    /// </summary>
    public ushort RegisterAddress => (ushort)(StartAddress / 2);

    /// <summary>
    /// 要读取的寄存器数量
    /// </summary>
    public ushort RegisterCount { get; }

    /// <summary>
    /// 请求读取的元素数量（字节数）
    /// </summary>
    public int Count => RegisterCount * 2;

    /// <summary>
    /// 要读取的字节数
    /// </summary>
    public int ByteCount => RegisterCount * 2;

    /// <summary>
    /// 创建读取请求
    /// </summary>
    public ModbusReadRequest(ushort startAddress, ushort registerCount)
    {
        StartAddress = startAddress;
        RegisterCount = registerCount;
    }
}
