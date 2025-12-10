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
    public IReadOnlyDictionary<string, ProtocolFieldInfo> Fields { get; }

    public ProtocolSchema(
        BooleanRepresentation booleanType = BooleanRepresentation.Int16,
        Endianness endianness = Endianness.BigEndian)
    {
        BooleanType = booleanType;
        Endianness = endianness;
        Fields = BuildFieldMapping(out var startAddress, out var totalSize);

        if (Fields.Count == 0)
        {
            throw new InvalidOperationException("协议中未找到任何带有 AddressAttribute 的字段或属性");
        }

        StartAddress = startAddress;
        TotalSize = totalSize;
    }

    public ushort GetAddress(string fieldName)
    {
        if (Fields.TryGetValue(fieldName, out var info))
        {
            return info.ByteAddress;
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
        if (Fields.TryGetValue(fieldName, out var info))
        {
            return info;
        }

        throw new ArgumentException($"找不到字段 {fieldName} 的定义");
    }

    private IReadOnlyDictionary<string, ProtocolFieldInfo> BuildFieldMapping(out int startAddress, out int totalSize)
    {
        var mapping = new Dictionary<string, ProtocolFieldInfo>(StringComparer.Ordinal);
        startAddress = int.MaxValue;
        var maxEndAddress = 0;

        var members = typeof(TProtocol).GetMembers(BindingFlags.Instance | BindingFlags.Public);
        foreach (var member in members)
        {
            if (member is not FieldInfo and not PropertyInfo)
            {
                continue;
            }

            var attribute = member.GetCustomAttribute<AddressAttribute>();
            if (attribute == null)
            {
                continue;
            }

            var fieldType = GetMemberType(member);
            var size = GetFieldSize(fieldType);
            var byteAddress = attribute.ByteAddress;

            mapping[member.Name] = new ProtocolFieldInfo
            {
                Name = member.Name,
                FieldType = fieldType,
                ByteAddress = byteAddress,
                Size = size
            };

            startAddress = Math.Min(startAddress, byteAddress);
            maxEndAddress = Math.Max(maxEndAddress, byteAddress + size);
        }

        if (startAddress == int.MaxValue)
        {
            startAddress = 0;
        }

        totalSize = Math.Max(0, maxEndAddress - startAddress);
        return mapping;
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

    private static Type GetMemberType(MemberInfo member) =>
        member switch
        {
            FieldInfo fieldInfo => fieldInfo.FieldType,
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            _ => throw new ArgumentException("不支持的成员类型")
        };

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

