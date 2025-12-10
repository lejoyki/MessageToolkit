using FluentModbus;
using MessageToolkit.Abstractions;
using MessageToolkit.Models;
using MobusTest.Models;
using System.Linq.Expressions;

namespace MobusTest;

public sealed class AppRunner : IDisposable
{
    private readonly IFrameBuilder<DeviceProtocol> _builder;
    private readonly ModbusTcpClient _client = new();
    private TargetConfig _config = new("127.0.0.1", 502, 1);
    private DeviceProtocol _current = new()
    {
        Speed = 1500,
        Temperature = 23.5f,
        IsRunning = true,
        Status = 1
    };

    public AppRunner(IFrameBuilder<DeviceProtocol> builder)
    {
        _builder = builder;
    }

    public void Run()
    {
        Console.WriteLine("Modbus FluentModbus 示例，按提示输入指令。");
        Connect();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("选择操作：");
            Console.WriteLine("1) 写入整协议");
            Console.WriteLine("2) 写入单字段");
            Console.WriteLine("3) 读取整协议");
            Console.WriteLine("4) 读取单字段");
            Console.WriteLine("5) 修改连接配置并重连");
            Console.WriteLine("0) 退出");

            var option = Console.ReadLine();
            switch (option)
            {
                case "1":
                    _current = PromptProtocol(_current);
                    WriteFull(_current);
                    break;
                case "2":
                    WriteSingleField();
                    break;
                case "3":
                    ReadAll();
                    break;
                case "4":
                    ReadSingleField();
                    break;
                case "5":
                    _config = PromptConfig(_config);
                    Connect();
                    break;
                case "0":
                    return;
                default:
                    Console.WriteLine("无效输入，请重试。");
                    break;
            }
        }
    }

    private void WriteFull(DeviceProtocol protocol)
    {
        var frame = _builder.BuildWriteFrame(protocol);
        _client.WriteMultipleRegisters(_config.UnitId, frame.RegisterAddress, frame.Data.ToArray());
        Console.WriteLine($"已写入: 起始寄存器 {frame.RegisterAddress}，寄存器数 {frame.RegisterCount}");
    }

    private void WriteSingleField()
    {
        Console.WriteLine("选择字段: 1) Speed  2) Temperature  3) IsRunning  4) Status");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                var speed = PromptInt("Speed(int)", _current.Speed);
                WriteValue(p => p.Speed, speed);
                break;
            case "2":
                var temp = PromptFloat("Temperature(float)", _current.Temperature);
                WriteValue(p => p.Temperature, temp);
                break;
            case "3":
                var running = PromptBool("IsRunning(bool)", _current.IsRunning);
                WriteValue(p => p.IsRunning, running);
                break;
            case "4":
                var status = (short)PromptInt("Status(int16)", _current.Status);
                WriteValue(p => p.Status, status);
                break;
            default:
                Console.WriteLine("无效字段选择。");
                break;
        }
    }

    private void WriteValue<T>(Expression<Func<DeviceProtocol, T>> selector, T value) where T : unmanaged
    {
        var frame = _builder.BuildWriteFrame(selector, value);
        _client.WriteMultipleRegisters(_config.UnitId, frame.RegisterAddress, frame.Data.ToArray());
        Console.WriteLine($"已写入字段，起始寄存器 {frame.RegisterAddress}，寄存器数 {frame.RegisterCount}");
    }

    private void ReadSingleField()
    {
        Console.WriteLine("选择字段: 1) Speed  2) Temperature  3) IsRunning  4) Status");
        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                ReadField(p => p.Speed, "Speed");
                break;
            case "2":
                ReadField(p => p.Temperature, "Temperature");
                break;
            case "3":
                ReadField(p => p.IsRunning, "IsRunning");
                break;
            case "4":
                ReadField(p => p.Status, "Status");
                break;
            default:
                Console.WriteLine("无效字段选择。");
                break;
        }
    }

    private void ReadField<T>(Expression<Func<DeviceProtocol, T>> selector, string label) where T : unmanaged
    {
        // 使用新的 BuildReadRequest 方法，直接获取寄存器数量
        var request = _builder.BuildReadRequest(selector);

        if (request.RegisterCount == 0)
        {
            Console.WriteLine("寄存器数量为 0，读取跳过。");
            return;
        }

        var raw = _client.ReadHoldingRegisters(_config.UnitId, request.RegisterAddress, request.RegisterCount);
        var value = _builder.Codec.DecodeValue<T>(raw);
        Console.WriteLine($"读取结果 {label}: {value}");
    }

    private void ReadAll()
    {
        // 使用新的 BuildReadRequest 方法，直接获取寄存器数量
        var request = _builder.BuildReadRequest();

        var raw = _client.ReadHoldingRegisters(_config.UnitId, request.RegisterAddress, request.RegisterCount);
        var decoded = _builder.Codec.Decode(raw);
        Console.WriteLine($"读取结果: Speed={decoded.Speed}, Temp={decoded.Temperature}, Running={decoded.IsRunning}, Status={decoded.Status}");
    }

    private TargetConfig PromptConfig(TargetConfig current)
    {
        var host = PromptString("主机/IP", current.Host);
        var port = PromptInt("端口", current.Port);
        var unit = (byte)Math.Clamp(PromptInt("UnitId (0-247)", current.UnitId), 0, 247);
        return new TargetConfig(host, port, unit);
    }

    private static DeviceProtocol PromptProtocol(DeviceProtocol current)
    {
        return new DeviceProtocol
        {
            Speed = PromptInt("Speed(int)", current.Speed),
            Temperature = PromptFloat("Temperature(float)", current.Temperature),
            IsRunning = PromptBool("IsRunning(bool)", current.IsRunning),
            Status = (short)PromptInt("Status(int16)", current.Status)
        };
    }

    private void Connect()
    {
        try
        {
            _client.Disconnect();
        }
        catch
        {
            // ignore disconnect failures
        }

        _client.Connect($"{_config.Host}:{_config.Port}", ModbusEndianness.BigEndian);
        Console.WriteLine($"已连接到 {_config.Host}:{_config.Port} (UnitId={_config.UnitId})，字节序：大端。");
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private static string PromptString(string label, string current)
    {
        Console.Write($"{label} [{current}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? current : input.Trim();
    }

    private static int PromptInt(string label, int current)
    {
        Console.Write($"{label} [{current}]: ");
        var input = Console.ReadLine();
        return int.TryParse(input, out var v) ? v : current;
    }

    private static float PromptFloat(string label, float current)
    {
        Console.Write($"{label} [{current}]: ");
        var input = Console.ReadLine();
        return float.TryParse(input, out var v) ? v : current;
    }

    private static bool PromptBool(string label, bool current)
    {
        Console.Write($"{label} [{current}](y/n): ");
        var input = Console.ReadLine();
        return input?.Equals("y", StringComparison.OrdinalIgnoreCase) == true
            ? true
            : input?.Equals("n", StringComparison.OrdinalIgnoreCase) == true
                ? false
                : current;
    }
}
