using System.Linq.Expressions;
using MessageToolkit.Models;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 批量帧构建器 - 支持链式调用构建多个写入操作
/// </summary>
public interface IBatchFrameBuilder<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 添加字段写入
    /// </summary>
    IBatchFrameBuilder<TProtocol> Write<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 添加地址写入
    /// </summary>
    IBatchFrameBuilder<TProtocol> Write<TValue>(
        ushort address,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 构建所有帧（不优化）
    /// </summary>
    FrameCollection Build();

    /// <summary>
    /// 构建并优化帧（合并连续地址）
    /// </summary>
    FrameCollection BuildOptimized();
}

