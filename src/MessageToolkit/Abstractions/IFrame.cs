namespace MessageToolkit.Abstractions;

/// <summary>
/// 帧抽象基接口 - 表示一个可传输的数据单元
/// </summary>
public interface IFrame
{
    /// <summary>
    /// 起始地址
    /// </summary>
    int StartAddress { get; }
}

/// <summary>
/// 泛型帧接口 - 携带特定类型的数据载荷
/// </summary>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IFrame<TData> : IFrame
{
    /// <summary>
    /// 数据载荷
    /// </summary>
    TData[] Data { get; }

    /// <summary>
    /// 数据长度
    /// </summary>
    int DataLength { get; }
}
