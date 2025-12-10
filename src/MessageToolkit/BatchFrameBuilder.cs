using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 批量帧构建器实现
/// </summary>
internal sealed class BatchFrameBuilder<TProtocol> : IBatchFrameBuilder<TProtocol>
    where TProtocol : struct
{
    private readonly IFrameBuilder<TProtocol> _frameBuilder;
    private readonly List<WriteEntry> _pendingWrites;

    // 预分配容量，避免频繁扩容
    private const int DefaultCapacity = 16;

    public int Count => _pendingWrites.Count;

    public BatchFrameBuilder(IFrameBuilder<TProtocol> frameBuilder)
    {
        _frameBuilder = frameBuilder;
        _pendingWrites = new List<WriteEntry>(DefaultCapacity);
    }

    public IBatchFrameBuilder<TProtocol> Write<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = _frameBuilder.Schema.GetAddress(fieldSelector);
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IBatchFrameBuilder<TProtocol> Write<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IBatchFrameBuilder<TProtocol> WriteRaw(ushort address, ReadOnlySpan<byte> data)
    {
        _pendingWrites.Add(new WriteEntry(address, data.ToArray()));
        return this;
    }

    public WriteFrameCollection Build()
    {
        var collection = new WriteFrameCollection();

        foreach (var entry in _pendingWrites)
        {
            collection.Add(new ModbusWriteFrame(
                ModbusFunctionCode.WriteMultipleRegisters,
                entry.Address,
                entry.Data));
        }

        return collection;
    }

    public WriteFrameCollection BuildOptimized()
    {
        if (_pendingWrites.Count == 0)
        {
            return new WriteFrameCollection();
        }

        // 使用 Span 排序避免分配
        var entries = _pendingWrites.ToArray();
        Array.Sort(entries, static (a, b) => a.Address.CompareTo(b.Address));

        var collection = new WriteFrameCollection();
        var index = 0;

        while (index < entries.Length)
        {
            var startEntry = entries[index];
            var startAddr = startEntry.Address;

            // 计算需要合并的数据总长度
            var totalLength = startEntry.Data.Length;
            var endAddr = startAddr + startEntry.Data.Length;
            var mergeCount = 1;

            // 查找可以合并的连续条目
            for (var i = index + 1; i < entries.Length; i++)
            {
                var nextEntry = entries[i];
                if (nextEntry.Address != endAddr)
                    break;

                totalLength += nextEntry.Data.Length;
                endAddr = nextEntry.Address + nextEntry.Data.Length;
                mergeCount++;
            }

            // 如果只有一个条目，直接使用原数据
            if (mergeCount == 1)
            {
                collection.Add(new ModbusWriteFrame(
                    ModbusFunctionCode.WriteMultipleRegisters,
                    startAddr,
                    startEntry.Data));
            }
            else
            {
                // 合并多个条目的数据
                var mergedData = new byte[totalLength];
                var offset = 0;

                for (var i = index; i < index + mergeCount; i++)
                {
                    var data = entries[i].Data;
                    data.CopyTo(mergedData, offset);
                    offset += data.Length;
                }

                collection.Add(new ModbusWriteFrame(
                    ModbusFunctionCode.WriteMultipleRegisters,
                    startAddr,
                    mergedData));
            }

            index += mergeCount;
        }

        return collection;
    }

    public void Clear()
    {
        _pendingWrites.Clear();
    }

    /// <summary>
    /// 写入条目 - 使用 readonly struct 减少内存分配
    /// </summary>
    private readonly struct WriteEntry
    {
        public readonly ushort Address;
        public readonly byte[] Data;

        public WriteEntry(ushort address, byte[] data)
        {
            Address = address;
            Data = data;
        }
    }
}
