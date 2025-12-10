namespace MessageToolkit.Attributes;

/// <summary>
/// 地址特性 - 标记协议字段的 Modbus 地址
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AddressAttribute(ushort byteAddress) : Attribute
{
    /// <summary>
    /// 字节地址
    /// </summary>
    public ushort ByteAddress { get; } = byteAddress;

    /// <summary>
    /// 寄存器地址
    /// </summary>
    public ushort RegisterAddress => (ushort)(ByteAddress / 2);
}

