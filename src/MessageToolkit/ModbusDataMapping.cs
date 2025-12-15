using MessageToolkit.Abstractions;
using MessageToolkit.Models;
using System.Linq.Expressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MessageToolkit;

/// <summary>
/// Modbus 数据映射 - 用于字节协议的批量写入构建
/// </summary>
public class ModbusDataMapping<TProtocol> : IModbusDataMapping<TProtocol>
    where TProtocol : struct
{
    protected readonly IProtocolSchema<TProtocol> _schema;
    protected readonly IModbusProtocolCodec<TProtocol> _codec;
    protected readonly List<WriteEntry> _pendingWrites;

    /// <summary>
    /// 已添加的数据项数量
    /// </summary>
    public int Count => _pendingWrites.Count;

    /// <summary>
    /// 创建 Modbus 数据映射
    /// </summary>
    /// <param name="schema">协议模式</param>
    /// <param name="codec">字节编解码器</param>
    public ModbusDataMapping(IProtocolSchema<TProtocol> schema, IModbusProtocolCodec<TProtocol> codec)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _pendingWrites = new List<WriteEntry>(16);
    }

    /// <summary>
    /// 添加字段写入（链式调用）
    /// </summary>
    public IModbusDataMapping<TProtocol> Write<TValue>(
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
    public IModbusDataMapping<TProtocol> Write<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _codec.EncodeValue(value);
        _pendingWrites.Add(new WriteEntry(address, data));
        return this;
    }

    public IModbusDataMapping<TProtocol> WriteRaw(ushort address, byte[] value)
    {
        _pendingWrites.Add(new WriteEntry(address, value));
        return this;
    }

    /// <summary>
    /// 构建帧集合（每个写入操作生成独立帧）
    /// </summary>
    public IEnumerable<ModbusWriteFrame> Build()
    {
        foreach (var entry in _pendingWrites)
        {
            yield return new ModbusWriteFrame(entry.Address, entry.Data);
        }
    }

    /// <summary>
    /// 构建并优化（合并连续地址）
    /// </summary>
    public IEnumerable<ModbusWriteFrame> BuildOptimized()
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

            // 查找可合并的连续地址
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
                // 单个写入，直接返回
                yield return new ModbusWriteFrame(startAddr, startEntry.Data);
            }
            else
            {
                // 合并多个写入
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

    /// <summary>
    /// 清空已添加的数据
    /// </summary>
    public void Clear()
    {
        _pendingWrites.Clear();
    }

    protected readonly struct WriteEntry(ushort address, byte[] data)
    {
        public ushort Address { get; } = address;
        public byte[] Data { get; } = data;
    }
}
