using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 数据映射 - 用于批量构建字节帧
/// </summary>
public sealed class ModbusDataMapping<TProtocol> : IDataMapping<TProtocol, byte>
    where TProtocol : struct
{
    private readonly IProtocolSchema<TProtocol> _schema;
    private readonly ByteProtocolCodec<TProtocol> _codec;
    private readonly List<WriteEntry> _pendingWrites;

    private const int DefaultCapacity = 16;

    public int Count => _pendingWrites.Count;

    public ModbusDataMapping(IProtocolSchema<TProtocol> schema, ByteProtocolCodec<TProtocol> codec)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _pendingWrites = new List<WriteEntry>(DefaultCapacity);
    }

    public void AddData(int address, byte data)
    {
        _pendingWrites.Add(new WriteEntry((ushort)address, [data]));
    }

    /// <summary>
    /// 添加字段写入（链式调用）
    /// </summary>
    public IDataMapping<TProtocol, byte> Property<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = _schema.GetAddress(fieldSelector);
        var data = _codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    /// <summary>
    /// 添加地址写入（链式调用）
    /// </summary>
    public IDataMapping<TProtocol, byte> Property<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IEnumerable<IFrame<byte>> Build()
    {
        foreach (var entry in _pendingWrites)
        {
            yield return new ModbusWriteFrame(entry.Address, entry.Data);
        }
    }

    public IEnumerable<IFrame<byte>> BuildOptimized()
    {
        if (_pendingWrites.Count == 0)
        {
            yield break;
        }

        var entries = _pendingWrites.ToArray();
        Array.Sort(entries, static (a, b) => a.Address.CompareTo(b.Address));

        var index = 0;
        while (index < entries.Length)
        {
            var startEntry = entries[index];
            var startAddr = startEntry.Address;

            var totalLength = startEntry.Data.Length;
            var endAddr = startAddr + startEntry.Data.Length;
            var mergeCount = 1;

            for (var i = index + 1; i < entries.Length; i++)
            {
                var nextEntry = entries[i];
                if (nextEntry.Address != endAddr)
                    break;

                totalLength += nextEntry.Data.Length;
                endAddr = nextEntry.Address + nextEntry.Data.Length;
                mergeCount++;
            }

            if (mergeCount == 1)
            {
                yield return new ModbusWriteFrame(startAddr, startEntry.Data);
            }
            else
            {
                var mergedData = new byte[totalLength];
                var offset = 0;

                for (var i = index; i < index + mergeCount; i++)
                {
                    var data = entries[i].Data;
                    data.CopyTo(mergedData, offset);
                    offset += data.Length;
                }

                yield return new ModbusWriteFrame(startAddr, mergedData);
            }

            index += mergeCount;
        }
    }

    public void Clear()
    {
        _pendingWrites.Clear();
    }

    private readonly struct WriteEntry(ushort address, byte[] data)
    {
        public ushort Address { get; } = address;
        public byte[] Data { get; } = data;
    }
}
