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
        
        // 注册具体类型，以便用户可以使用完整功能
        services.AddTransient<ByteProtocolCodec<TProtocol>>();
        services.AddTransient<ModbusFrameBuilder<TProtocol>>();
        
        // 注册接口映射
        services.AddTransient<IProtocolCodec<TProtocol, byte>>(sp => sp.GetRequiredService<ByteProtocolCodec<TProtocol>>());
        services.AddTransient<IFrameBuilder<TProtocol, byte>>(sp => sp.GetRequiredService<ModbusFrameBuilder<TProtocol>>());
        
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
        
        // 注册具体类型，以便用户可以使用完整功能
        services.AddTransient<BitProtocolCodec<TProtocol>>();
        services.AddTransient<BitFrameBuilder<TProtocol>>();
        
        // 注册接口映射
        services.AddTransient<IProtocolCodec<TProtocol, bool>>(sp => sp.GetRequiredService<BitProtocolCodec<TProtocol>>());
        services.AddTransient<IFrameBuilder<TProtocol, bool>>(sp => sp.GetRequiredService<BitFrameBuilder<TProtocol>>());
        
        return services;
    }
}

