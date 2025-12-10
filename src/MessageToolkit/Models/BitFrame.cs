using MessageToolkit.Abstractions;

namespace MessageToolkit.Models;

/// <summary>
/// 位写入帧 - 布尔数据帧实现（用于 IO 点位）
/// </summary>
public readonly struct BitWriteFrame : IWriteFrame<bool>
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
    /// 创建位写入帧（使用已有数据）
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

/// <summary>
/// 位读取请求 - 用于 IO 点位读取
/// </summary>
public readonly struct BitReadRequest : IReadRequest
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public int StartAddress { get; }

    /// <summary>
    /// 请求读取的点位数量
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 数据长度（请求不携带数据）
    /// </summary>
    public int DataLength => 0;

    /// <summary>
    /// 创建位读取请求
    /// </summary>
    public BitReadRequest(int startAddress, int count)
    {
        StartAddress = startAddress;
        Count = count;
    }

    /// <summary>
    /// 创建单点读取请求
    /// </summary>
    public static BitReadRequest ReadSingle(int address)
        => new(address, 1);

    /// <summary>
    /// 创建多点读取请求
    /// </summary>
    public static BitReadRequest ReadMultiple(int startAddress, int count)
        => new(startAddress, count);
}
