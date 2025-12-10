using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// Modbus 数据映射 - 用于批量构建字节帧
/// </summary>
internal sealed class ModbusBatchFrameBuilder<TProtocol> : IDataMapping<TProtocol, byte>
    where TProtocol : struct
{
    private readonly IFrameBuilder<TProtocol, byte> _frameBuilder;
    private readonly List<WriteEntry> _pendingWrites;

    private const int DefaultCapacity = 16;

    public int Count => _pendingWrites.Count;

    public ModbusBatchFrameBuilder(IFrameBuilder<TProtocol, byte> frameBuilder)
    {
        _frameBuilder = frameBuilder;
        _pendingWrites = new List<WriteEntry>(DefaultCapacity);
    }

    public void AddData(int address, byte data)
    {
        _pendingWrites.Add(new WriteEntry((ushort)address, [data]));
    }

    public IDataMapping<TProtocol, byte> Property<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = _frameBuilder.Schema.GetAddress(fieldSelector);
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IDataMapping<TProtocol, byte> Property<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IValueSetter<TProtocol, byte> Property<TValue>(
        Expression<Func<TProtocol, TValue>> propertyExpression)
    {
        var address = _frameBuilder.Schema.GetAddress(propertyExpression);
        return new ByteValueSetter<TProtocol>(this, (ushort)address, _frameBuilder.Codec);
    }

    public IValueSetter<TProtocol, byte> Property(int address)
    {
        return new ByteValueSetter<TProtocol>(this, (ushort)address, _frameBuilder.Codec);
    }

    public IEnumerable<IWriteFrame<byte>> Build()
    {
        foreach (var entry in _pendingWrites)
        {
            yield return new ModbusWriteFrame(entry.Address, entry.Data);
        }
    }

    public IEnumerable<IWriteFrame<byte>> BuildOptimized()
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

/// <summary>
/// 字节值设置器 - 用于 Fluent API
/// </summary>
internal sealed class ByteValueSetter<TProtocol>(
    IDataMapping<TProtocol, byte> mapping,
    ushort address,
    IProtocolCodec<TProtocol, byte> codec) : IValueSetter<TProtocol, byte>
    where TProtocol : struct
{
    public IDataMapping<TProtocol, byte> Value(byte value)
    {
        mapping.AddData(address, value);
        return mapping;
    }

    public IDataMapping<TProtocol, byte> Value<TValue>(TValue value) where TValue : unmanaged
    {
        var data = codec.EncodeValue(value);
        foreach (var b in data)
        {
            mapping.AddData(address, b);
        }
        return mapping;
    }
}
