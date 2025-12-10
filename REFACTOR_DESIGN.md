# MessageToolkit 重构设计文档

## 1. 概述

### 1.1 重构背景

当前 `MessageToolkit` 类库依赖于 `ProjectLibrary.Communication` 包来进行 Modbus 通信。本次重构的目标是：

- **移除通信依赖**：不再依赖 `ModbusClient`，不执行实际的数据读写操作
- **专注帧构建**：仅负责根据协议构建 Modbus 帧信息（包含起始地址和数据）
- **保持序列化能力**：继续支持协议的序列化和反序列化功能

### 1.2 现有架构分析

```
当前依赖关系：
┌─────────────────────────────────────────────────────────────┐
│                    MessageToolkit                           │
├─────────────────────────────────────────────────────────────┤
│  IMessageBuilder<T>                                         │
│    └── ModbusMessageBuilder<T>                              │
│          ├── FluentModbusClient (外部依赖 ❌)               │
│          ├── IProtocolConfiguration<T>                      │
│          └── IProtocolSerialize<T>                          │
│                                                             │
│  IProtocolDataMapping<T>                                    │
│    └── ProtocolDataMapping<T>                               │
│          └── IMessageBuilder<T> (写入数据)                  │
│                                                             │
│  依赖: ProjectLibrary.Communication (ByteUnit, etc.) ❌      │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 现有问题

| 问题 | 描述 |
|------|------|
| 紧耦合 | 类库与通信层紧密耦合，无法独立使用 |
| 职责混乱 | 同时负责协议处理和通信操作 |
| 外部依赖 | 依赖 `ProjectLibrary.Communication` 包 |
| 测试困难 | 需要真实的 Modbus 连接才能测试 |

---

## 2. 重构目标架构

### 2.1 新架构设计

```
重构后架构：
┌─────────────────────────────────────────────────────────────┐
│                    MessageToolkit                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐    ┌─────────────────────────────┐    │
│  │  协议配置层     │    │        帧构建层              │    │
│  ├─────────────────┤    ├─────────────────────────────┤    │
│  │ IProtocolSchema │    │ IFrameBuilder<T>            │    │
│  │ ProtocolSchema  │    │ ModbusFrameBuilder<T>       │    │
│  │ AddressAttribute│    │                             │    │
│  └─────────────────┘    └─────────────────────────────┘    │
│           │                          │                      │
│           ▼                          ▼                      │
│  ┌─────────────────┐    ┌─────────────────────────────┐    │
│  │  序列化层       │    │        模型层               │    │
│  ├─────────────────┤    ├─────────────────────────────┤    │
│  │ IProtocolCodec  │    │ ModbusFrame                 │    │
│  │ ProtocolCodec   │    │ FrameCollection             │    │
│  │ ByteConverter   │    │                             │    │
│  └─────────────────┘    └─────────────────────────────┘    │
│                                                             │
│  零外部依赖 ✅                                              │
└─────────────────────────────────────────────────────────────┘
```

### 2.2 核心设计原则

1. **单一职责**：只负责协议解析和帧构建，不负责通信
2. **零依赖**：不依赖任何外部通信库
3. **输出驱动**：输出标准的 Modbus 帧信息，供外部通信层使用
4. **类型安全**：利用泛型和强类型保证类型安全

---

## 3. 详细设计

### 3.1 核心模型

#### 3.1.1 ModbusFrame - Modbus 帧模型

```csharp
namespace MessageToolkit.Models;

/// <summary>
/// Modbus 帧信息
/// </summary>
public sealed class ModbusFrame
{
    /// <summary>
    /// 起始地址（字节地址）
    /// </summary>
    public ushort StartAddress { get; init; }
    
    /// <summary>
    /// 寄存器地址（StartAddress / 2）
    /// </summary>
    public ushort RegisterAddress => (ushort)(StartAddress / 2);
    
    /// <summary>
    /// 寄存器数量
    /// </summary>
    public ushort RegisterCount => (ushort)(Data.Length / 2);
    
    /// <summary>
    /// 数据载荷
    /// </summary>
    public required byte[] Data { get; init; }
    
    /// <summary>
    /// 数据长度（字节）
    /// </summary>
    public int DataLength => Data.Length;
}

/// <summary>
/// 帧集合，支持连续地址合并
/// </summary>
public sealed class FrameCollection : IEnumerable<ModbusFrame>
{
    private readonly List<ModbusFrame> _frames = [];
    
    /// <summary>
    /// 帧数量
    /// </summary>
    public int Count => _frames.Count;
    
    /// <summary>
    /// 添加帧
    /// </summary>
    public void Add(ModbusFrame frame) => _frames.Add(frame);
    
    /// <summary>
    /// 合并连续地址的帧
    /// </summary>
    public FrameCollection Optimize();
    
    /// <summary>
    /// 获取所有帧
    /// </summary>
    public IReadOnlyList<ModbusFrame> Frames => _frames.AsReadOnly();
    
    public IEnumerator<ModbusFrame> GetEnumerator() => _frames.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
```

#### 3.1.2 ProtocolFieldInfo - 协议字段信息

```csharp
namespace MessageToolkit.Models;

/// <summary>
/// 协议字段信息
/// </summary>
public sealed class ProtocolFieldInfo
{
    /// <summary>
    /// 字段名称
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// 字段类型
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
```

---

### 3.2 协议配置层

#### 3.2.1 IProtocolSchema - 协议模式接口

```csharp
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
    /// 所有字段信息
    /// </summary>
    IReadOnlyDictionary<string, ProtocolFieldInfo> Fields { get; }
    
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

/// <summary>
/// 布尔类型表示方式
/// </summary>
public enum BooleanRepresentation
{
    /// <summary>
    /// 使用 Int16 表示布尔值 (2字节)
    /// </summary>
    Int16,
    
    /// <summary>
    /// 使用 Int32 表示布尔值 (4字节)
    /// </summary>
    Int32
}

/// <summary>
/// 字节序
/// </summary>
public enum Endianness
{
    /// <summary>
    /// 小端序 (Little Endian)
    /// </summary>
    LittleEndian,
    
    /// <summary>
    /// 大端序 (Big Endian)
    /// </summary>
    BigEndian
}
```

#### 3.2.2 ProtocolSchema - 协议模式实现

```csharp
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
        Fields = BuildFieldMapping();
        (StartAddress, TotalSize) = CalculateAddressRange();
    }
    
    // ... 实现细节
}
```

#### 3.2.3 AddressAttribute - 地址特性（保持兼容）

```csharp
namespace MessageToolkit.Attributes;

/// <summary>
/// 地址特性 - 标记协议字段的 Modbus 地址
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AddressAttribute : Attribute
{
    /// <summary>
    /// 字节地址
    /// </summary>
    public ushort ByteAddress { get; }
    
    /// <summary>
    /// 寄存器地址
    /// </summary>
    public ushort RegisterAddress => (ushort)(ByteAddress / 2);
    
    public AddressAttribute(ushort byteAddress)
    {
        ByteAddress = byteAddress;
    }
}
```

---

### 3.3 序列化层

#### 3.3.1 IProtocolCodec - 协议编解码器接口

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 协议编解码器接口
/// </summary>
public interface IProtocolCodec<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }
    
    /// <summary>
    /// 序列化整个协议
    /// </summary>
    byte[] Encode(TProtocol protocol);
    
    /// <summary>
    /// 反序列化整个协议
    /// </summary>
    TProtocol Decode(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// 序列化单个字段值
    /// </summary>
    byte[] EncodeValue<TValue>(TValue value) where TValue : unmanaged;
    
    /// <summary>
    /// 反序列化单个字段值
    /// </summary>
    TValue DecodeValue<TValue>(ReadOnlySpan<byte> data) where TValue : unmanaged;
    
    /// <summary>
    /// 获取字段的序列化字节
    /// </summary>
    byte[] EncodeField<TValue>(TProtocol protocol, Expression<Func<TProtocol, TValue>> fieldSelector);
}
```

#### 3.3.2 ProtocolCodec - 协议编解码器实现

```csharp
namespace MessageToolkit;

/// <summary>
/// 协议编解码器实现
/// </summary>
public sealed class ProtocolCodec<TProtocol> : IProtocolCodec<TProtocol> 
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    
    public ProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema;
    }
    
    public byte[] Encode(TProtocol protocol)
    {
        var buffer = new byte[Schema.TotalSize];
        // 遍历所有字段，序列化到对应位置
        foreach (var (name, fieldInfo) in Schema.Fields)
        {
            var value = GetFieldValue(protocol, name);
            var bytes = EncodeValueInternal(value, fieldInfo.FieldType);
            var offset = fieldInfo.ByteAddress - Schema.StartAddress;
            bytes.CopyTo(buffer.AsSpan(offset));
        }
        return buffer;
    }
    
    public TProtocol Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < Schema.TotalSize)
            throw new ArgumentException($"数据长度不足: 需要 {Schema.TotalSize} 字节, 实际 {data.Length} 字节");
            
        var result = new TProtocol();
        object boxed = result;
        
        foreach (var (name, fieldInfo) in Schema.Fields)
        {
            var offset = fieldInfo.ByteAddress - Schema.StartAddress;
            var valueBytes = data.Slice(offset, fieldInfo.Size);
            var value = DecodeValueInternal(valueBytes, fieldInfo.FieldType);
            SetFieldValue(boxed, name, value);
        }
        
        return (TProtocol)boxed;
    }
    
    // ... 其他实现
}
```

#### 3.3.3 ByteConverter - 字节转换工具（内置实现）

```csharp
namespace MessageToolkit.Internal;

/// <summary>
/// 字节转换工具 - 替代外部依赖
/// </summary>
internal static class ByteConverter
{
    public static byte[] GetBytes(short value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        return endianness == Endianness.BigEndian ? bytes.Reverse().ToArray() : bytes;
    }
    
    public static byte[] GetBytes(ushort value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        return endianness == Endianness.BigEndian ? bytes.Reverse().ToArray() : bytes;
    }
    
    public static byte[] GetBytes(int value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        if (endianness == Endianness.BigEndian)
        {
            // Modbus 大端序: 高字在前，每个字内部也是大端
            return [bytes[1], bytes[0], bytes[3], bytes[2]];
        }
        return bytes;
    }
    
    public static byte[] GetBytes(uint value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        if (endianness == Endianness.BigEndian)
        {
            return [bytes[1], bytes[0], bytes[3], bytes[2]];
        }
        return bytes;
    }
    
    public static byte[] GetBytes(float value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        if (endianness == Endianness.BigEndian)
        {
            return [bytes[1], bytes[0], bytes[3], bytes[2]];
        }
        return bytes;
    }
    
    public static short ToInt16(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            return (short)(bytes[0] << 8 | bytes[1]);
        }
        return BitConverter.ToInt16(bytes);
    }
    
    public static int ToInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            // Modbus 大端序解析
            Span<byte> temp = stackalloc byte[4];
            temp[0] = bytes[1];
            temp[1] = bytes[0];
            temp[2] = bytes[3];
            temp[3] = bytes[2];
            return BitConverter.ToInt32(temp);
        }
        return BitConverter.ToInt32(bytes);
    }
    
    public static float ToSingle(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            Span<byte> temp = stackalloc byte[4];
            temp[0] = bytes[1];
            temp[1] = bytes[0];
            temp[2] = bytes[3];
            temp[3] = bytes[2];
            return BitConverter.ToSingle(temp);
        }
        return BitConverter.ToSingle(bytes);
    }
    
    // ... 其他类型转换
}
```

---

### 3.4 帧构建层

#### 3.4.1 IFrameBuilder - 帧构建器接口

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// Modbus 帧构建器接口
/// </summary>
public interface IFrameBuilder<TProtocol> where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }
    
    /// <summary>
    /// 协议编解码器
    /// </summary>
    IProtocolCodec<TProtocol> Codec { get; }
    
    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    ModbusFrame BuildWriteFrame(TProtocol protocol);
    
    /// <summary>
    /// 构建写入单个字段的帧
    /// </summary>
    ModbusFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector, 
        TValue value) where TValue : unmanaged;
    
    /// <summary>
    /// 构建写入指定地址的帧
    /// </summary>
    ModbusFrame BuildWriteFrame<TValue>(
        ushort address, 
        TValue value) where TValue : unmanaged;
    
    /// <summary>
    /// 构建读取整个协议的帧信息
    /// </summary>
    ModbusFrame BuildReadFrame();
    
    /// <summary>
    /// 构建读取单个字段的帧信息
    /// </summary>
    ModbusFrame BuildReadFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged;
    
    /// <summary>
    /// 创建批量写入构建器
    /// </summary>
    IBatchFrameBuilder<TProtocol> CreateBatchBuilder();
}
```

#### 3.4.2 IBatchFrameBuilder - 批量帧构建器接口

```csharp
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
    /// 构建所有帧（不优化）
    /// </summary>
    FrameCollection Build();
    
    /// <summary>
    /// 构建并优化帧（合并连续地址）
    /// </summary>
    FrameCollection BuildOptimized();
}
```

#### 3.4.3 ModbusFrameBuilder - 帧构建器实现

```csharp
namespace MessageToolkit;

/// <summary>
/// Modbus 帧构建器实现
/// </summary>
public sealed class ModbusFrameBuilder<TProtocol> : IFrameBuilder<TProtocol> 
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    public IProtocolCodec<TProtocol> Codec { get; }
    
    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema;
        Codec = new ProtocolCodec<TProtocol>(schema);
    }
    
    public ModbusFrameBuilder(IProtocolSchema<TProtocol> schema, IProtocolCodec<TProtocol> codec)
    {
        Schema = schema;
        Codec = codec;
    }
    
    public ModbusFrame BuildWriteFrame(TProtocol protocol)
    {
        return new ModbusFrame
        {
            StartAddress = (ushort)Schema.StartAddress,
            Data = Codec.Encode(protocol)
        };
    }
    
    public ModbusFrame BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector, 
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return BuildWriteFrame(address, value);
    }
    
    public ModbusFrame BuildWriteFrame<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        return new ModbusFrame
        {
            StartAddress = address,
            Data = Codec.EncodeValue(value)
        };
    }
    
    public ModbusFrame BuildReadFrame()
    {
        return new ModbusFrame
        {
            StartAddress = (ushort)Schema.StartAddress,
            Data = [] // 读取帧不需要数据
        };
    }
    
    public IBatchFrameBuilder<TProtocol> CreateBatchBuilder()
    {
        return new BatchFrameBuilder<TProtocol>(this);
    }
}
```

#### 3.4.4 BatchFrameBuilder - 批量帧构建器实现

```csharp
namespace MessageToolkit;

/// <summary>
/// 批量帧构建器实现
/// </summary>
internal sealed class BatchFrameBuilder<TProtocol> : IBatchFrameBuilder<TProtocol> 
    where TProtocol : struct
{
    private readonly IFrameBuilder<TProtocol> _frameBuilder;
    private readonly List<(ushort Address, byte[] Data)> _pendingWrites = [];
    
    public BatchFrameBuilder(IFrameBuilder<TProtocol> frameBuilder)
    {
        _frameBuilder = frameBuilder;
    }
    
    public IBatchFrameBuilder<TProtocol> Write<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector, 
        TValue value) where TValue : unmanaged
    {
        var address = _frameBuilder.Schema.GetAddress(fieldSelector);
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add((address, data));
        return this;
    }
    
    public IBatchFrameBuilder<TProtocol> Write<TValue>(ushort address, TValue value) where TValue : unmanaged
    {
        var data = _frameBuilder.Codec.EncodeValue(value);
        _pendingWrites.Add((address, data));
        return this;
    }
    
    public FrameCollection Build()
    {
        var collection = new FrameCollection();
        foreach (var (address, data) in _pendingWrites)
        {
            collection.Add(new ModbusFrame
            {
                StartAddress = address,
                Data = data
            });
        }
        return collection;
    }
    
    public FrameCollection BuildOptimized()
    {
        // 按地址排序并合并连续地址
        var ordered = _pendingWrites.OrderBy(x => x.Address).ToList();
        var collection = new FrameCollection();
        
        int i = 0;
        while (i < ordered.Count)
        {
            var (startAddr, startData) = ordered[i];
            var combinedData = new List<byte>(startData);
            var endAddr = startAddr + startData.Length;
            
            int j = i + 1;
            while (j < ordered.Count && ordered[j].Address == endAddr)
            {
                combinedData.AddRange(ordered[j].Data);
                endAddr = ordered[j].Address + ordered[j].Data.Length;
                j++;
            }
            
            collection.Add(new ModbusFrame
            {
                StartAddress = startAddr,
                Data = [.. combinedData]
            });
            
            i = j;
        }
        
        return collection;
    }
}
```

---

### 3.5 依赖注入

```csharp
namespace MessageToolkit.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 MessageToolkit 服务
    /// </summary>
    public static IServiceCollection AddMessageToolkit(this IServiceCollection services)
    {
        services.AddSingleton(typeof(IProtocolSchema<>), typeof(ProtocolSchema<>));
        services.AddTransient(typeof(IProtocolCodec<>), typeof(ProtocolCodec<>));
        services.AddTransient(typeof(IFrameBuilder<>), typeof(ModbusFrameBuilder<>));
        return services;
    }
    
    /// <summary>
    /// 添加指定协议的配置
    /// </summary>
    public static IServiceCollection AddProtocol<TProtocol>(
        this IServiceCollection services,
        BooleanRepresentation booleanType = BooleanRepresentation.Int16,
        Endianness endianness = Endianness.BigEndian) where TProtocol : struct
    {
        services.AddSingleton<IProtocolSchema<TProtocol>>(
            _ => new ProtocolSchema<TProtocol>(booleanType, endianness));
        return services;
    }
}
```

---

## 4. 使用示例

### 4.1 定义协议

```csharp
using MessageToolkit.Attributes;

public struct DeviceProtocol
{
    [Address(100)]
    public int Speed { get; set; }
    
    [Address(104)]
    public float Temperature { get; set; }
    
    [Address(108)]
    public bool IsRunning { get; set; }
    
    [Address(110)]
    public short Status { get; set; }
}
```

### 4.2 构建单个写入帧

```csharp
// 创建帧构建器
var schema = new ProtocolSchema<DeviceProtocol>(
    BooleanRepresentation.Int16, 
    Endianness.BigEndian);
var builder = new ModbusFrameBuilder<DeviceProtocol>(schema);

// 构建写入整个协议的帧
var protocol = new DeviceProtocol 
{ 
    Speed = 1000, 
    Temperature = 25.5f, 
    IsRunning = true,
    Status = 1
};
ModbusFrame fullFrame = builder.BuildWriteFrame(protocol);

Console.WriteLine($"起始地址: {fullFrame.StartAddress}");      // 100
Console.WriteLine($"寄存器地址: {fullFrame.RegisterAddress}"); // 50
Console.WriteLine($"数据长度: {fullFrame.DataLength}");        // 12
Console.WriteLine($"数据: {BitConverter.ToString(fullFrame.Data)}");

// 构建写入单个字段的帧
ModbusFrame speedFrame = builder.BuildWriteFrame(
    p => p.Speed, 
    2000);

Console.WriteLine($"Speed 帧 - 地址: {speedFrame.StartAddress}, 数据: {BitConverter.ToString(speedFrame.Data)}");
```

### 4.3 批量写入

```csharp
// 使用批量构建器
var frames = builder.CreateBatchBuilder()
    .Write(p => p.Speed, 1500)
    .Write(p => p.Temperature, 30.0f)
    .Write(p => p.IsRunning, false)
    .BuildOptimized(); // 合并连续地址

foreach (var frame in frames)
{
    Console.WriteLine($"帧: 地址={frame.StartAddress}, 长度={frame.DataLength}");
}
```

### 4.4 与通信层集成

```csharp
// 假设有一个 ModbusClient
public class MyModbusClient
{
    private readonly IFrameBuilder<DeviceProtocol> _frameBuilder;
    private readonly SomeModbusLibrary _modbusClient; // 外部 Modbus 库
    
    public MyModbusClient(IFrameBuilder<DeviceProtocol> frameBuilder)
    {
        _frameBuilder = frameBuilder;
    }
    
    public void WriteProtocol(DeviceProtocol protocol)
    {
        // 1. 使用 MessageToolkit 构建帧
        var frame = _frameBuilder.BuildWriteFrame(protocol);
        
        // 2. 使用外部库发送数据
        _modbusClient.WriteMultipleRegisters(
            unitId: 1,
            startAddress: frame.RegisterAddress,
            data: frame.Data);
    }
    
    public DeviceProtocol ReadProtocol()
    {
        // 1. 获取读取信息
        var frame = _frameBuilder.BuildReadFrame();
        
        // 2. 使用外部库读取数据
        var data = _modbusClient.ReadHoldingRegisters(
            unitId: 1,
            startAddress: frame.RegisterAddress,
            count: _frameBuilder.Schema.RegisterCount);
        
        // 3. 使用 MessageToolkit 解码数据
        return _frameBuilder.Codec.Decode(data);
    }
}
```

### 4.5 使用依赖注入

```csharp
// Program.cs
services.AddMessageToolkit();
services.AddProtocol<DeviceProtocol>(
    BooleanRepresentation.Int16, 
    Endianness.BigEndian);

// 在服务中使用
public class DeviceService
{
    private readonly IFrameBuilder<DeviceProtocol> _frameBuilder;
    
    public DeviceService(IFrameBuilder<DeviceProtocol> frameBuilder)
    {
        _frameBuilder = frameBuilder;
    }
    
    public ModbusFrame CreateSetSpeedFrame(int speed)
    {
        return _frameBuilder.BuildWriteFrame(p => p.Speed, speed);
    }
}
```

---

## 5. 文件结构

```
MessageToolkit/
├── src/
│   └── MessageToolkit/
│       ├── Abstractions/
│       │   ├── IProtocolSchema.cs          # 协议模式接口
│       │   ├── IProtocolCodec.cs           # 协议编解码器接口
│       │   ├── IFrameBuilder.cs            # 帧构建器接口
│       │   └── IBatchFrameBuilder.cs       # 批量帧构建器接口
│       ├── Attributes/
│       │   └── AddressAttribute.cs         # 地址特性
│       ├── Models/
│       │   ├── ModbusFrame.cs              # Modbus 帧模型
│       │   ├── FrameCollection.cs          # 帧集合
│       │   ├── ProtocolFieldInfo.cs        # 协议字段信息
│       │   ├── BooleanRepresentation.cs    # 布尔表示枚举
│       │   └── Endianness.cs               # 字节序枚举
│       ├── Internal/
│       │   └── ByteConverter.cs            # 字节转换工具
│       ├── DependencyInjection/
│       │   └── ServiceCollectionExtensions.cs
│       ├── ProtocolSchema.cs               # 协议模式实现
│       ├── ProtocolCodec.cs                # 协议编解码器实现
│       ├── ModbusFrameBuilder.cs           # 帧构建器实现
│       ├── BatchFrameBuilder.cs            # 批量帧构建器实现
│       └── MessageToolkit.csproj
└── tests/
    └── MessageToolkit.Tests/
        ├── ProtocolSchemaTests.cs
        ├── ProtocolCodecTests.cs
        ├── FrameBuilderTests.cs
        └── BatchFrameBuilderTests.cs
```

---

## 6. 迁移指南

### 6.1 接口映射

| 旧接口/类 | 新接口/类 | 说明 |
|-----------|-----------|------|
| `IProtocolConfiguration<T>` | `IProtocolSchema<T>` | 重命名，语义更清晰 |
| `IProtocolSerialize<T>` | `IProtocolCodec<T>` | 重命名，更符合编解码概念 |
| `IMessageBuilder<T>` | `IFrameBuilder<T>` | 职责变更，只构建帧 |
| `IProtocolDataMapping<T>` | `IBatchFrameBuilder<T>` | 职责变更，批量构建帧 |
| `ModbusMessageBuilder<T>` | `ModbusFrameBuilder<T>` | 移除通信功能 |
| `ProtocolDataMapping<T>` | `BatchFrameBuilder<T>` | 内部类，链式构建 |
| `FluentModbusClient` | ❌ 移除 | 不再依赖 |

### 6.2 方法映射

| 旧方法 | 新方法 | 说明 |
|--------|--------|------|
| `WriteProtocol(protocol)` | `BuildWriteFrame(protocol)` | 返回帧而非直接写入 |
| `ReadProtocol()` | `BuildReadFrame()` + 外部读取 + `Codec.Decode()` | 分离读取和解码 |
| `Commit()` / `CommitAsync()` | `Build()` / `BuildOptimized()` | 返回帧集合 |

### 6.3 配置迁移

```csharp
// 旧代码
services.AddModbusMessageService();
services.AddMessageConfigurationService<MyProtocol>(typeof(short), true);

// 新代码
services.AddMessageToolkit();
services.AddProtocol<MyProtocol>(
    BooleanRepresentation.Int16, 
    Endianness.BigEndian);
```

---

## 7. 项目配置

### 7.1 新的 csproj 文件

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GeneratePackageOnBuild>True</GeneratePackageOnBuild>
    <Version>2.0.0</Version>
    <Authors>Lejoy</Authors>
    <Description>
      Modbus 协议帧构建工具库。
      支持协议序列化、帧构建，不依赖任何通信库。
    </Description>
    <PackageReleaseNotes>
      v2.0.0 - 重大重构
      - 移除 ModbusClient 依赖
      - 专注于协议帧构建
      - 新增 ModbusFrame 模型
      - 支持批量帧构建和地址合并优化
    </PackageReleaseNotes>
    <PackageOutputPath>D:\Code\NuGet\ModbusMessageToolkit</PackageOutputPath>
    <PackageIcon>modbusMessageToolkit.png</PackageIcon>
    <PackageReadmeFile>readme.md</PackageReadmeFile>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\..\NuGet\ModbusMessageToolkit\modbusMessageToolkit.png">
      <Pack>True</Pack>
      <PackagePath>\</PackagePath>
    </None>
    <None Include="..\..\NuGet\ModbusMessageToolkit\readme.md">
      <Pack>True</Pack>
      <PackagePath>\</PackagePath>
    </None>
  </ItemGroup>

  <!-- 移除外部依赖 -->
  <!-- <PackageReference Include="ProjectLibrary.Communication" Version="1.5.2" /> -->
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

---

## 8. 总结

### 8.1 重构收益

| 收益 | 描述 |
|------|------|
| **解耦** | 协议处理与通信完全分离 |
| **可测试** | 无需真实连接即可测试 |
| **灵活** | 可与任意 Modbus 库集成 |
| **轻量** | 移除外部依赖，包体积更小 |
| **清晰** | 职责单一，API 更直观 |

### 8.2 重构范围

- ✅ 移除 `ProjectLibrary.Communication` 依赖
- ✅ 重新设计核心接口和模型
- ✅ 内置字节转换工具
- ✅ 新增帧模型和批量构建器
- ✅ 保持协议序列化能力
- ✅ 提供依赖注入支持

### 8.3 后续计划

1. 实现并测试所有新组件
2. 编写完整的单元测试
3. 更新 NuGet 包文档
4. 发布 v2.0.0 版本

---

**文档版本**: 1.0  
**创建日期**: 2024-12-10  
**作者**: Claude Assistant

