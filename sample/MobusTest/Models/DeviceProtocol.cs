using MessageToolkit.Attributes;
using System.Runtime.InteropServices;

namespace MobusTest.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeviceProtocol
{
    [Address(100)] public int Speed;
    [Address(104)] public float Temperature;
    [Address(108)] public bool IsRunning;
    [Address(110)] public short Status;
}

