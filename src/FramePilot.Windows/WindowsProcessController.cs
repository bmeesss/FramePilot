using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;

namespace FramePilot.Windows;

/// <summary>
/// Enumerates processes and asks them to close.
/// </summary>
/// <remarks>
/// <para>
/// Only a graceful close is ever requested: a <c>WM_CLOSE</c> message is sent
/// to the windows of the selected process. FramePilot never calls
/// <c>Process.Kill</c> and never terminates a process by force.
/// </para>
/// <para>
/// Applications that show a confirmation dialog, that are busy or that keep a
/// background process alive simply stay open - the result reports that.
/// </para>
/// </remarks>
public sealed class WindowsProcessController : IProcessController
{
    private const uint WmClose = 0x0010;

    public async Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ProcessSnapshot>();

        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception)
        {
            return result;
        }

        foreach (var process in processes)
        {
            try
            {
                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception)
                {
                    // Access denied for protected processes.
                }

                result.Add(new ProcessSnapshot(
                    ProcessId: process.Id,
                    Name: process.ProcessName,
                    ExecutablePath: path,
                    CpuPercent: null,
                    WorkingSetBytes: process.WorkingSet64,
                    PrivateBytes: process.PrivateMemorySize64,
                    HasMainWindow: process.MainWindowHandle != IntPtr.Zero,
                    IsSystemProcess: IsSystemProcess(path)));
            }
            catch (Exception)
            {
                // The process exited while it was inspected.
            }
            finally
            {
                process.Dispose();
            }
        }

        return result;
    }

    public async Task<ProcessCloseResult> RequestGracefulCloseAsync(int processId, CancellationToken cancellationToken = default)
    {
        Process process;

        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return ProcessCloseResult.Refused("The process has already exited.");
        }
        catch (InvalidOperationException)
        {
            return ProcessCloseResult.Refused("The process could not be opened.");
        }

        using (process)
        {
            try
            {
                if (process.HasExited)
                {
                    return new ProcessCloseResult(false, true, "The process has already exited.");
                }

                var accepted = false;

                try
                {
                    accepted = process.CloseMainWindow();
                }
                catch (Exception)
                {
                    accepted = false;
                }

                if (!accepted)
                {
                    accepted = PostCloseToWindows(processId);
                }

                if (!accepted)
                {
                    return ProcessCloseResult.Refused(
                        "The application did not accept a close request. It has no window that can be closed, " +
                        "FramePilot does not terminate processes by force.");
                }

                var exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                if (exited)
                {
                    return new ProcessCloseResult(true, true, "The application was closed.");
                }

                return new ProcessCloseResult(
                    true,
                    false,
                    "The close request was delivered. The application is still running - it may ask for a confirmation " +
                    "or keep a background process alive.");
            }
            catch (Exception ex)
            {
                return ProcessCloseResult.Refused("The close request failed: " + ex.Message);
            }
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Posts a <c>WM_CLOSE</c> message to every top level window of the
    /// process. Returns true when at least one message was posted.
    /// </summary>
    private static bool PostCloseToWindows(int processId)
    {
        var posted = false;

        EnumWindows((handle, _) =>
        {
            try
            {
                GetWindowThreadProcessId(handle, out var owner);
                if (owner != (uint)processId)
                {
                    return true;
                }

                if (!IsWindowVisible(handle))
                {
                    return true;
                }

                if (PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero))
                {
                    posted = true;
                }
            }
            catch (Exception)
            {
                // Ignore windows that disappear while they are inspected.
            }

            return true;
        }, IntPtr.Zero);

        return posted;
    }

    private static bool IsSystemProcess(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return !string.IsNullOrEmpty(windows) &&
               executablePath.StartsWith(windows, StringComparison.OrdinalIgnoreCase);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
}
