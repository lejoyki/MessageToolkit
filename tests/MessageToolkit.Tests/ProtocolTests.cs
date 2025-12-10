using MessageToolkit;
using MessageToolkit.Abstractions;
using MessageToolkit.Attributes;
using MessageToolkit.Models;
using Xunit;

namespace MessageToolkit.Tests;

public sealed class ProtocolTests
{
    private readonly IProtocolSchema<DemoProtocol> _schema;
    private readonly IProtocolCodec<DemoProtocol> _codec;
    private readonly IFrameBuilder<DemoProtocol> _builder;

    public ProtocolTests()
    {
        _schema = new ProtocolSchema<DemoProtocol>(BooleanRepresentation.Int16, Endianness.BigEndian);
        _codec = new ProtocolCodec<DemoProtocol>(_schema);
        _builder = new ModbusFrameBuilder<DemoProtocol>(_schema, _codec);
    }

    [Fact]
    public void Schema_Should_Parse_Address_And_Size()
    {
        Assert.Equal(100, _schema.StartAddress);
        Assert.Equal(12, _schema.TotalSize);
        Assert.Equal(4, _schema.Fields.Count);

        var boolField = _schema.GetFieldInfo(nameof(DemoProtocol.IsRunning));
        Assert.Equal(2, boolField.Size);
        Assert.Equal(108, boolField.ByteAddress);
    }

    [Fact]
    public void Codec_Should_Encode_And_Decode()
    {
        var protocol = new DemoProtocol
        {
            Speed = 1234,
            Temperature = 36.5f,
            IsRunning = true,
            Status = 7
        };

        var bytes = _codec.Encode(protocol);
        Assert.Equal(_schema.TotalSize, bytes.Length);

        // bool 以 Int16 表示，大端：0x00 0x01 位于偏移 8
        Assert.Equal(0x00, bytes[8]);
        Assert.Equal(0x01, bytes[9]);

        var decoded = _codec.Decode(bytes);
        Assert.Equal(protocol.Speed, decoded.Speed);
        Assert.Equal(protocol.Temperature, decoded.Temperature);
        Assert.Equal(protocol.IsRunning, decoded.IsRunning);
        Assert.Equal(protocol.Status, decoded.Status);
    }

    [Fact]
    public void FrameBuilder_Should_Build_WriteFrame()
    {
        var protocol = new DemoProtocol { Speed = 100, Temperature = 10.5f, IsRunning = false, Status = -1 };

        var writeFrame = _builder.BuildWriteFrame(protocol);
        Assert.Equal(ModbusFunctionCode.WriteMultipleRegisters, writeFrame.FunctionCode);
        Assert.Equal((ushort)_schema.StartAddress, writeFrame.StartAddress);
        Assert.Equal(_schema.TotalSize, writeFrame.DataLength);
        Assert.Equal((ushort)(_schema.TotalSize / 2), writeFrame.RegisterCount);
    }

    [Fact]
    public void FrameBuilder_Should_Build_ReadRequest_For_Protocol()
    {
        var readAll = _builder.BuildReadRequest();

        Assert.Equal(ModbusFunctionCode.ReadHoldingRegisters, readAll.FunctionCode);
        Assert.Equal((ushort)_schema.StartAddress, readAll.StartAddress);
        Assert.Equal((ushort)_schema.RegisterCount, readAll.RegisterCount);
        Assert.Equal(_schema.TotalSize, readAll.ByteCount);
    }

    [Fact]
    public void FrameBuilder_Should_Build_ReadRequest_For_Field()
    {
        var readField = _builder.BuildReadRequest(p => p.Temperature);

        Assert.Equal(ModbusFunctionCode.ReadHoldingRegisters, readField.FunctionCode);
        Assert.Equal((ushort)104, readField.StartAddress);
        Assert.Equal((ushort)2, readField.RegisterCount); // float = 4 bytes = 2 registers
        Assert.Equal(4, readField.ByteCount);
    }

    [Fact]
    public void FrameBuilder_Should_Build_WriteFrame_For_Field()
    {
        var writeFrame = _builder.BuildWriteFrame(p => p.Speed, 1500);

        Assert.Equal(ModbusFunctionCode.WriteMultipleRegisters, writeFrame.FunctionCode);
        Assert.Equal((ushort)100, writeFrame.StartAddress);
        Assert.Equal(4, writeFrame.DataLength); // int = 4 bytes
        Assert.Equal((ushort)2, writeFrame.RegisterCount);
    }

    [Fact]
    public void BatchBuilder_Should_Combine_Contiguous_Addresses()
    {
        using var frames = _builder.CreateBatchBuilder()
            .Write(p => p.Speed, 10)
            .Write(p => p.Temperature, 20.5f)
            .Write(p => p.IsRunning, true)
            .Write(p => p.Status, (short)2)
            .BuildOptimized();

        Assert.Single(frames);
        var frame = frames.Frames[0];
        Assert.Equal((ushort)100, frame.StartAddress);
        Assert.Equal(12, frame.DataLength); // 4 + 4 + 2 + 2
    }

    [Fact]
    public void BatchBuilder_Should_Not_Combine_NonContiguous_Addresses()
    {
        using var frames = _builder.CreateBatchBuilder()
            .Write(p => p.Speed, 10)          // 地址 100, 4 bytes
            .Write(p => p.IsRunning, true)    // 地址 108, 2 bytes (跳过 Temperature)
            .BuildOptimized();

        Assert.Equal(2, frames.Count);
        Assert.Equal((ushort)100, frames[0].StartAddress);
        Assert.Equal((ushort)108, frames[1].StartAddress);
    }

    [Fact]
    public void BatchBuilder_Clear_Should_Reset()
    {
        var builder = _builder.CreateBatchBuilder();
        builder.Write(p => p.Speed, 10);
        Assert.Equal(1, builder.Count);

        builder.Clear();
        Assert.Equal(0, builder.Count);

        using var frames = builder.Build();
        Assert.Empty(frames);
    }

    [Fact]
    public void WriteFrame_ToArray_Should_Return_Copy()
    {
        var writeFrame = _builder.BuildWriteFrame(p => p.Speed, 100);
        var array1 = writeFrame.ToArray();
        var array2 = writeFrame.ToArray();

        Assert.NotSame(array1, array2);
        Assert.Equal(array1, array2);
    }

    [Fact]
    public void ReadRequest_Static_Factory_Methods()
    {
        var holdingRequest = ModbusReadRequest.ReadHoldingRegisters(100, 10);
        Assert.Equal(ModbusFunctionCode.ReadHoldingRegisters, holdingRequest.FunctionCode);
        Assert.Equal((ushort)100, holdingRequest.StartAddress);
        Assert.Equal((ushort)10, holdingRequest.RegisterCount);

        var inputRequest = ModbusReadRequest.ReadInputRegisters(200, 5);
        Assert.Equal(ModbusFunctionCode.ReadInputRegisters, inputRequest.FunctionCode);
        Assert.Equal((ushort)200, inputRequest.StartAddress);
        Assert.Equal((ushort)5, inputRequest.RegisterCount);
    }

    private struct DemoProtocol
    {
        [Address(100)] public int Speed;
        [Address(104)] public float Temperature;
        [Address(108)] public bool IsRunning;
        [Address(110)] public short Status;
    }
}
