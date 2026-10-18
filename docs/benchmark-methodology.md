# Benchmark methodology

This document describes how FramePilot measures a benchmark and how the result
is interpreted. It is the reference for every number the application shows.

## 1. What is measured

FramePilot analyses **PresentMon CSV recordings**. PresentMon is an ETW based
capture tool from Intel that records every frame an application presents to the
display. FramePilot never estimates a frame rate: every value in a report comes
from a recorded frame.

The recording is made with the same binary, the same scene or replay, the same
graphics settings and the same resolution for every run.

## 2. Preparing a measurement

1. **Close everything that is not needed.** A background download, a browser
   with 40 tabs or a second game changes the result. FramePilot's process list
   helps to find the obvious candidates, but it cannot prove that a process is
   idle.
2. **Let the machine settle.** A laptop that was just gaming is thermally
   limited; a desktop that was idle for an hour has a cold CPU cache. Use the
   same warm-up for every run.
3. **Use a repeatable scene.** A fixed benchmark scene, a fixed replay or a
   fixed route is required. A random play session is not a benchmark.
4. **Keep the duration constant.** A recording of 60 seconds per run is a
   good default. The first seconds of a recording are often slower (loading,
   compilation), which is one reason to exclude warm-up runs.

## 3. Warm-up runs

A *warm-up run* is a run that is recorded but not evaluated. It is used for:

* the first launch of the game (shader compilation, asset streaming),
* a driver or shader cache rebuild,
* the transition into the steady state of the scene.

FramePilot excludes warm-up runs from the comparison and reports how many were
excluded. The documented procedure is:

> Record one warm-up run per configuration before the measured runs, and mark
> it as a warm-up run in the benchmark list. The warm-up run must use the same
> settings as the measured runs.

## 4. Number of runs

The default is **five measured runs per configuration**. FramePilot refuses to
report an improvement when a configuration has fewer runs than that:

> *Not enough runs: 'Baseline' has 3 (needs 2 more), 'Optimized' has 5 (needs 0
> more). FramePilot does not report an improvement from an insufficient number
> of runs.*

The requirement can be changed in the settings, but lowering it increases the
risk of reporting noise as an improvement.

## 5. Alternating the runs

Recording all baseline runs first and all optimized runs afterwards makes the
comparison vulnerable to time related effects: the machine heats up, a
background service starts, the driver loads an update. The recommended
procedure is to alternate:

```
Baseline, Optimized, Baseline, Optimized, Baseline, Optimized, ...
```

FramePilot checks the order of the runs and adds a remark to the report when
the configurations were not alternated.

## 6. The metrics

All metrics are computed over the **presented** frames of the recording. A
frame that PresentMon reports as not displayed (`NA` in the display columns) is
counted separately and excluded from the frame rate.

| Metric | Definition |
| --- | --- |
| Duration | Sum of the frame times of the recording. |
| Average FPS | `presented frames / duration`. |
| Median frame time | 50th percentile of the frame times. |
| p95 / p99 frame time | 95th / 99th percentile of the frame times. |
| Frame time variation | Standard deviation of the frame times. |
| 1 % low | Mean of `1000 / frame time` over the frames whose frame time is at or above the 99th percentile of the recording. |
| 1 % high | Mean of `1000 / frame time` over the frames whose frame time is at or below the 1st percentile. |
| Unusually long frames | Frames that are slower than twice the median frame time of the recording. |

Percentiles use **linear interpolation between the two closest order
statistics** (the "inclusive" definition, the same as Excel's
`PERCENTILE.INC` and NumPy's default). With 100 frames the 99th percentile is
therefore `frame[98] + 0.01 * (frame[99] - frame[98])`.

The 1 % low is defined explicitly because there is no single standard: the
definition above is the one used by CapFrameX style tools, and stating it makes
the number reproducible.

## 7. Comparing two configurations

FramePilot compares the **medians of the per run values** (not the medians of
all frames of all runs, which would hide the run to run spread) and reports:

* the median of the per run average frame rate for both configurations,
* the relative change in percent,
* the same for the 1 % low,
* the run to run spread (inter quartile range divided by the median),
* the result of a two sided Mann-Whitney U test on the per run values.

The decision rules are:

| Situation | Result |
| --- | --- |
| Fewer than the required runs (default 5) | *Insufficient data* |
| Fewer than three usable runs per side (the minimum of the rank sum test) | *Inconclusive* |
| Difference is significant (p &lt; 0.05) and at least 2 % | *Measured improvement* / *Measured regression* |
| Difference is significant but below 2 % | *No reliable difference* (statistically visible, practically irrelevant) |
| Difference is at least 2 % but not significant | *Inconclusive* (the difference cannot be separated from the noise) |
| Difference is below 2 % and not significant | *No reliable difference* |

A successful optimization that does not change the frame rate ends up in the
last row. That is a valid result and FramePilot reports it as such.

## 8. What a result does not prove

A measured difference is an **observation of the recorded runs**. It is not
proof of causation, because the following can change between the runs:

* the driver version or a driver update in the background,
* the thermal state of the machine,
* other software that uses CPU, memory or GPU time,
* the order of the runs,
* the automatic quality settings of the game.

FramePilot states this in every report and never announces a proven frame rate
improvement.

## 9. Reproducing a result

1. Import the same CSV files into FramePilot.
2. Assign the same configuration names and warm-up flags.
3. Press **Compare the runs**.
4. The report contains every input value, so the result can be checked by
   hand.

The raw recordings stay in the folder that is shown in the settings, and the
Markdown report can be saved next to them.

## 10. Limitations

* FramePilot does not verify the *content* of a recording. A recording of the
  wrong scene is still analysed; only the frame times are checked.
* PresentMon measures the frames the application presents. Frames that are
  never presented (for example because the game is limited by a frame rate
  cap) are not part of the recording.
* The comparison is between two configurations of the *same* machine. It says
  nothing about other hardware.
* A recording is a sample. More runs reduce the uncertainty; they do not
  remove the biases listed above.
