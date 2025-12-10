# MessageToolkit 2.0.0

基于 REFACTOR_DESIGN 的 Modbus 协议帧构建与编解码库，聚焦协议层，完全移除通信依赖。可与任意 Modbus 通信客户端搭配使用。

## 主要特性
- 只负责协议序列化、反序列化与帧构建，零通信耦合
- 通过 `AddressAttribute` 反射生成协议模式，自动计算起始地址与总长度
- 支持布尔以 `Int16`/`Int32` 表示、可配置大小端
- 提供写入/读取帧构建、批量写入并自动合并连续地址
- 内置 `ByteConverter`，无外部字节工具依赖
- 提供依赖注入扩展，快速集成

## 安装
```bash
dotnet add package MessageToolkit --version 2.0.0
```

## 快速开始
1) 定义协议（标记字节地址）
```csharp
using MessageToolkit.Attributes;

public struct DeviceProtocol
{
    [Address(100)] public int Speed { get; set; }
    [Address(104)] public float Temperature { get; set; }
    [Address(108)] public bool IsRunning { get; set; }
    [Address(110)] public short Status { get; set; }
}
```

2) 注册服务（DI）
```csharp
using MessageToolkit.DependencyInjection;
using MessageToolkit.Models;

services.AddMessageToolkit();
services.AddProtocol<DeviceProtocol>(
    booleanType: BooleanRepresentation.Int16,
    endianness: Endianness.BigEndian);
```

3) 构建写入/读取帧
```csharp
var builder = provider.GetRequiredService<IFrameBuilder<DeviceProtocol>>();

// 写入整个协议
var protocol = new DeviceProtocol { Speed = 1000, Temperature = 25.5f, IsRunning = true, Status = 1 };
ModbusFrame writeAll = builder.BuildWriteFrame(protocol);

// 写入单字段
ModbusFrame speedOnly = builder.BuildWriteFrame(p => p.Speed, 2000);

// 读取整协议帧（供通信层读取后再解码）
ModbusFrame readAll = builder.BuildReadFrame();
```

4) 批量写入并自动合并
```csharp
FrameCollection frames = builder.CreateBatchBuilder()
    .Write(p => p.Speed, 1500)
    .Write(p => p.Temperature, 30.0f)
    .Write(p => p.IsRunning, false)
    .BuildOptimized(); // 合并连续地址
```

5) 通信层集成示意（伪代码）
```csharp
var frame = builder.BuildWriteFrame(protocol);
modbusClient.WriteMultipleRegisters(unitId: 1, frame.RegisterAddress, frame.Data);

var raw = modbusClient.ReadHoldingRegisters(unitId: 1, frame.RegisterAddress, builder.Schema.RegisterCount);
var decoded = builder.Codec.Decode(raw);
```

## API 速览
- `AddressAttribute`：标注字段/属性的字节地址
- `IProtocolSchema<T>`：协议模式（字段信息、起始地址、总大小）
- `IProtocolCodec<T>`：协议编解码
- `IFrameBuilder<T>`：单帧构建（读/写）
- `IBatchFrameBuilder<T>`：批量写入帧并可地址合并
- 模型：`ModbusFrame`、`FrameCollection`、`BooleanRepresentation`、`Endianness`

## 设计要点
- 按字段地址序列化，字段顺序无关
- 布尔表示可选 `Int16`/`Int32`，与大小端配置配合
- 仅输出帧数据，实际通信由上层自行发送/接收
- `FrameCollection.Optimize()` 支持连续地址合并，减少帧数
- 仅解析带 `AddressAttribute` 的公共属性（不支持字段成员）

## 开发与测试
```bash
dotnet build
```
（当前未附带单元测试，建议根据业务协议补充编解码与帧合并的用例）

## 许可
MIT（若仓库未声明，请根据实际许可补充）

