using MessageToolkit.Abstractions;
using MessageToolkit.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MessageToolkit.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 MessageToolkit 基础服务
    /// </summary>
    public static IServiceCollection AddMessageToolkit(this IServiceCollection services)
    {
        return services;
    }

    /// <summary>
    /// 添加 Modbus 字节帧协议支持
    /// </summary>
    public static IServiceCollection AddModbusProtocol<TProtocol>(
        this IServiceCollection services,
        BooleanRepresentation booleanType = BooleanRepresentation.Int16,
        Endianness endianness = Endianness.BigEndian) where TProtocol : struct
    {
        services.AddSingleton<IProtocolSchema<TProtocol>>(
            _ => new ProtocolSchema<TProtocol>(booleanType, endianness));
        services.AddTransient<IProtocolCodec<TProtocol, byte>, ProtocolCodec<TProtocol>>();
        services.AddTransient<IFrameBuilder<TProtocol, byte>, ModbusFrameBuilder<TProtocol>>();
        return services;
    }

    /// <summary>
    /// 添加位帧协议支持（用于 IO 点位）
    /// </summary>
    public static IServiceCollection AddBitProtocol<TProtocol>(
        this IServiceCollection services,
        BooleanRepresentation booleanType = BooleanRepresentation.Int16,
        Endianness endianness = Endianness.BigEndian) where TProtocol : struct
    {
        services.AddSingleton<IProtocolSchema<TProtocol>>(
            _ => new ProtocolSchema<TProtocol>(booleanType, endianness));
        services.AddTransient<IProtocolCodec<TProtocol, bool>, BitProtocolCodec<TProtocol>>();
        services.AddTransient<IFrameBuilder<TProtocol, bool>, BitFrameBuilder<TProtocol>>();
        return services;
    }
}

