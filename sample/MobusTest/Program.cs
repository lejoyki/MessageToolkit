using MessageToolkit.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using MobusTest.Models;
using MessageToolkit.Models;
using MobusTest;

var services = new ServiceCollection()
    .AddMessageToolkit()
    .AddModbusProtocol<DeviceProtocol>(
        booleanType: BooleanRepresentation.Int16,
        endianness: Endianness.BigEndian)
    .AddSingleton<AppRunner>();

using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<AppRunner>();
app.Run();

