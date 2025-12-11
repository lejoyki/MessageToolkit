namespace MessageToolkit.Abstractions;

/// <summary>
/// 读取请求接口
/// </summary>
public interface IReadFrame : IFrame
{
    /// <summary>
    /// 请求读取的元素数量
    /// </summary>
    int Count { get; }
}
