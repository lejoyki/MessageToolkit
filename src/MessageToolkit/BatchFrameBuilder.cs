using System.Linq;
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
    private readonly List<(ushort Address, byte[] Data)> _pendingWrites = [];

    public BatchFrameBuilder(IFrameBuilder<TProtocol> frameBuilder)
    {
        _frameBuilder = frameBuilder;
    }

    public IBatchFrameBuilder<TProtocol> Write<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector, TValue value) where TValue : unmanaged
    {
        var address = _frameBuilder.Schema.GetAddress(fieldSelector);
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add((address, data));
        return this;
    }

    public IBatchFrameBuilder<TProtocol> Write<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add((address, data));
        return this;
    }

    public FrameCollection Build()
    {
        var collection = new FrameCollection();
        foreach (var (address, data) in _pendingWrites)
        {
            collection.Add(new ModbusFrame
            {
                FunctionCode = ModbusFunctionCode.WriteMultipleRegisters,
                StartAddress = address,
                Data = data
            });
        }

        return collection;
    }

    public FrameCollection BuildOptimized()
    {
        var ordered = _pendingWrites.OrderBy(x => x.Address).ToList();
        var collection = new FrameCollection();

        var index = 0;
        while (index < ordered.Count)
        {
            var (startAddr, startData) = ordered[index];
            var combinedData = new List<byte>(startData);
            var nextStart = startAddr + startData.Length;
            var nextIndex = index + 1;

            while (nextIndex < ordered.Count && ordered[nextIndex].Address == nextStart)
            {
                combinedData.AddRange(ordered[nextIndex].Data);
                nextStart = (ushort)(ordered[nextIndex].Address + ordered[nextIndex].Data.Length);
                nextIndex++;
            }

            collection.Add(new ModbusFrame
            {
                FunctionCode = ModbusFunctionCode.WriteMultipleRegisters,
                StartAddress = startAddr,
                Data = combinedData.ToArray()
            });

            index = nextIndex;
        }

        return collection;
    }
}

