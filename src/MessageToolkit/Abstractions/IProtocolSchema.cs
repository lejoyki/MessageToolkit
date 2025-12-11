using System.Linq.Expressions;
using MessageToolkit.Models;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 协议模式接口 - 描述协议的结构信息
/// </summary>
public interface IProtocolSchema<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议起始地址
    /// </summary>
    int StartAddress { get; }

    /// <summary>
    /// 协议总大小（元素数量）
    /// </summary>
    int TotalSize { get; }

    /// <summary>
    /// 布尔类型在协议中的表示方式
    /// </summary>
    BooleanRepresentation BooleanType { get; }

    /// <summary>
    /// 字节序
    /// </summary>
    Endianness Endianness { get; }

    /// <summary>
    /// 所有属性信息
    /// </summary>
    IReadOnlyDictionary<string, ProtocolFieldInfo> Properties { get; }

    /// <summary>
    /// 根据字段名获取地址
    /// </summary>
    ushort GetAddress(string fieldName);

    /// <summary>
    /// 根据表达式获取地址
    /// </summary>
    ushort GetAddress<TValue>(Expression<Func<TProtocol, TValue>> expression);

    /// <summary>
    /// 获取字段信息
    /// </summary>
    ProtocolFieldInfo GetFieldInfo(string fieldName);
}

