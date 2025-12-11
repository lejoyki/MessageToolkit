namespace MessageToolkit.Attributes;

/// <summary>
/// 地址特性 - 标记协议字段的地址
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AddressAttribute(ushort address) : Attribute
{
    /// <summary>
    /// 地址
    /// </summary>
    public ushort Address { get; } = address;
}

