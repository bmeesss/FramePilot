using FramePilot.Core.Models;

namespace FramePilot.Core.Abstractions;

/// <summary>
/// Raw system counters. Implementations return values, they never compute
/// utilisation: the derivation lives in <c>SystemMonitor</c> so that it can be
/// unit tested without touching a real machine.
/// </summary>
public interface ISystemMetricsSource
{
    /// <summary>Cumulative processor times since boot.</summary>
    ProcessorTimes GetProcessorTimes();

    /// <summary>Physical memory totals.</summary>
    MemorySnapshot GetMemory();

    /// <summary>Capacity of every drive that is currently accessible.</summary>
    IReadOnlyList<DiskSnapshot> GetDisks();

    /// <summary>Per process counters for the current sample.</summary>
    IReadOnlyList<ProcessCpuSample> GetProcessSamples();

    /// <summary>Static information about the operating system and hardware.</summary>
    SystemInfo GetSystemInfo();
}
