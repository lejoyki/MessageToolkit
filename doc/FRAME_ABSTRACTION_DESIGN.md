# Frame 抽象设计方案

> **状态**: ✅ 已完成
> **最后更新**: 2025年
>
> 此设计方案已完全实施。所有泛型接口和实现已就位，所有测试通过。

---

## 背景分析

通过对比 `MessageToolkit` 和 `ZMotionSDK.ProtocolSugar` 两个库，发现它们在通信层封装上有很高的相似性：

### MessageToolkit 现状
- **数据类型**: `byte[]` (字节数组)
- **核心抽象**: `ModbusWriteFrame`, `ModbusReadRequest`
- **协议描述**: `IProtocolSchema<T>`, `ProtocolFieldInfo`
- **编解码器**: `IProtocolCodec<T>`
- **帧构建器**: `IFrameBuilder<T>`, `IBatchFrameBuilder<T>`

### ZMotionSDK.ProtocolSugar 现状
- **数据类型**: `bool` (布尔值)
- **核心抽象**: `IMessageBuilder<TDI, TDO>`
- **协议描述**: `IProtocolConfiguration<T>`
- **数据映射**: `IProtocolDataMapping<T>`
- **值设置器**: `PropertyValueSetter<T>`

### 相似之处
| 概念 | MessageToolkit | ZMotionSDK.ProtocolSugar |
|------|----------------|--------------------------|
| 地址标记 | `AddressAttribute(ushort byteAddress)` | `AddressAttribute(int address)` |
| 协议配置 | `IProtocolSchema<T>` | `IProtocolConfiguration<T>` |
| 帧/数据构建 | `IFrameBuilder<T>` | `IMessageBuilder<TDI, TDO>` |
| 批量操作 | `IBatchFrameBuilder<T>` | `IProtocolDataMapping<T>` |

### 核心差异
- **数据粒度**: MessageToolkit 操作字节块，ZMotionSDK 操作单个布尔位
- **地址语义**: MessageToolkit 是寄存器地址(byte offset)，ZMotionSDK 是 IO 点位地址
- **通信模式**: MessageToolkit 生成帧供外部发送，ZMotionSDK 直接通过 `ZMotion` 对象通信

---

## 设计目标

1. **抽象 Frame 数据类型** - 让数据载荷支持 `byte[]`、`bool`、`bool[]` 等多种类型
2. **统一协议描述层** - 共享 `AddressAttribute` 和协议解析逻辑
3. **保持各自特性** - 允许 MessageToolkit 继续专注 Modbus 字节帧，同时可扩展支持位级操作
4. **向后兼容** - 现有 API 不受影响，新功能作为扩展

---

## 架构设计

### 1. 核心抽象层 (MessageToolkit.Abstractions)

```
MessageToolkit.Abstractions/
├── IFrame.cs                    # 帧抽象基接口
├── IFrame{TData}.cs             # 泛型帧接口
├── IProtocolSchema{T}.cs        # 协议模式（已有，保持）
├── IProtocolCodec{T,TData}.cs   # 泛型编解码器
├── IFrameBuilder{T,TData}.cs    # 泛型帧构建器
└── IDataMapping{T,TData}.cs     # 泛型数据映射
```

### 2. 新增接口设计

#### 2.1 帧抽象接口

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 帧抽象基接口 - 表示一个可传输的数据单元
/// </summary>
public interface IFrame
{
    /// <summary>
    /// 起始地址
    /// </summary>
    int StartAddress { get; }
    
    /// <summary>
    /// 数据长度（元素数量）
    /// </summary>
    int DataLength { get; }
    
    /// <summary>
    /// 帧类型标识
    /// </summary>
    FrameType FrameType { get; }
}

/// <summary>
/// 帧类型枚举
/// </summary>
public enum FrameType
{
    /// <summary>
    /// 字节帧（Modbus 寄存器）
    /// </summary>
    ByteFrame,
    
    /// <summary>
    /// 位帧（IO 点位）
    /// </summary>
    BitFrame
}

/// <summary>
/// 泛型帧接口 - 携带特定类型的数据载荷
/// </summary>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IFrame<TData> : IFrame
{
    /// <summary>
    /// 数据载荷
    /// </summary>
    ReadOnlyMemory<TData> Data { get; }
    
    /// <summary>
    /// 获取数据副本
    /// </summary>
    TData[] ToArray();
}
```

#### 2.2 写入帧与读取请求

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 写入帧接口
/// </summary>
public interface IWriteFrame<TData> : IFrame<TData>
{
}

/// <summary>
/// 读取请求接口
/// </summary>
public interface IReadRequest : IFrame
{
    /// <summary>
    /// 请求读取的元素数量
    /// </summary>
    int Count { get; }
}
```

#### 2.3 泛型编解码器

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 泛型协议编解码器接口
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IProtocolCodec<TProtocol, TData> 
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 序列化整个协议为数据数组
    /// </summary>
    TData[] Encode(TProtocol protocol);

    /// <summary>
    /// 从数据数组反序列化协议
    /// </summary>
    TProtocol Decode(ReadOnlySpan<TData> data);

    /// <summary>
    /// 序列化单个值
    /// </summary>
    TData[] EncodeValue<TValue>(TValue value) where TValue : unmanaged;

    /// <summary>
    /// 反序列化单个值
    /// </summary>
    TValue DecodeValue<TValue>(ReadOnlySpan<TData> data) where TValue : unmanaged;
}
```

#### 2.4 泛型帧构建器

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 泛型帧构建器接口
/// </summary>
/// <typeparam name="TProtocol">协议类型</typeparam>
/// <typeparam name="TData">数据载荷类型</typeparam>
public interface IFrameBuilder<TProtocol, TData> 
    where TProtocol : struct
{
    /// <summary>
    /// 协议模式
    /// </summary>
    IProtocolSchema<TProtocol> Schema { get; }

    /// <summary>
    /// 编解码器
    /// </summary>
    IProtocolCodec<TProtocol, TData> Codec { get; }

    /// <summary>
    /// 构建写入整个协议的帧
    /// </summary>
    IWriteFrame<TData> BuildWriteFrame(TProtocol protocol);

    /// <summary>
    /// 构建写入单个字段的帧
    /// </summary>
    IWriteFrame<TData> BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged;

    /// <summary>
    /// 构建读取整个协议的请求
    /// </summary>
    IReadRequest BuildReadRequest();

    /// <summary>
    /// 构建读取单个字段的请求
    /// </summary>
    IReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged;

    /// <summary>
    /// 创建批量构建器
    /// </summary>
    IBatchFrameBuilder<TProtocol, TData> CreateBatchBuilder();
}
```

---

### 3. 实现层设计

#### 3.1 字节帧实现 (现有 Modbus 支持)

保持现有 `ModbusWriteFrame`、`ModbusReadRequest` 作为 `IWriteFrame<byte>`、`IReadRequest` 的具体实现：

```csharp
namespace MessageToolkit.Models;

/// <summary>
/// Modbus 写入帧 - 字节数据帧实现
/// </summary>
public readonly struct ModbusWriteFrame : IWriteFrame<byte>
{
    public int StartAddress { get; }
    public ReadOnlyMemory<byte> Data { get; }
    public int DataLength => Data.Length;
    public FrameType FrameType => FrameType.ByteFrame;

    // 保持现有的 RegisterAddress, RegisterCount 属性供 Modbus 使用
    public ushort RegisterAddress => (ushort)(StartAddress / 2);
    public ushort RegisterCount => (ushort)(Data.Length / 2);

    public ModbusWriteFrame(ushort startAddress, byte[] data) { ... }
    public ModbusWriteFrame(ushort startAddress, ReadOnlyMemory<byte> data) { ... }
    public byte[] ToArray() => Data.ToArray();
}
```

#### 3.2 位帧实现 (新增 IO 支持)

```csharp
namespace MessageToolkit.Models;

/// <summary>
/// 位写入帧 - 布尔数据帧实现（用于 IO 点位）
/// </summary>
public readonly struct BitWriteFrame : IWriteFrame<bool>
{
    public int StartAddress { get; }
    public ReadOnlyMemory<bool> Data { get; }
    public int DataLength => Data.Length;
    public FrameType FrameType => FrameType.BitFrame;

    public BitWriteFrame(int startAddress, bool[] data)
    {
        StartAddress = startAddress;
        Data = data;
    }

    public BitWriteFrame(int startAddress, ReadOnlyMemory<bool> data)
    {
        StartAddress = startAddress;
        Data = data;
    }

    public bool[] ToArray() => Data.ToArray();
}

/// <summary>
/// 位读取请求
/// </summary>
public readonly struct BitReadRequest : IReadRequest
{
    public int StartAddress { get; }
    public int Count { get; }
    public int DataLength => 0; // 请求不携带数据
    public FrameType FrameType => FrameType.BitFrame;

    public BitReadRequest(int startAddress, int count)
    {
        StartAddress = startAddress;
        Count = count;
    }
}
```

#### 3.3 位协议编解码器

```csharp
namespace MessageToolkit;

/// <summary>
/// 位协议编解码器 - 用于 bool 字段的协议
/// </summary>
public sealed class BitProtocolCodec<TProtocol> : IProtocolCodec<TProtocol, bool>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }

    public BitProtocolCodec(IProtocolSchema<TProtocol> schema)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
    }

    public bool[] Encode(TProtocol protocol)
    {
        var buffer = new bool[Schema.TotalSize];
        foreach (var (name, address) in Schema.BooleanProperties)
        {
            var property = typeof(TProtocol).GetProperty(name);
            if (property?.GetValue(protocol) is bool value)
            {
                buffer[address - Schema.StartAddress] = value;
            }
        }
        return buffer;
    }

    public TProtocol Decode(ReadOnlySpan<bool> data)
    {
        var result = new TProtocol();
        object boxed = result;

        foreach (var (name, address) in Schema.BooleanProperties)
        {
            var offset = address - Schema.StartAddress;
            if (offset >= 0 && offset < data.Length)
            {
                var property = typeof(TProtocol).GetProperty(name);
                property?.SetValue(boxed, data[offset]);
            }
        }

        return (TProtocol)boxed;
    }

    public bool[] EncodeValue<TValue>(TValue value) where TValue : unmanaged
    {
        if (value is bool b)
            return [b];
        throw new NotSupportedException($"BitProtocolCodec 仅支持 bool 类型");
    }

    public TValue DecodeValue<TValue>(ReadOnlySpan<bool> data) where TValue : unmanaged
    {
        if (typeof(TValue) == typeof(bool) && data.Length > 0)
            return (TValue)(object)data[0];
        throw new NotSupportedException($"BitProtocolCodec 仅支持 bool 类型");
    }
}
```

#### 3.4 位帧构建器

```csharp
namespace MessageToolkit;

/// <summary>
/// 位帧构建器 - 用于构建 IO 点位帧
/// </summary>
public sealed class BitFrameBuilder<TProtocol> : IFrameBuilder<TProtocol, bool>
    where TProtocol : struct
{
    public IProtocolSchema<TProtocol> Schema { get; }
    public IProtocolCodec<TProtocol, bool> Codec { get; }

    public BitFrameBuilder(IProtocolSchema<TProtocol> schema)
        : this(schema, new BitProtocolCodec<TProtocol>(schema))
    {
    }

    public BitFrameBuilder(IProtocolSchema<TProtocol> schema, IProtocolCodec<TProtocol, bool> codec)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Codec = codec ?? throw new ArgumentNullException(nameof(codec));
    }

    public IWriteFrame<bool> BuildWriteFrame(TProtocol protocol)
    {
        return new BitWriteFrame(Schema.StartAddress, Codec.Encode(protocol));
    }

    public IWriteFrame<bool> BuildWriteFrame<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector,
        TValue value) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return new BitWriteFrame(address, Codec.EncodeValue(value));
    }

    public IReadRequest BuildReadRequest()
    {
        return new BitReadRequest(Schema.StartAddress, Schema.TotalSize);
    }

    public IReadRequest BuildReadRequest<TValue>(
        Expression<Func<TProtocol, TValue>> fieldSelector) where TValue : unmanaged
    {
        var address = Schema.GetAddress(fieldSelector);
        return new BitReadRequest(address, 1);
    }

    public IBatchFrameBuilder<TProtocol, bool> CreateBatchBuilder()
    {
        return new BitBatchFrameBuilder<TProtocol>(this);
    }
}
```

---

### 4. 数据映射层 (Fluent API)

```csharp
namespace MessageToolkit.Abstractions;

/// <summary>
/// 泛型数据映射接口 - 支持链式写入
/// </summary>
public interface IDataMapping<TProtocol, TData> 
    where TProtocol : struct
{
    /// <summary>
    /// 添加数据项
    /// </summary>
    void AddData(int address, TData data);

    /// <summary>
    /// 设置属性值（链式调用）
    /// </summary>
    IValueSetter<TProtocol, TData> Property<TValue>(
        Expression<Func<TProtocol, TValue>> propertyExpression);

    /// <summary>
    /// 设置地址值（链式调用）
    /// </summary>
    IValueSetter<TProtocol, TData> Property(int address);

    /// <summary>
    /// 构建帧集合
    /// </summary>
    IEnumerable<IWriteFrame<TData>> Build();

    /// <summary>
    /// 构建并优化（合并连续地址）
    /// </summary>
    IEnumerable<IWriteFrame<TData>> BuildOptimized();
}

/// <summary>
/// 值设置器接口
/// </summary>
public interface IValueSetter<TProtocol, TData> 
    where TProtocol : struct
{
    /// <summary>
    /// 设置值并返回映射对象（链式调用）
    /// </summary>
    IDataMapping<TProtocol, TData> Value(TData value);
}
```

---

### 5. 统一地址特性

合并两个库的 `AddressAttribute`：统一Address

```csharp
namespace MessageToolkit.Attributes;

/// <summary>
/// 地址特性 - 标记协议字段的地址
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class AddressAttribute : Attribute
{
    /// <summary>
    /// 地址
    /// </summary>
    public ushort Address { get; }

    /// <summary>
    /// 使用字节地址创建
    /// </summary>
    public AddressAttribute(ushort address)
    {
        Address = address;
    }
}
```

---

## 使用示例

### 示例 1: Modbus 字节帧（现有用法，完全兼容）

```csharp
// 定义寄存器协议
public struct DeviceProtocol
{
    [Address(100)] public int Speed { get; set; }
    [Address(104)] public float Temperature { get; set; }
    [Address(108)] public bool IsRunning { get; set; }
}

// 构建字节帧
var schema = new ProtocolSchema<DeviceProtocol>();
var builder = new ModbusFrameBuilder<DeviceProtocol>(schema);

var protocol = new DeviceProtocol { Speed = 1000, Temperature = 25.5f, IsRunning = true };
ModbusWriteFrame frame = builder.BuildWriteFrame(protocol);

// 通过通信层发送
modbusClient.WriteMultipleRegisters(1, frame.RegisterAddress, frame.Data.ToArray());
```

### 示例 2: IO 位帧（新增用法）

```csharp
// 定义 IO 协议
public struct DIProtocol
{
    [Address(0)] public bool StartButton { get; set; }
    [Address(1)] public bool StopButton { get; set; }
    [Address(2)] public bool EmergencyStop { get; set; }
}

public struct DOProtocol
{
    [Address(0)] public bool MotorEnable { get; set; }
    [Address(1)] public bool AlarmLight { get; set; }
    [Address(2)] public bool RunningLight { get; set; }
}

// 构建位帧
var schema = new ProtocolSchema<DOProtocol>();
var builder = new BitFrameBuilder<DOProtocol>(schema);

var protocol = new DOProtocol { MotorEnable = true, RunningLight = true };
BitWriteFrame frame = builder.BuildWriteFrame(protocol);

// 通过 IO 通信层发送
ioClient.SetDO_Multi(frame.StartAddress, frame.ToArray());
```

### 示例 3: 批量写入与链式调用

```csharp
// 字节帧批量
var byteFrames = builder.CreateBatchBuilder()
    .Write(p => p.Speed, 1500)
    .Write(p => p.Temperature, 30.0f)
    .BuildOptimized();

// 位帧批量
var bitBuilder = new BitFrameBuilder<DOProtocol>(schema);
var bitFrames = bitBuilder.CreateBatchBuilder()
    .Write(p => p.MotorEnable, true)
    .Write(p => p.AlarmLight, false)
    .Build();
```

### 示例 4: 兼容 ZMotionSDK 风格的 Fluent API

```csharp
// 创建数据映射
var mapping = bitBuilder.CreateDataMapping();

mapping
    .Property(p => p.MotorEnable).Value(true)
    .Property(p => p.RunningLight).Value(true)
    .Property(p => p.AlarmLight).Value(false);

// 获取帧
var frames = mapping.Build();

// 或直接提交到通信层
foreach (var frame in frames)
{
    ioClient.SetDO_Multi(frame.StartAddress, frame.ToArray());
}
```

---

## 迁移指南

### 从 MessageToolkit 迁移
- **无需修改**：现有 `ModbusFrameBuilder`、`ModbusWriteFrame` 等 API 保持不变
- **可选升级**：如需位操作支持，引入 `BitFrameBuilder`

### 从 ZMotionSDK.ProtocolSugar 迁移
1. 将 `AddressAttribute` 改用 MessageToolkit 版本
2. 将 `IMessageBuilder` 替换为 `IFrameBuilder<T, bool>`
3. 将 `IProtocolDataMapping` 替换为 `IDataMapping<T, bool>`
4. 通信层对接保持独立（ZMotion 对象仍由 ZMotionSDK 管理）

---

## 项目结构

```
MessageToolkit/
├── Abstractions/
│   ├── IFrame.cs                  # 帧基接口 + 泛型帧接口
│   ├── IWriteFrame.cs             # 写入帧接口
│   ├── IReadRequest.cs            # 读取请求接口
│   ├── IProtocolSchema.cs         # 协议模式接口
│   ├── IProtocolCodec.cs          # 泛型编解码器接口
│   ├── IFrameBuilder.cs           # 泛型帧构建器接口
│   ├── IBatchFrameBuilder.cs      # 泛型批量构建器接口
│   ├── IDataMapping.cs            # 数据映射接口（Fluent API）
│   └── IValueSetter.cs            # 值设置器接口
├── Models/
│   ├── FrameType.cs               # 帧类型枚举
│   ├── ModbusFrame.cs             # Modbus 写入帧 + 读取请求
│   ├── BitFrame.cs                # 位写入帧 + 读取请求
│   ├── ProtocolFieldInfo.cs       # 协议字段信息
│   ├── BooleanRepresentation.cs   # 布尔表示方式
│   ├── Endianness.cs              # 字节序
│   └── FrameCollection.cs         # 帧集合（可选）
├── Attributes/
│   └── AddressAttribute.cs        # 地址特性
├── ModbusFrameBuilder.cs          # Modbus 帧构建器
├── ModbusBatchFrameBuilder.cs     # Modbus 批量构建器
├── BitFrameBuilder.cs             # 位帧构建器
├── BitBatchFrameBuilder.cs        # 位批量构建器
├── BitDataMapping.cs              # 位数据映射
├── ProtocolSchema.cs              # 协议模式实现
├── ProtocolCodec.cs               # Modbus 编解码器
├── BitProtocolCodec.cs            # 位编解码器
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs  # DI 扩展
```

---

## 总结

本设计方案通过引入泛型 `IFrame<TData>` 抽象，将帧的数据载荷类型参数化，实现了：

1. **统一抽象**：`IFrame<byte>` 用于 Modbus 字节帧，`IFrame<bool>` 用于 IO 位帧
2. **代码复用**：协议解析、地址映射、批量优化等逻辑可共享
4. **易于扩展**：未来可支持更多数据类型（如 `IFrame<ushort>` 用于字帧）

建议实施步骤：
1. 先增加抽象接口，不修改现有实现
2. 让现有 `ModbusWriteFrame` 实现 `IWriteFrame<byte>`
3. 新增 `BitWriteFrame` 和 `BitFrameBuilder`
4. 完善 DI 扩展和文档
