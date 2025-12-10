using MessageToolkit.Attributes;
using System.Runtime.InteropServices;

namespace MobusTest.Models;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeviceProtocol
{
    [Address(100)] public int Speed { get; set; }
    [Address(104)] public float Temperature { get; set; }
    [Address(108)] public bool IsRunning { get; set; }
    [Address(110)] public short Status { get; set; }
}

