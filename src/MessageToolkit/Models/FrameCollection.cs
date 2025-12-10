using System.Collections;
using System.Linq;

namespace MessageToolkit.Models;

/// <summary>
/// 帧集合，支持连续地址合并
/// </summary>
public sealed class FrameCollection : IEnumerable<ModbusFrame>
{
    private readonly List<ModbusFrame> _frames = [];

    /// <summary>
    /// 帧数量
    /// </summary>
    public int Count => _frames.Count;

    /// <summary>
    /// 添加帧
    /// </summary>
    public void Add(ModbusFrame frame) => _frames.Add(frame);

    /// <summary>
    /// 合并连续地址的帧
    /// </summary>
    public FrameCollection Optimize()
    {
        var ordered = _frames.OrderBy(f => f.StartAddress).ToList();
        var optimized = new FrameCollection();

        var index = 0;
        while (index < ordered.Count)
        {
            var current = ordered[index];
            var mergedData = new List<byte>(current.Data);
            var currentFunction = current.FunctionCode;
            var expectedNextStart = current.StartAddress + current.Data.Length;

            var nextIndex = index + 1;
            while (nextIndex < ordered.Count)
            {
                var next = ordered[nextIndex];
                if (next.FunctionCode != currentFunction || next.StartAddress != expectedNextStart)
                {
                    break;
                }

                mergedData.AddRange(next.Data);
                expectedNextStart = (ushort)(next.StartAddress + next.Data.Length);
                nextIndex++;
            }

            optimized.Add(new ModbusFrame
            {
                FunctionCode = currentFunction,
                StartAddress = current.StartAddress,
                Data = mergedData.ToArray()
            });

            index = nextIndex;
        }

        return optimized;
    }

    /// <summary>
    /// 获取所有帧
    /// </summary>
    public IReadOnlyList<ModbusFrame> Frames => _frames.AsReadOnly();

    public IEnumerator<ModbusFrame> GetEnumerator() => _frames.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

