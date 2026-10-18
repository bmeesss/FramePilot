using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.Metrics;
using FramePilot.Core.Models;

namespace FramePilot.App.ViewModels;

/// <summary>Shows the live system metrics of the machine.</summary>
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly SystemMonitor _monitor;
    private readonly IPowerPlanService _powerPlanService;
    private readonly DispatcherTimer _timer;
    private int _sampleCounter;

    private string _cpuValue = "Unavailable";
    private string _cpuDetail = "Waiting for the first sample";
    private double _cpuBarValue;
    private string _memoryValue = "Unavailable";
    private string _memoryDetail = string.Empty;
    private double _memoryBarValue;
    private string _storageValue = "Unavailable";
    private string _powerPlanValue = "Unavailable";
    private string _uptimeValue = "Unavailable";
    private string _systemInfoValue = "Unavailable";
    private string _lastUpdated = "Not sampled yet";
    private bool _autoRefresh = true;
    private bool _isRefreshing;
    private string _statusMessage = string.Empty;

    public DashboardViewModel(SystemMonitor monitor, IPowerPlanService powerPlanService)
    {
        _monitor = monitor;
        _powerPlanService = powerPlanService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync)
        {
            ErrorHandler = ex => StatusMessage = "The system could not be sampled: " + ex.Message,
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public override string Title => "Dashboard";

    public override string Description =>
        "Live values from the Windows APIs. Metrics that cannot be read are shown as \"Unavailable\" instead of an estimate.";

    public AsyncRelayCommand RefreshCommand { get; }

    public ObservableCollection<ProcessRow> TopProcesses { get; } = new();

    public string CpuValue
    {
        get => _cpuValue;
        private set => SetProperty(ref _cpuValue, value);
    }

    public string CpuDetail
    {
        get => _cpuDetail;
        private set => SetProperty(ref _cpuDetail, value);
    }

    public double CpuBarValue
    {
        get => _cpuBarValue;
        private set => SetProperty(ref _cpuBarValue, value);
    }

    public string MemoryValue
    {
        get => _memoryValue;
        private set => SetProperty(ref _memoryValue, value);
    }

    public string MemoryDetail
    {
        get => _memoryDetail;
        private set => SetProperty(ref _memoryDetail, value);
    }

    public double MemoryBarValue
    {
        get => _memoryBarValue;
        private set => SetProperty(ref _memoryBarValue, value);
    }

    public string StorageValue
    {
        get => _storageValue;
        private set => SetProperty(ref _storageValue, value);
    }

    public string PowerPlanValue
    {
        get => _powerPlanValue;
        private set => SetProperty(ref _powerPlanValue, value);
    }

    public string UptimeValue
    {
        get => _uptimeValue;
        private set => SetProperty(ref _uptimeValue, value);
    }

    public string SystemInfoValue
    {
        get => _systemInfoValue;
        private set => SetProperty(ref _systemInfoValue, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set => SetProperty(ref _lastUpdated, value);
    }

    public bool AutoRefresh
    {
        get => _autoRefresh;
        set
        {
            if (SetProperty(ref _autoRefresh, value))
            {
                if (value)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;

        try
        {
            var snapshot = await Task.Run(() => _monitor.Sample()).ConfigureAwait(true);

            ApplySnapshot(snapshot);

            _sampleCounter++;

            // The active power plan is read through powercfg, so it is not
            // queried on every sample.
            if (_sampleCounter % 5 == 1)
            {
                await RefreshPowerPlanAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private void ApplySnapshot(SystemSnapshot snapshot)
    {
        if (snapshot.Cpu.IsAvailable)
        {
            CpuValue = snapshot.Cpu.UtilizationPercent.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " %";
            CpuDetail = $"{snapshot.Cpu.LogicalProcessorCount} logical processors, " +
                        $"{((snapshot.Cpu.TotalCoreUtilizationPercent ?? 0) / 100.0).ToString("0.0", System.Globalization.CultureInfo.CurrentCulture)} busy on average";
            CpuBarValue = snapshot.Cpu.UtilizationPercent;
        }
        else
        {
            CpuValue = "Unavailable";
            CpuDetail = "The first sample only establishes the reference values";
            CpuBarValue = 0;
        }

        if (snapshot.Memory.IsAvailable)
        {
            MemoryValue = snapshot.Memory.UsedPercent.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " %";
            MemoryDetail = $"{ByteFormatter.Format(snapshot.Memory.UsedBytes)} used of {ByteFormatter.Format(snapshot.Memory.TotalBytes)} " +
                           $"({ByteFormatter.Format(snapshot.Memory.AvailableBytes)} available)";
            MemoryBarValue = snapshot.Memory.UsedPercent;
        }
        else
        {
            MemoryValue = "Unavailable";
            MemoryDetail = "The memory counters could not be read";
            MemoryBarValue = 0;
        }

        if (snapshot.Disks.Count > 0)
        {
            StorageValue = ByteFormatter.Format(snapshot.Disks.Sum(d => d.AvailableBytes));
            var details = snapshot.Disks
                .Select(d => $"{d.Name.TrimEnd('\\')} {ByteFormatter.Format(d.AvailableBytes)} free of {ByteFormatter.Format(d.TotalBytes)}");
            StatusMessage = string.Join("   |   ", details);
        }
        else
        {
            StorageValue = "Unavailable";
        }

        UptimeValue = FormatUptime(snapshot.Uptime);
        LastUpdated = DateTime.Now.ToString("HH:mm:ss");

        var systemInfo = _monitor.GetSystemInfo();
        SystemInfoValue = DescribeSystemInfo(systemInfo);

        TopProcesses.Clear();

        foreach (var process in snapshot.Processes
                     .OrderByDescending(p => p.CpuPercent ?? -1)
                     .ThenByDescending(p => p.WorkingSetBytes)
                     .Take(12))
        {
            TopProcesses.Add(ProcessRow.FromSnapshot(process));
        }
    }

    private async Task RefreshPowerPlanAsync()
    {
        try
        {
            var plan = await _powerPlanService.GetActivePowerPlanAsync().ConfigureAwait(true);
            PowerPlanValue = plan is null ? "Unavailable" : plan.Name;
        }
        catch (Exception)
        {
            PowerPlanValue = "Unavailable";
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime <= TimeSpan.Zero)
        {
            return "Unavailable";
        }

        return $"{(int)uptime.TotalDays} d {uptime.Hours} h {uptime.Minutes} min";
    }

    private static string DescribeSystemInfo(SystemInfo info)
    {
        var parts = new List<string>
        {
            info.OperatingSystem,
            "Build " + info.OsVersion,
        };

        if (!string.IsNullOrWhiteSpace(info.DisplayVersion))
        {
            parts.Add("Version " + info.DisplayVersion);
        }

        if (!string.IsNullOrWhiteSpace(info.ProcessorName))
        {
            parts.Add(info.ProcessorName);
        }

        parts.Add($"{info.LogicalProcessorCount} logical processors");

        if (info.PhysicalProcessorCount is { } physical)
        {
            parts.Add($"{physical} physical cores");
        }

        parts.Add(info.Architecture ?? "Unknown architecture");

        if (!string.IsNullOrWhiteSpace(info.Manufacturer) || !string.IsNullOrWhiteSpace(info.Model))
        {
            parts.Add($"{info.Manufacturer} {info.Model}".Trim());
        }

        return string.Join("   |   ", parts);
    }
}

/// <summary>One row of the process list.</summary>
public sealed class ProcessRow
{
    public required string Name { get; init; }

    public required int ProcessId { get; init; }

    public required string CpuText { get; init; }

    public required string MemoryText { get; init; }

    public required string Path { get; init; }

    public required bool IsSystem { get; init; }

    /// <summary>Measured CPU usage in percent, or null when it is not available.</summary>
    public double? CpuPercent { get; init; }

    /// <summary>Working set in bytes.</summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>True when the process owns a window that can be closed.</summary>
    public bool HasMainWindow { get; init; }

    public static ProcessRow FromSnapshot(ProcessSnapshot snapshot) => new()
    {
        Name = snapshot.Name,
        ProcessId = snapshot.ProcessId,
        CpuText = snapshot.CpuPercent is { } cpu
            ? cpu.ToString("0.0", System.Globalization.CultureInfo.CurrentCulture) + " %"
            : "Unavailable",
        MemoryText = ByteFormatter.Format(snapshot.WorkingSetBytes),
        Path = snapshot.ExecutablePath ?? "Unavailable",
        IsSystem = snapshot.IsSystemProcess,
        CpuPercent = snapshot.CpuPercent,
        WorkingSetBytes = snapshot.WorkingSetBytes,
        HasMainWindow = snapshot.HasMainWindow,
    };

    /// <summary>Converts the row back into the model of the core library.</summary>
    public ProcessSnapshot ToSnapshot() => new(
        ProcessId,
        Name,
        Path == "Unavailable" ? null : Path,
        CpuPercent,
        WorkingSetBytes,
        WorkingSetBytes,
        HasMainWindow,
        IsSystem);
}
