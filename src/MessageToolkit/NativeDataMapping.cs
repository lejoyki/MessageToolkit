using System.Linq.Expressions;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 原生协议数据映射 - 用于原生类型的批量写入构建
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
/// <typeparam name="TData">原生数据类型</typeparam>
public class NativeDataMapping<TProtocol, TData> : INativeDataMapping<TProtocol, TData>
    where TProtocol : struct
{
    protected readonly IProtocolSchema<TProtocol> _schema;
    protected readonly Dictionary<int, TData> _data;

    /// <summary>
    /// 已添加的数据项数量
    /// </summary>
    public int Count => _data.Count;

    /// <summary>
    /// 创建原生数据映射
    /// </summary>
    /// <param name="schema">协议模式</param>
    public NativeDataMapping(IProtocolSchema<TProtocol> schema)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _data = [];
    }

    /// <summary>
    /// 添加字段写入（链式调用）
    /// </summary>
    public INativeDataMapping<TProtocol, TData> Write(Expression<Func<TProtocol, TData>> fieldSelector,TData value)
    {
        var address = _schema.GetAddress(fieldSelector);
        _data[address] = value;
        return this;
    }

    /// <summary>
    /// 添加地址写入（链式调用）
    /// </summary>
    public INativeDataMapping<TProtocol, TData> Write(ushort address, TData value)
    {
        _data[address] = value;
        return this;
    }

    public PropertyValueSetter<TProtocol, TData> Property(ushort address)
    {
        return new PropertyValueSetter<TProtocol, TData>(this, address);
    }

    public PropertyValueSetter<TProtocol, TData> Property(Expression<Func<TProtocol, TData>> fieldSelector)
    {
        var address = _schema.GetAddress(fieldSelector);
        return new PropertyValueSetter<TProtocol, TData>(this, address);
    }

    /// <summary>
    /// 构建帧集合（每个写入操作生成独立帧）
    /// </summary>
    public IEnumerable<WriteFrame<TData>> Build()
    {
        foreach (var kvp in _data)
        {
            yield return new WriteFrame<TData>(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// 构建并优化（合并连续地址）
    /// </summary>
    public IEnumerable<WriteFrame<TData>> BuildOptimized()
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
            var data = new TData[mergeCount];
            for (var i = 0; i < mergeCount; i++)
            {
                data[i] = sortedEntries[index + i].Value;
            }

            yield return new WriteFrame<TData>(startAddr, data);
            index += mergeCount;
        }
    }

    /// <summary>
    /// 清空已添加的数据
    /// </summary>
    public void Clear()
    {
        _data.Clear();
    }
}
