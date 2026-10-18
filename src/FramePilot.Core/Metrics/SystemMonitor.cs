using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;

namespace FramePilot.Core.Metrics;

/// <summary>
/// Samples the system and derives utilisation values from the raw counters.
/// </summary>
/// <remarks>
/// A freshly created monitor has no history, so the very first sample cannot
/// contain a CPU percentage; the dashboard shows "warming up" until the second
/// sample has been taken.
/// </remarks>
public sealed class SystemMonitor
{
    private readonly ISystemMetricsSource _source;
    private readonly IClock _clock;
    private readonly Dictionary<int, TimeSpan> _previousProcessTimes = new();
    private readonly object _gate = new();

    private ProcessorTimes? _previousProcessorTimes;
    private DateTimeOffset _previousSampleTime;
    private bool _hasPreviousSample;

    public SystemMonitor(ISystemMetricsSource source, IClock clock)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Number of logical processors of this machine.</summary>
    public int LogicalProcessorCount => Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// Takes a sample of the CPU, memory, disks and processes. The CPU and
    /// per process percentages are derived from the previous sample.
    /// </summary>
    public SystemSnapshot Sample()
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            var processorTimes = _source.GetProcessorTimes();
            var elapsed = _hasPreviousSample ? now - _previousSampleTime : TimeSpan.Zero;

            var utilization = CpuLoadCalculator.ComputeUtilization(_previousProcessorTimes, processorTimes);
            var totalCore = CpuLoadCalculator.ComputeTotalCoreUtilization(utilization, LogicalProcessorCount);

            var cpu = utilization is null
                ? CpuSnapshot.Unavailable(LogicalProcessorCount)
                : new CpuSnapshot(utilization.Value, LogicalProcessorCount, totalCore);

            var memory = _source.GetMemory();
            var disks = _source.GetDisks();
            var processes = BuildProcessSnapshots(elapsed);

            _previousProcessorTimes = processorTimes;
            _previousSampleTime = now;
            _hasPreviousSample = true;

            return new SystemSnapshot(cpu, memory, disks, processes, TimeSpan.FromMilliseconds(Environment.TickCount64));
        }
    }

    /// <summary>Static information about the machine.</summary>
    public SystemInfo GetSystemInfo() => _source.GetSystemInfo();

    private List<ProcessSnapshot> BuildProcessSnapshots(TimeSpan elapsed)
    {
        var samples = _source.GetProcessSamples();
        var seen = new HashSet<int>();
        var result = new List<ProcessSnapshot>(samples.Count);

        foreach (var sample in samples)
        {
            seen.Add(sample.ProcessId);

            double? cpuPercent = null;
            if (_previousProcessTimes.TryGetValue(sample.ProcessId, out var previous) && elapsed > TimeSpan.Zero)
            {
                cpuPercent = ProcessCpuCalculator.ComputePercent(
                    previous,
                    sample.ProcessorTime,
                    elapsed,
                    LogicalProcessorCount);
            }

            result.Add(new ProcessSnapshot(
                sample.ProcessId,
                sample.Name,
                sample.ExecutablePath,
                cpuPercent,
                sample.WorkingSetBytes,
                sample.PrivateBytes,
                sample.HasMainWindow,
                IsSystemProcess(sample)));
        }

        // Forget processes that have exited so the dictionary does not grow.
        foreach (var pid in _previousProcessTimes.Keys.ToArray())
        {
            if (!seen.Contains(pid))
            {
                _previousProcessTimes.Remove(pid);
            }
        }

        foreach (var sample in samples)
        {
            _previousProcessTimes[sample.ProcessId] = sample.ProcessorTime;
        }

        return result;
    }

    private static bool IsSystemProcess(ProcessCpuSample sample)
    {
        if (string.IsNullOrWhiteSpace(sample.ExecutablePath))
        {
            return false;
        }

        var windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windowsDirectory))
        {
            return false;
        }

        return sample.ExecutablePath.StartsWith(windowsDirectory, StringComparison.OrdinalIgnoreCase);
    }
}
