using System.Collections;

namespace MessageToolkit.Models;

/// <summary>
/// 写入帧集合，支持连续地址合并
/// </summary>
public sealed class WriteFrameCollection : IEnumerable<ModbusWriteFrame>, IDisposable
{
    private readonly List<ModbusWriteFrame> _frames = [];
    private bool _disposed;

    /// <summary>
    /// 帧数量
    /// </summary>
    public int Count => _frames.Count;

    /// <summary>
    /// 添加帧
    /// </summary>
    public void Add(ModbusWriteFrame frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _frames.Add(frame);
    }

    /// <summary>
    /// 获取所有帧
    /// </summary>
    public IReadOnlyList<ModbusWriteFrame> Frames => _frames;

    /// <summary>
    /// 索引器
    /// </summary>
    public ModbusWriteFrame this[int index] => _frames[index];

    public IEnumerator<ModbusWriteFrame> GetEnumerator() => _frames.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
        _frames.Clear();
    }
}
