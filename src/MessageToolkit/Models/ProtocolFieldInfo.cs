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
    /// 地址
    /// </summary>
    public required ushort Address { get; init; }

    /// <summary>
    /// 字节地址（兼容属性，等同于 Address）
    /// </summary>
    public ushort ByteAddress => Address;

    /// <summary>
    /// 寄存器地址（Address / 2）
    /// </summary>
    public ushort RegisterAddress => (ushort)(Address / 2);

    /// <summary>
    /// 字段大小（字节）
    /// </summary>
    public required int Size { get; init; }

    /// <summary>
    /// 是否为布尔类型
    /// </summary>
    public bool IsBoolean => FieldType == typeof(bool);
}

