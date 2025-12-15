using System.Linq.Expressions;
using MessageToolkit.Models;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 原生协议数据映射接口 - 用于原生类型的批量写入
/// </summary>
/// <typeparam name="TProtocol">协议结构体类型</typeparam>
/// <typeparam name="TData">原生数据类型</typeparam>
public interface INativeDataMapping<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 已添加的数据项数量
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 添加字段写入（链式调用）
    /// </summary>
    INativeDataMapping<TProtocol, TData> Write(Expression<Func<TProtocol, TData>> fieldSelector, TData value);

    /// <summary>
    /// 添加地址写入（链式调用）
    /// </summary>
    INativeDataMapping<TProtocol, TData> Write(ushort address, TData value);

    PropertyValueSetter<TProtocol, TData> Property(ushort address);

    PropertyValueSetter<TProtocol, TData> Property(Expression<Func<TProtocol, TData>> fieldSelector);

    /// <summary>
    /// 构建帧集合（每个写入操作生成独立帧）
    /// </summary>
    IEnumerable<WriteFrame<TData>> Build();

    /// <summary>
    /// 构建并优化（合并连续地址）
    /// </summary>
    IEnumerable<WriteFrame<TData>> BuildOptimized();

    /// <summary>
    /// 清空已添加的数据
    /// </summary>
    void Clear();
}
