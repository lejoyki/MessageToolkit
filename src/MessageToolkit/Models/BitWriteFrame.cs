using MessageToolkit.Abstractions;

namespace MessageToolkit.Models;

/// <summary>
/// 位写入帧
/// </summary>
public readonly struct BitWriteFrame : IFrame<bool>
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public int StartAddress { get; }

    /// <summary>
    /// 数据载荷
    /// </summary>
    public ReadOnlyMemory<bool> Data { get; }

    /// <summary>
    /// 数据长度（点位数量）
    /// </summary>
    public int DataLength => Data.Length;

    /// <summary>
    /// 创建位写入帧
    /// </summary>
    public BitWriteFrame(int startAddress, bool[] data)
    {
        StartAddress = startAddress;
        Data = data;
    }

    /// <summary>
    /// 创建位写入帧
    /// </summary>
    public BitWriteFrame(int startAddress, ReadOnlyMemory<bool> data)
    {
        StartAddress = startAddress;
        Data = data;
    }

    /// <summary>
    /// 创建单点写入帧
    /// </summary>
    public BitWriteFrame(int address, bool value)
    {
        StartAddress = address;
        Data = new[] { value };
    }

    /// <summary>
    /// 获取数据的副本
    /// </summary>
    public bool[] ToArray() => Data.ToArray();
}

