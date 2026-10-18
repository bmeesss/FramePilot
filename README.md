# FramePilot

FramePilot is an open source Windows performance and gaming optimization tool.
It measures a machine with documented Windows APIs, applies a small number of
**reversible** changes, and shows the evidence for whether a change actually
helped.

FramePilot does **not** ship "game mode" registry collections, does not disable
security services, does not terminate processes by force, and does not claim
that it can universally increase your frame rate. A successful optimisation
with **zero** frame rate gain is a valid result and is reported as such.

---

## Table of contents

* [What FramePilot does](#what-framepilot-does)
* [Requirements](#requirements)
* [Build and test](#build-and-test)
* [Publish the executable](#publish-the-executable)
* [Using FramePilot](#using-framepilot)
* [PresentMon](#presentmon)
* [Supported metrics and their limits](#supported-metrics-and-their-limits)
* [Safety behaviour](#safety-behaviour)
* [Benchmark methodology](#benchmark-methodology)
* [Project layout](#project-layout)
* [Testing](#testing)
* [Manual test procedure](#manual-test-procedure)
* [Known limitations](#known-limitations)
* [Contributing](#contributing)
* [License](#license)

---

## What FramePilot does

| Area | What it really does |
| --- | --- |
| **Dashboard** | Samples CPU utilisation (`GetSystemTimes`), physical memory (`GlobalMemoryStatusEx`), drive capacity, the active power plan (`powercfg /getactivescheme`) and per process CPU/memory. |
| **Game Sessions** | Activates a power plan that **already exists** on the machine, journals the original plan and restores it when the session ends, when the game exits or after an interrupted run. |
| **Benchmark Reports** | Parses genuine [PresentMon](https://github.com/GameTechDev/PresentMon) CSV recordings, computes average FPS, frame time percentiles, the 1 % low and long frames, and compares two configurations with a statistical test. |
| **Running Processes** | Lists processes that use CPU time or memory and can send a **graceful close request** to the application the user selected. |
| **Settings** | Paths, sampling interval, comparison settings and the storage locations. Everything stays on the local machine. |

## Requirements

* Windows 10 (1809 or newer) or Windows 11, 64 bit.
* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) to build from source.
* Optional: [PresentMon](https://github.com/GameTechDev/PresentMon) 2.x for frame time recordings. FramePilot does **not** bundle it.

FramePilot runs as a normal user. It never asks for administrator privileges.
The only operation that can need elevated rights is switching a power plan,
and when `powercfg` refuses the change, FramePilot reports the error and
restores the previous plan.

## Build and test

```bash
git clone https://github.com/bmeesss/FramePilot.git
cd FramePilot

dotnet restore FramePilot.sln
dotnet build FramePilot.sln -c Release
dotnet test tests/FramePilot.Core.Tests/FramePilot.Core.Tests.csproj -c Release
```

The unit test project targets plain `net10.0`, so it also runs on Linux and
macOS. The application itself is a WPF project and can only be built on
Windows.

## Publish the executable

```bash
dotnet publish src/FramePilot.App/FramePilot.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -o artifacts/publish
```

The result is a single, self contained `FramePilot.exe` (no .NET runtime
installation required on the target machine). The publish configuration lives
in `src/FramePilot.App/FramePilot.App.csproj`
(`PublishSingleFile`, `SelfContained`, `RuntimeIdentifier=win-x64`,
`IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`).

## Using FramePilot

### Dashboard

The dashboard samples once per second (configurable) and shows the real values.
The first sample only establishes the reference values, therefore the CPU
percentage appears with the second sample. Processes that just started have no
CPU percentage yet and are shown as *Unavailable*.

### Game sessions

1. Select the game executable (optionally with command line arguments).
2. Load the power plans and select the one to use during the session.
3. Press **Start session** and confirm.

FramePilot writes a recovery journal *before* it changes anything, activates
the plan and then reads the active plan back to verify that the change
worked. The original plan is restored when the game exits, when you press
**End session** or **Cancel**, and after an unclean shutdown.

On the next start FramePilot checks the journal. It restores the original plan
**only** when the plan that is currently active is still the temporary plan
FramePilot applied. If you changed the power plan yourself in the meantime,
your choice is left untouched.

### Running processes

The list shows the processes that use CPU time or a noticeable amount of
memory. Selecting one and pressing **Ask the application to close** shows a
confirmation with the reason and a warning; only after that confirmation a
close message is posted to the windows of that application. The application
decides whether it closes. FramePilot never calls `Process.Kill()` and never
touches system, driver, security or update processes.

### Benchmarks

1. Record with PresentMon (directly from the **Benchmarks** page or with
   PresentMon itself) and import the CSV file, or let FramePilot start a
   recording.
2. Assign each run to a configuration (`Baseline` / `Optimized`, the names are
   configurable) and mark warm-up runs.
3. Press **Compare the runs** and read the result.
4. **Save the report** to write a Markdown report next to your recordings.

The result never contains an estimate: every number comes from the recorded
frames.

## PresentMon

FramePilot reads the CSV files of the official
[PresentMon](https://github.com/GameTechDev/PresentMon) console application
(2.6.0 was the reference version while writing the parser).

* FramePilot **does not** ship the tool. Download it yourself and either put
  `PresentMon.exe` next to `FramePilot.exe`, into
  `%LOCALAPPDATA%\PresentMon\`, into `%ProgramFiles%\PresentMon\`, or set the
  path in **Settings**.
* When PresentMon cannot be found, the interface says so and offers the
  manual import of a recording.
* Only documented command line switches are used: `--output_file`,
  `--process_name`, `--process_id`, `--timed`, `--terminate_after_timed`,
  `--terminate_on_proc_exit`, `--session_name`, `--no_console_stats`,
  `--exclude_dropped`, `--no_track_gpu`, `--no_track_input`,
  `--no_track_display`. See the
  [PresentMon documentation](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md).
* PresentMon is MIT licensed, Copyright (C) 2017-2026 Intel Corporation. The
  full text is reproduced in [NOTICE.md](NOTICE.md).

### CSV layouts that are supported

| Layout | Frame time column | Meaning |
| --- | --- | --- |
| PresentMon 1.x (`--v1_metrics`) | `msBetweenPresents` | time between two `Present()` calls |
| PresentMon 2.x (`--v2_metrics`) | `FrameTime` | CPU busy + CPU wait of the frame |
| PresentMon 2.1+ (default) | `MsBetweenPresents` | time between two `Present()` calls |

The layout is detected from the header. A file that is not a PresentMon
recording is rejected with an explanation instead of being interpreted.

## Supported metrics and their limits

| Metric | Source | Limits |
| --- | --- | --- |
| CPU utilisation | `GetSystemTimes` | Aggregate over all logical processors. The first sample has no value. |
| Physical memory | `GlobalMemoryStatusEx` | The memory of the machine, never the memory of one application. |
| Drive capacity | `GetDiskFreeSpaceEx` | Only drives that are ready and fixed or removable. |
| Active power plan | `powercfg /getactivescheme` | The plan name is localised; only the GUID is parsed reliably. |
| Per process CPU | `GetProcessTimes` | Normalised by the number of logical processors, so 100 % means "all cores busy". A new process needs two samples. |
| Per process memory | `Process.WorkingSet64` | The working set, not the committed memory. |
| System information | Registry, `GetLogicalProcessorInformationEx` | Everything is optional; missing values are shown as *Unavailable*. |
| Frame times | PresentMon CSV | Only for a recorded capture. |

**Never displayed, because it cannot be read reliably:** GPU utilisation, GPU
temperature, CPU clock speed, and any frame rate that was not recorded. A
metric that cannot be retrieved is shown as *Unavailable* instead of a
plausible looking number.

## Safety behaviour

* No registry modifications, no service or startup changes, no security
  settings are touched.
* No process is terminated by force. Only a documented close message is sent,
  and only after an explicit confirmation.
* Power plans are never created, and never activated without confirmation.
* Every temporary change is journalled and reversed automatically.
* No telemetry, no analytics, no advertising, no uploads. Benchmarks and
  system reports stay in `%LOCALAPPDATA%\FramePilot`.
* External executables are started with an argument list, never with a
  concatenated command line, and they are validated before they are used.

## Benchmark methodology

The full description is in [docs/benchmark-methodology.md](docs/benchmark-methodology.md).
The short version:

1. **Same conditions.** Use the same game, executable, graphics settings,
   scene or replay, resolution and test duration for every run.
2. **At least five runs per configuration.** FramePilot refuses to report an
   improvement when there are fewer runs than that.
3. **Warm-up runs are excluded.** Mark the run that only filled the shader or
   driver cache as a warm-up run.
4. **Alternate the configurations** (baseline, optimized, baseline, ...) to
   reduce the influence of time related effects such as thermal throttling.
5. **Metrics.** Average FPS = presented frames / duration. Percentiles use
   linear interpolation between the two closest order statistics. The 1 % low
   is the mean of `1000 / frame time` over the frames whose frame time is at
   or above the 99th percentile of the recording.
6. **Statistics.** The medians of the runs are compared, the spread is
   reported and a two sided Mann-Whitney U test decides whether the
   difference is larger than the noise. A difference that is not significant
   is reported as *inconclusive*; a difference that is smaller than the
   practical threshold is reported as *no reliable difference*.
7. **Honesty.** A measured difference is an observation of these runs, not
   proof of causation. FramePilot never announces a proven frame rate
   improvement from a single uncontrolled run.

## Project layout

```
FramePilot.sln
src/FramePilot.Core        platform independent logic and abstractions (net10.0)
src/FramePilot.Windows     Windows implementations: counters, powercfg, processes (net10.0-windows)
src/FramePilot.App         the WPF application (net10.0-windows, WPF)
tests/FramePilot.Core.Tests  xunit test suite (net10.0)
docs/                      methodology, metrics and manual test procedure
```

The separation is strict: the core library contains the calculations, the
parsing and the session logic and has no dependency on WPF or on Windows APIs.
Everything that touches the machine lives in `FramePilot.Windows` behind an
interface, which is what makes the logic testable without a real machine.

## Testing

```bash
dotnet test tests/FramePilot.Core.Tests/FramePilot.Core.Tests.csproj -c Release
```

The suite covers the CPU utilisation calculation, the per process CPU
calculation and its edge cases, the memory calculation, the PresentMon CSV
parsing (valid, empty, malformed, truncated and unsupported files), the
average frame rate, the frame time percentiles and the documented 1 % low
calculation, the benchmark comparison including the inconclusive case, the
power plan parsing, the recovery journal, the process close policy and the
error and cancellation handling.

All external commands are mocked, the tests never change the settings of the
machine they run on. The GitHub workflow runs the suite on Linux and on
Windows, builds the application and publishes the self contained executable.

## Manual test procedure

The parts that depend on real hardware (power plan switching, the graceful
close of an application, a PresentMon recording) cannot be covered by the
automated tests. A reproducible manual procedure is documented in
[docs/manual-test-procedure.md](docs/manual-test-procedure.md).

## Known limitations

* PresentMon is not redistributed; frame time analysis needs the user's own
  copy of the tool.
* GPU metrics are not available. Adding them would require additional
  vendor specific tooling, which FramePilot does not do.
* The first CPU reading of a session is a reference value, not a utilisation.
* A power plan change can increase power consumption and temperatures. It does
  not necessarily improve the frame rate, and FramePilot never promises that
  it does.
* The published executable is a self contained single file, which is larger
  than a framework dependent build (roughly 150 MB).

## Contributing

1. Fork the repository and create a branch from `main`.
2. Keep the core library free of WPF and Windows specific code; put platform
   code behind an interface in `FramePilot.Windows`.
3. Add tests for new logic in `tests/FramePilot.Core.Tests`. Tests must not
   change the settings of the machine they run on - mock the external
   commands.
4. Run `dotnet build FramePilot.sln -c Release` and the test suite.
5. Open a pull request that describes what changed, how it was measured and
   what a reviewer has to check on real hardware.

## License

FramePilot is licensed under the [MIT license](LICENSE). Third party notices
are in [NOTICE.md](NOTICE.md).
