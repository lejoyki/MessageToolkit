using System.Collections.Frozen;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using MessageToolkit.Abstractions;
using MessageToolkit.Attributes;
using MessageToolkit.Models;

namespace MessageToolkit;

/// <summary>
/// 协议模式实现 - 通过反射分析协议结构
/// </summary>
public sealed class ProtocolSchema<TProtocol> : IProtocolSchema<TProtocol>
    where TProtocol : struct
{
    public int StartAddress { get; }
    public int TotalSize { get; }
    public BooleanRepresentation BooleanType { get; }
    public Endianness Endianness { get; }
    public IReadOnlyDictionary<string, ProtocolFieldInfo> Properties { get; }
    public IReadOnlyDictionary<string, ushort> BooleanProperties { get; }

    public ProtocolSchema(
        BooleanRepresentation booleanType = BooleanRepresentation.Int16,
        Endianness endianness = Endianness.BigEndian)
    {
        BooleanType = booleanType;
        Endianness = endianness;
        var mapping = BuildFieldMapping();
        Properties = mapping.Fields;
        BooleanProperties = mapping.BooleanFields;

        if (Properties.Count == 0)
        {
            throw new InvalidOperationException("协议中未找到任何带有 AddressAttribute 的字段或属性");
        }

        StartAddress = mapping.StartAddress;
        TotalSize = mapping.TotalSize;
    }

    public ushort GetAddress(string fieldName)
    {
        if (Properties.TryGetValue(fieldName, out var info))
        {
            return info.Address;
        }

        throw new ArgumentException($"找不到字段 {fieldName} 的地址映射");
    }

    public ushort GetAddress<TValue>(Expression<Func<TProtocol, TValue>> expression)
    {
        var memberName = GetMemberName(expression);
        return GetAddress(memberName);
    }

    public ProtocolFieldInfo GetFieldInfo(string fieldName)
    {
        if (Properties.TryGetValue(fieldName, out var info))
        {
            return info;
        }

        throw new ArgumentException($"找不到字段 {fieldName} 的定义");
    }

    private (IReadOnlyDictionary<string, ProtocolFieldInfo> Fields,
        IReadOnlyDictionary<string, ushort> BooleanFields,
        int StartAddress,
        int TotalSize) BuildFieldMapping()
    {
        var mapping = new Dictionary<string, ProtocolFieldInfo>(StringComparer.Ordinal);
        var booleanMapping = new Dictionary<string, ushort>(StringComparer.Ordinal);
        var startAddress = int.MaxValue;
        var maxEndAddress = 0;

        var members = typeof(TProtocol).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var propertyInfo in members)
        {
            var attribute = propertyInfo.GetCustomAttribute<AddressAttribute>();
            if (attribute == null)
            {
                continue;
            }

            var fieldType = propertyInfo.PropertyType;
            var size = GetFieldSize(fieldType);
            var address = attribute.Address;

            var fieldInfo = new ProtocolFieldInfo
            {
                Name = propertyInfo.Name,
                FieldType = fieldType,
                Address = address,
                Size = size
            };

            mapping[propertyInfo.Name] = fieldInfo;

            if (fieldType == typeof(bool))
            {
                booleanMapping[propertyInfo.Name] = address;
            }

            startAddress = Math.Min(startAddress, address);
            maxEndAddress = Math.Max(maxEndAddress, address + size);
        }

        if (startAddress == int.MaxValue)
        {
            startAddress = 0;
        }

        var totalSize = Math.Max(0, maxEndAddress - startAddress);
        return (
            mapping.ToFrozenDictionary(),
            booleanMapping.ToFrozenDictionary(),
            startAddress,
            totalSize);
    }

    private int GetFieldSize(Type type)
    {
        if (type == typeof(bool))
        {
            return BooleanType == BooleanRepresentation.Int32 ? 4 : 2;
        }

        if (type.IsEnum)
        {
            return Marshal.SizeOf(Enum.GetUnderlyingType(type));
        }

        return Marshal.SizeOf(type);
    }

    private static string GetMemberName<TValue>(Expression<Func<TProtocol, TValue>> expression)
    {
        if (expression.Body is MemberExpression memberExpression)
        {
            return memberExpression.Member.Name;
        }

        if (expression.Body is UnaryExpression { Operand: MemberExpression unaryMember })
        {
            return unaryMember.Member.Name;
        }

        throw new ArgumentException("无效的字段访问表达式");
    }
}

