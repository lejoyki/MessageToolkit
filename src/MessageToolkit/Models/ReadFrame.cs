using MessageToolkit.Abstractions;

namespace MessageToolkit.Models;

/// <summary>
/// 读取请求
/// </summary>
public readonly struct ReadFrame(int startAddress, int count) : IReadFrame
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public int StartAddress { get; } = startAddress;

    /// <summary>
    /// 请求读取的点位数量
    /// </summary>
    public int Count { get; } = count;

    /// <summary>
    /// 创建单点读取请求
    /// </summary>
    public static ReadFrame ReadSingle(int address)
        => new(address, 1);

    /// <summary>
    /// 创建多点读取请求
    /// </summary>
    public static ReadFrame ReadMultiple(int startAddress, int count)
        => new(startAddress, count);
}