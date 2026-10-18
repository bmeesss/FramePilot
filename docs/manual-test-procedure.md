# Manual test procedure

The automated test suite covers the calculations, the parsing, the session
logic and the recovery journal. Everything that depends on the real machine -
switching a power plan, asking an application to close, recording with
PresentMon and the behaviour of the user interface - has to be checked by hand.

This document is the reproducible procedure for those checks. Run it on a
machine that is allowed to be modified, and note the results.

## 0. Preparation

* Windows 10 1809 or Windows 11, 64 bit.
* A user account that is allowed to switch power plans (a standard user can
  switch between the existing plans; `powercfg /setactive` on a plan that the
  user may not use fails with an access error).
* A game or any other DirectX application that can be started and closed.
* Optionally: [PresentMon](https://github.com/GameTechDev/PresentMon) 2.x.

Before the test, record the current state:

```powershell
powercfg /getactivescheme
powercfg /list
```

## 1. Dashboard

| Step | Action | Expected result |
| --- | --- | --- |
| 1.1 | Start FramePilot and open the **Dashboard**. | The CPU value shows *Unavailable* for the first second, then a percentage. |
| 1.2 | Compare the CPU value with the Task Manager. | Both values are within a few percent; FramePilot aggregates over all logical processors. |
| 1.3 | Compare the memory value with the Task Manager. | The same numbers, because both use the same Windows counters. The value is the memory of the machine. |
| 1.4 | Open a game and watch the process list. | The game appears with a CPU percentage that grows while it is busy. |
| 1.5 | Start a new process and look at it immediately. | It shows *Unavailable* for the CPU until the second sample. |
| 1.6 | Fill the disk and look at the storage card. | The free space matches the file manager. |
| 1.7 | Switch the power plan in the Windows settings and wait five seconds. | The dashboard shows the new plan. |

## 2. Game session

| Step | Action | Expected result |
| --- | --- | --- |
| 2.1 | Note the active plan (`powercfg /getactivescheme`). | The plan is listed. |
| 2.2 | Open **Game Sessions**, press **Reload the power plans** and select another plan. | Only the existing plans are offered. |
| 2.3 | Press **Start session** and confirm. | The confirmation mentions the plan that is switched and the risks. After the confirmation the new plan is active. |
| 2.4 | Run `powercfg /getactivescheme`. | The plan that was selected is active. |
| 2.5 | Press **End session**. | The original plan is active again. `powercfg /getactivescheme` confirms it. |
| 2.6 | Start a session with **Start the game when the session starts** enabled and a game executable. | The game starts, the plan is switched. |
| 2.7 | Close the game. | The original plan is restored automatically. |
| 2.8 | Start a session and press **Cancel**. | The plan is restored, the game keeps running. |
| 2.9 | Start a session and kill FramePilot with the task manager. | The journal file `%LOCALAPPDATA%\FramePilot\session-journal.json` contains the original and the applied plan. |
| 2.10 | Start FramePilot again. | The status bar reports that the interrupted session was recovered and the original plan is active. |
| 2.11 | Repeat 2.9, but change the power plan manually before starting FramePilot. | FramePilot reports that the plan was changed outside of FramePilot and **does not** overwrite the current plan. |
| 2.12 | Try to start a session with a plan that does not exist (edit the journal by hand). | FramePilot reports the error and leaves the machine in the previous state. |

## 3. Running processes

| Step | Action | Expected result |
| --- | --- | --- |
| 3.1 | Open **Running Processes**. | Only processes with a measurable CPU usage or more than 64 MB of memory are listed. |
| 3.2 | Select `explorer.exe` (or another system process) and press **Ask the application to close**. | FramePilot refuses and explains that the process must not be closed. |
| 3.3 | Select a normal application with a window (a text editor) and press **Ask the application to close**. | A confirmation appears with the reason and the warning. |
| 3.4 | Confirm. | The application receives a close message and closes. If it asks for a confirmation itself, it stays open and FramePilot says so. |
| 3.5 | Select a background process without a window. | FramePilot refuses because there is no window that could be closed. |
| 3.6 | Select a game that ignores the close message. | FramePilot reports that the application is still running; it is never killed. |

## 4. PresentMon recording and analysis

| Step | Action | Expected result |
| --- | --- | --- |
| 4.1 | Install PresentMon 2.x and put `PresentMon.exe` next to `FramePilot.exe` (or set the path in the settings). | The **Benchmarks** page shows the found path and the version. |
| 4.2 | Open the **Benchmarks** page and press **Start recording** without PresentMon installed. | FramePilot says that PresentMon is not available and offers the manual import. |
| 4.3 | Start a recording of 30 seconds while a game runs. | The status line shows that the recording is running, the CSV file appears in the capture folder. |
| 4.4 | Wait for the recording to end. | The recording is imported automatically and the run list shows the frame count, the average frame rate and the 1 % low. |
| 4.5 | Stop a recording early. | FramePilot asks PresentMon to shut down; the CSV file is complete. If it had to be ended by force, the status line says that the last frame can be missing. |
| 4.6 | Import a CSV file that was recorded with PresentMon manually. | The same values appear. |
| 4.7 | Import a file that is not a PresentMon recording (for example a text file). | FramePilot rejects it with an explanation and does not invent numbers. |
| 4.8 | Import a recording that was cut off (kill PresentMon manually). | The import succeeds, the last incomplete row is skipped and the truncation is reported. |

## 5. Benchmark comparison

| Step | Action | Expected result |
| --- | --- | --- |
| 5.1 | Import three baseline runs and three optimized runs. | The comparison reports *Insufficient data* and names how many runs are missing. |
| 5.2 | Add two more runs of each configuration. | The comparison reports a result. |
| 5.3 | Compare two configurations that are identical. | The result is *No reliable difference*. |
| 5.4 | Compare two configurations that clearly differ (for example a different resolution). | The result is a measured improvement or regression, with the p value and the spread. |
| 5.5 | Mark the first run as a warm-up run. | The run count drops by one and the report mentions the excluded warm-up run. |
| 5.6 | Press **Save the report**. | A Markdown file with the individual runs, the medians, the spread, the notes and the method is written. |

## 6. Publishing

| Step | Action | Expected result |
| --- | --- | --- |
| 6.1 | `dotnet publish src/FramePilot.App/FramePilot.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish` | `FramePilot.exe` is produced. |
| 6.2 | Copy the folder to a machine without .NET and start `FramePilot.exe`. | The application starts, the dashboard shows real values, and the power plan can be switched. |
| 6.3 | Check that no file outside `%LOCALAPPDATA%\FramePilot` is written. | Only the data folder contains files. |

## 7. Result template

```
Date:
Windows version:
Hardware:
FramePilot version:
1. Dashboard:        pass / fail   Notes:
2. Game session:     pass / fail   Notes:
3. Processes:        pass / fail   Notes:
4. PresentMon:       pass / fail   Notes:
5. Comparison:       pass / fail   Notes:
6. Publish:          pass / fail   Notes:
```
