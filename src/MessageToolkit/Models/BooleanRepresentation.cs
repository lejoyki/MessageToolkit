namespace MessageToolkit.Models;

/// <summary>
/// 布尔类型表示方式
/// </summary>
public enum BooleanRepresentation
{
    /// <summary>
    /// Boolean 类型原生表示 (1字节)
    /// </summary>
    Boolean = 1,

    /// <summary>
    /// 使用 Int16 表示布尔值 (2字节)
    /// </summary>
    Int16 = 2,

    /// <summary>
    /// 使用 Int32 表示布尔值 (4字节)
    /// </summary>
    Int32 = 4,
}

