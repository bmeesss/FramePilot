# Metrics, their meaning and their limits

FramePilot only shows values that it can read from a documented source. Every
metric below lists the source, the meaning and the limits. A metric that
cannot be read is shown as **Unavailable** - FramePilot never substitutes an
estimate, an average or a plausible looking number.

## Dashboard

| Value | Source | Meaning | Limits |
| --- | --- | --- | --- |
| CPU utilisation | `GetSystemTimes` (kernel, user, idle) | Share of the processor time that was not idle, averaged over all logical processors. | The first sample of a session only establishes the reference values. Counters are cumulative since boot, so a sample pair is needed. |
| Busy cores (equivalent) | derived from the CPU utilisation and the processor count | The aggregated utilisation multiplied with the number of logical processors, shown as "x.y busy on average" (50 % on eight processors = 4.0 busy cores). | It is an equivalent of the aggregated counter, **not** the load of a single core. The load of one core cannot be derived from the aggregated Windows counters. |
| Logical processors | `Environment.ProcessorCount` | Number of processors visible to the process. | Restricted by processor groups on very large machines. |
| Physical cores | `GetLogicalProcessorInformationEx` | Number of physical processor packages. | `null` when the API is not available; the value is then shown as *Unavailable*. |
| Physical memory | `GlobalMemoryStatusEx` | Total, available and used physical RAM of the **machine**. | This is not the memory usage of a single application. The "available" value is an estimate of what can be allocated without swapping. |
| Storage | `GetDiskFreeSpaceEx` via `DriveInfo` | Free and total space of the drives that are ready. | Only fixed and removable drives that are ready. Network drives and drives that are not ready are skipped. |
| Active power plan | `powercfg /getactivescheme` | The GUID of the active plan and its localised name. | The name is localised, the GUID is not. Only the GUID is parsed reliably. |
| System information | Registry (`CurrentVersion`, `CentralProcessor`, `BIOS`) | Windows version, build, edition, processor name, manufacturer, model, BIOS version. | Every value is optional; a missing value is shown as *Unavailable*. The registry still reports "Windows 10" on Windows 11, so the build number decides. |
| Uptime | `Environment.TickCount64` | Time since the last boot. | - |
| Per process CPU | `GetProcessTimes` via `Process.TotalProcessorTime` | Share of the total processor time that the process used, normalised by the number of logical processors. | Needs two samples. A process that just started has no value yet. A process that restarts resets its counter, the value is then shown as *Unavailable* for one sample. 100 % means that all logical processors are busy with this process. |
| Per process memory | `Process.WorkingSet64` | Resident memory of the process. | The working set is not the committed memory; a process can have a larger commit size. |

## Benchmark analysis

| Value | Source | Meaning | Limits |
| --- | --- | --- | --- |
| Frame time | PresentMon CSV | The time between two presented frames, taken from the column that matches the layout of the recording. | Only as good as the recording. See [benchmark-methodology.md](benchmark-methodology.md). |
| Average FPS | derived | Presented frames divided by the duration of the recording. | A recording of a menu is not a benchmark of the game. |
| Median, p95, p99 | derived | Percentiles of the frame times with linear interpolation. | - |
| 1 % low / 1 % high | derived | Mean frame rate of the slowest / fastest 1 % of the frames. | The definition is stated in the report so that it can be reproduced. |
| Frame time variation | derived | Standard deviation of the frame times. | - |
| Unusually long frames | derived | Frames slower than twice the median frame time. | A threshold that is relative to the recording, not an absolute stall detector. |

## Metrics that FramePilot does not show

* **GPU utilisation** - Windows does not expose a reliable, vendor independent
  API for it. FramePilot would have to guess, so it does not show it.
* **GPU temperature** - only available through vendor specific libraries.
* **CPU clock speed and turbo behaviour** - the counters are not comparable
  across CPUs and are frequently wrong under virtualization.
* **A frame rate that was not recorded** - for example a "predicted" FPS from
  the CPU load. FramePilot never produces such a number.
* **Disk throughput, network throughput** - not part of the current scope.

## Where the numbers come from

| File | Responsibility |
| --- | --- |
| `src/FramePilot.Core/Metrics/` | The calculations. Platform independent and unit tested. |
| `src/FramePilot.Windows/WindowsSystemMetricsSource.cs` | Reads the raw counters from Windows. |
| `src/FramePilot.Windows/WindowsSystemInfoProvider.cs` | Reads the static system information. |
| `src/FramePilot.Core/PresentMon/` | Parses the recordings and derives the frame metrics. |
