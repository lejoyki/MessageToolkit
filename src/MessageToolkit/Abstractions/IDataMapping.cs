using System.Linq.Expressions;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 泛型数据映射接口 - 支持链式写入
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IDataMapping<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 添加数据项
    /// </summary>
    void AddData(int address, TData data);

    /// <summary>
    /// 添加字段写入
    /// </summary>
    IDataMapping<TProtocol, TData> Property<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 添加地址写入
    /// </summary>
    IDataMapping<TProtocol, TData> Property<TValue>(
        ushort address,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 设置属性值（链式调用）- Fluent API 风格
    /// </summary>
    IValueSetter<TProtocol, TData> Property<TValue>(
        Expression<Func<TProtocol, TValue>> propertyExpression);

    /// <summary>
    /// 设置地址值（链式调用）- Fluent API 风格
    /// </summary>
    IValueSetter<TProtocol, TData> Property(int address);

    /// <summary>
    /// 构建帧集合
    /// </summary>
    IEnumerable<IWriteFrame<TData>> Build();

    /// <summary>
    /// 构建并优化（合并连续地址）
    /// </summary>
    IEnumerable<IWriteFrame<TData>> BuildOptimized();

    /// <summary>
    /// 清空已添加的数据
    /// </summary>
    void Clear();

    /// <summary>
    /// 已添加的数据项数量
    /// </summary>
    int Count { get; }
}
