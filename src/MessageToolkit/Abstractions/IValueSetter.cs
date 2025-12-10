namespace MessageToolkit.Abstractions;

/// <summary>
/// 值设置器接口 - 用于链式 API
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IValueSetter<TProtocol, TData>
    where TProtocol : struct
{
    /// <summary>
    /// 设置单个数据值并返回映射对象（链式调用）
    /// </summary>
    IDataMapping<TProtocol, TData> Value(TData value);

    /// <summary>
    /// 设置泛型值并返回映射对象（链式调用）
    /// </summary>
    IDataMapping<TProtocol, TData> Value<TValue>(TValue value) where TValue : unmanaged;
}
