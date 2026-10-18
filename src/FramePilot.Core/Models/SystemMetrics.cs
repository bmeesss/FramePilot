namespace FramePilot.Core.Models;

/// <summary>
/// Cumulative processor time counters, matching the semantics of the Windows
/// <c>GetSystemTimes</c> API: <see cref="Kernel"/> includes <see cref="Idle"/>.
/// </summary>
public readonly record struct ProcessorTimes(TimeSpan Idle, TimeSpan Kernel, TimeSpan User)
{
    /// <summary>Total (busy + idle) processor time. Kernel already contains idle.</summary>
    public TimeSpan Total => Kernel + User;

    /// <summary>Processor time spent doing work.</summary>
    public TimeSpan Busy => Total - Idle;
}

/// <summary>A point in time CPU reading.</summary>
/// <param name="UtilizationPercent">Average utilisation of all logical processors (0-100).</param>
/// <param name="LogicalProcessorCount">Number of logical processors of the machine.</param>
/// <param name="TotalCoreUtilizationPercent">
/// The sum of the utilisation of all logical processors, i.e. the aggregated
/// utilisation multiplied with the number of logical processors. 400 % on a
/// machine with eight processors means that on average four cores were busy.
/// It is an equivalent of the aggregated counter and not the load of one core.
/// </param>
public sealed record CpuSnapshot(
    double UtilizationPercent,
    int LogicalProcessorCount,
    double? TotalCoreUtilizationPercent)
{
    /// <summary>Returned when the CPU utilisation could not be sampled.</summary>
    public static CpuSnapshot Unavailable(int logicalProcessorCount) =>
        new(double.NaN, logicalProcessorCount, null);

    public bool IsAvailable => !double.IsNaN(UtilizationPercent);
}

/// <summary>Physical memory usage of the machine.</summary>
public sealed record MemorySnapshot(long TotalBytes, long AvailableBytes, long UsedBytes, double UsedPercent)
{
    public static MemorySnapshot Unavailable { get; } = new(0, 0, 0, 0);

    public bool IsAvailable => TotalBytes > 0;
}

/// <summary>Capacity information for one drive.</summary>
public sealed record DiskSnapshot(
    string Name,
    string DriveType,
    long TotalBytes,
    long AvailableBytes,
    long UsedBytes,
    double UsedPercent)
{
    public bool IsAvailable => TotalBytes > 0;
}

/// <summary>Everything the dashboard needs for a single sampling round.</summary>
public sealed record SystemSnapshot(
    CpuSnapshot Cpu,
    MemorySnapshot Memory,
    IReadOnlyList<DiskSnapshot> Disks,
    IReadOnlyList<ProcessSnapshot> Processes,
    TimeSpan Uptime);

/// <summary>
/// Raw per process counters. CPU utilisation is derived by the monitor by
/// comparing two consecutive samples, so the raw counters are exposed here.
/// </summary>
public sealed record ProcessCpuSample(
    int ProcessId,
    string Name,
    string? ExecutablePath,
    TimeSpan ProcessorTime,
    long WorkingSetBytes,
    long PrivateBytes,
    bool HasExited,
    bool HasMainWindow);

/// <summary>A process as presented to the user (with derived CPU percentage).</summary>
public sealed record ProcessSnapshot(
    int ProcessId,
    string Name,
    string? ExecutablePath,
    double? CpuPercent,
    long WorkingSetBytes,
    long PrivateBytes,
    bool HasMainWindow,
    bool IsSystemProcess)
{
    public bool CpuIsAvailable => CpuPercent.HasValue;
}

/// <summary>Static information about the machine and operating system.</summary>
public sealed record SystemInfo(
    string OperatingSystem,
    string OsVersion,
    string? DisplayVersion,
    string? Edition,
    string? Architecture,
    string? ProcessorName,
    int LogicalProcessorCount,
    int? PhysicalProcessorCount,
    long TotalPhysicalMemoryBytes,
    string? MachineName,
    string? Manufacturer,
    string? Model,
    string? BiosVersion)
{
    public static SystemInfo Unavailable { get; } = new(
        "Unavailable",
        "Unavailable",
        null,
        null,
        null,
        null,
        Environment.ProcessorCount,
        null,
        0,
        null,
        null,
        null,
        null);
}
