using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 位数据映射实现 - 用于 IO 点位的链式写入
/// </summary>
public sealed class BitDataMapping<TProtocol>(IProtocolSchema<TProtocol> schema) : IDataMapping<TProtocol,bool>
    where TProtocol : struct
{
    private readonly IProtocolSchema<TProtocol> _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    private readonly Dictionary<int, bool> _data = [];

    public int Count => _data.Count;

    public IDataMapping<TProtocol,bool> Property<TValue>(Expression<Func<TProtocol, TValue>> fieldSelector, TValue value) where TValue : unmanaged
    {
        ushort address = _schema.GetAddress(fieldSelector);
        if(value is not bool boolValue)
        {
            throw new ArgumentException("Only boolean values are supported for bit data mapping.");
        }
        _data[address] = boolValue;
        return this;
    }

    public IDataMapping<TProtocol,bool> Property<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        if(value is not bool boolValue)
        {
            throw new ArgumentException("Only boolean values are supported for bit data mapping.");
        }
        _data[address] = boolValue;
        return this;
    }

    public IEnumerable<IFrame<bool>> Build()
    {
        foreach (var kvp in _data)
        {
            yield return new BitWriteFrame(kvp.Key, kvp.Value);
        }
    }

    public IEnumerable<IFrame<bool>> BuildOptimized()
    {
        if (_data.Count == 0)
        {
            yield break;
        }

        var sortedEntries = _data.OrderBy(kvp => kvp.Key).ToArray();
        var index = 0;

        while (index < sortedEntries.Length)
        {
            var startAddr = sortedEntries[index].Key;
            var mergeCount = 1;

            // 查找连续地址
            for (var i = index + 1; i < sortedEntries.Length; i++)
            {
                if (sortedEntries[i].Key != startAddr + mergeCount)
                    break;
                mergeCount++;
            }

            // 创建帧
            var data = new bool[mergeCount];
            for (var i = 0; i < mergeCount; i++)
            {
                data[i] = sortedEntries[index + i].Value;
            }

            yield return new BitWriteFrame(startAddr, data);
            index += mergeCount;
        }
    }

    public void Clear()
    {
        _data.Clear();
    }
}
