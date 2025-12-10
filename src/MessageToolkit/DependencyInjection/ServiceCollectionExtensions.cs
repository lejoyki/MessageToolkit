using MessageToolkit.Abstractions;
using MessageToolkit.Models;
using Microsoft.Extensions.DependencyInjection;

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

