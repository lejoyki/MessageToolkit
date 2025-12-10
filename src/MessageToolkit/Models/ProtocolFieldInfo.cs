namespace MessageToolkit.Models;

/// <summary>
/// 协议字段信息
/// </summary>
public sealed class ProtocolFieldInfo
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 属性类型
    /// </summary>
    public required Type FieldType { get; init; }

    /// <summary>
    /// 字节地址
    /// </summary>
    public required ushort ByteAddress { get; init; }

    /// <summary>
    /// 寄存器地址
    /// </summary>
    public ushort RegisterAddress => (ushort)(ByteAddress / 2);

    /// <summary>
    /// 字段大小（字节）
    /// </summary>
    public required int Size { get; init; }

    /// <summary>
    /// 是否为布尔类型
    /// </summary>
    public bool IsBoolean => FieldType == typeof(bool);
}

