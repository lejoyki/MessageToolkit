using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 位数据映射实现 - 用于 IO 点位的链式写入
/// </summary>
public sealed class BitDataMapping<TProtocol> : IDataMapping<TProtocol, bool>
    where TProtocol : struct
{
    private readonly IProtocolSchema<TProtocol> _schema;
    private readonly Dictionary<int, bool> _data = new();

    public int Count => _data.Count;

    public BitDataMapping(IProtocolSchema<TProtocol> schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public void AddData(int address, bool data)
    {
        _data[address] = data;
    }

    public IDataMapping<TProtocol, bool> Property<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = _schema.GetAddress(fieldSelector);
        if (value is bool b)
        {
            _data[address] = b;
        }
        else
        {
            throw new NotSupportedException($"BitDataMapping 仅支持 bool 类型值");
        }
        return this;
    }

    public IDataMapping<TProtocol, bool> Property<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        if (value is bool b)
        {
            _data[address] = b;
        }
        else
        {
            throw new NotSupportedException($"BitDataMapping 仅支持 bool 类型值");
        }
        return this;
    }

    public IValueSetter<TProtocol, bool> Property<TValue>(
        Expression<Func<TProtocol, TValue>> propertyExpression)
    {
        var address = _schema.GetAddress(propertyExpression);
        return new BitValueSetter<TProtocol>(this, address);
    }

    public IValueSetter<TProtocol, bool> Property(int address)
    {
        return new BitValueSetter<TProtocol>(this, address);
    }

    public IEnumerable<IWriteFrame<bool>> Build()
    {
        foreach (var kvp in _data)
        {
            yield return new BitWriteFrame(kvp.Key, kvp.Value);
        }
    }

    public IEnumerable<IWriteFrame<bool>> BuildOptimized()
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

/// <summary>
/// 位值设置器实现
/// </summary>
internal sealed class BitValueSetter<TProtocol> : IValueSetter<TProtocol, bool>
    where TProtocol : struct
{
    private readonly IDataMapping<TProtocol, bool> _mapping;
    private readonly int _address;

    public BitValueSetter(IDataMapping<TProtocol, bool> mapping, int address)
    {
        _mapping = mapping;
        _address = address;
    }

    public IDataMapping<TProtocol, bool> Value(bool value)
    {
        _mapping.AddData(_address, value);
        return _mapping;
    }

    public IDataMapping<TProtocol, bool> Value<TValue>(TValue value) where TValue : unmanaged
    {
        if (value is bool b)
        {
            _mapping.AddData(_address, b);
            return _mapping;
        }
        throw new NotSupportedException($"BitValueSetter 仅支持 bool 类型值");
    }
}
