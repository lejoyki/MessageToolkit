using MessageToolkit.Models;

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
            return new[] { bytes[1], bytes[0], bytes[3], bytes[2] };
        }
        return bytes;
    }

    public static byte[] GetBytes(uint value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        if (endianness == Endianness.BigEndian)
        {
            return new[] { bytes[1], bytes[0], bytes[3], bytes[2] };
        }
        return bytes;
    }

    public static byte[] GetBytes(float value, Endianness endianness)
    {
        var bytes = BitConverter.GetBytes(value);
        if (endianness == Endianness.BigEndian)
        {
            return new[] { bytes[1], bytes[0], bytes[3], bytes[2] };
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

    public static ushort ToUInt16(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            return (ushort)(bytes[0] << 8 | bytes[1]);
        }
        return BitConverter.ToUInt16(bytes);
    }

    public static int ToInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            Span<byte> temp = stackalloc byte[4];
            temp[0] = bytes[1];
            temp[1] = bytes[0];
            temp[2] = bytes[3];
            temp[3] = bytes[2];
            return BitConverter.ToInt32(temp);
        }
        return BitConverter.ToInt32(bytes);
    }

    public static uint ToUInt32(ReadOnlySpan<byte> bytes, Endianness endianness)
    {
        if (endianness == Endianness.BigEndian)
        {
            Span<byte> temp = stackalloc byte[4];
            temp[0] = bytes[1];
            temp[1] = bytes[0];
            temp[2] = bytes[3];
            temp[3] = bytes[2];
            return BitConverter.ToUInt32(temp);
        }
        return BitConverter.ToUInt32(bytes);
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
}

