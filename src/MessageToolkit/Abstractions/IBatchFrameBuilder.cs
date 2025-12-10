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
    /// 添加原始字节写入
    /// </summary>
    IBatchFrameBuilder<TProtocol> WriteRaw(ushort address, ReadOnlySpan<byte> data);

    /// <summary>
    /// 构建所有帧（不优化，每个写入操作生成一个帧）
    /// </summary>
    WriteFrameCollection Build();

    /// <summary>
    /// 构建并优化帧（合并连续地址，减少通信次数）
    /// </summary>
    WriteFrameCollection BuildOptimized();

    /// <summary>
    /// 清空已添加的写入操作
    /// </summary>
    void Clear();

    /// <summary>
    /// 已添加的写入操作数量
    /// </summary>
    int Count { get; }
}
