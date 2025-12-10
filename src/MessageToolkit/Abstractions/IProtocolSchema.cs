using System.Linq.Expressions;
using MessageToolkit.Models;

namespace MessageToolkit.Abstractions;

/// <summary>
/// 协议模式接口 - 描述协议的结构信息
/// </summary>
public interface IProtocolSchema<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议起始字节地址
    /// </summary>
    int StartAddress { get; }

    /// <summary>
    /// 协议起始寄存器地址
    /// </summary>
    int StartRegisterAddress => StartAddress / 2;

    /// <summary>
    /// 协议总大小（字节）
    /// </summary>
    int TotalSize { get; }

    /// <summary>
    /// 寄存器数量
    /// </summary>
    int RegisterCount => TotalSize / 2;

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
    /// 布尔属性地址映射（仅包含类型为 bool 的字段/属性）
    /// 键为属性名，值为字节地址
    /// </summary>
    IReadOnlyDictionary<string, ushort> BooleanProperties { get; }

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

