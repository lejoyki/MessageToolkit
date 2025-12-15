using MessageToolkit.Abstractions;

namespace MessageToolkit.Models;

/// <summary>
/// 原生类型写入帧 - 用于原生类型数据的写入
/// </summary>
/// <typeparam name="TData">原生数据类型</typeparam>
public readonly struct WriteFrame<TData> : IFrame<TData>
{
    /// <summary>
    /// 起始地址
    /// </summary>
    public int StartAddress { get; }

    /// <summary>
    /// 数据载荷
    /// </summary>
    public TData[] Data { get; }

    /// <summary>
    /// 数据长度
    /// </summary>
    public int DataLength => Data.Length;

    /// <summary>
    /// 创建写入帧（数组数据）
    /// </summary>
    /// <param name="startAddress">起始地址</param>
    /// <param name="data">数据数组</param>
    public WriteFrame(int startAddress, TData[] data)
    {
        StartAddress = startAddress;
        Data = data;
    }

    /// <summary>
    /// 创建单值写入帧
    /// </summary>
    /// <param name="address">地址</param>
    /// <param name="value">值</param>
    public WriteFrame(int address, TData value)
    {
        StartAddress = address;
        Data = [value];
    }
}
