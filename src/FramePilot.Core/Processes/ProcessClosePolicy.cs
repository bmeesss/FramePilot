using FramePilot.Core.Models;

namespace FramePilot.Core.Processes;

/// <summary>What FramePilot is allowed to do with a process.</summary>
public enum ProcessCloseVerdict
{
    /// <summary>The process may be closed after the user confirmed.</summary>
    RequiresConfirmation = 0,

    /// <summary>The process must not be closed by FramePilot at all.</summary>
    Blocked = 1,
}

/// <summary>The result of the safety evaluation for one process.</summary>
public sealed record ProcessCloseDecision(ProcessCloseVerdict Verdict, string Reason, string? Warning = null)
{
    public bool IsBlocked => Verdict == ProcessCloseVerdict.Blocked;
}

/// <summary>
/// Decides whether a process may be closed through a graceful close request.
/// </summary>
/// <remarks>
/// <para>
/// FramePilot never force-terminates a process. System, security, driver and
/// essential Windows processes are blocked, and processes that run from the
/// Windows directory are blocked as well.
/// </para>
/// <para>
/// Closing a background application only helps the frame rate when that
/// application is actually consuming a resource the game needs. The decision
/// therefore always carries that explanation.
/// </para>
/// </remarks>
public static class ProcessClosePolicy
{
    /// <summary>
    /// Base names of processes that must never be closed by FramePilot.
    /// </summary>
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss",
        "wininit",
        "winlogon",
        "services",
        "lsass",
        "svchost",
        "dwm",
        "smss",
        "lsm",
        "fontdrvhost",
        "spoolsv",
        "securityhealthservice",
        "securityhealthsystray",
        "msmpeng",
        "mpssvc",
        "wuauclt",
        "tiworker",
        "trustedinstaller",
        "audiodg",
        "rdpclip",
        "conhost",
        "openconsole",
        "system",
        "registry",
        "memory compression",
        "vpnclient",
    };

    /// <summary>Evaluates a process.</summary>
    /// <param name="process">The process the user selected.</param>
    /// <param name="windowsDirectory">
    /// The Windows directory, used to recognise operating system binaries.
    /// </param>
    public static ProcessCloseDecision Evaluate(ProcessSnapshot process, string? windowsDirectory)
    {
        ArgumentNullException.ThrowIfNull(process);

        var baseName = GetBaseName(process.Name);

        if (ProtectedProcessNames.Contains(baseName))
        {
            return new ProcessCloseDecision(
                ProcessCloseVerdict.Blocked,
                $"'{process.Name}' is a system, driver or security process and must not be closed.");
        }

        if (IsUnderWindowsDirectory(process.ExecutablePath, windowsDirectory))
        {
            return new ProcessCloseDecision(
                ProcessCloseVerdict.Blocked,
                $"'{process.Name}' runs from the Windows directory and is treated as an operating system process.");
        }

        if (!process.HasMainWindow)
        {
            return new ProcessCloseDecision(
                ProcessCloseVerdict.Blocked,
                $"'{process.Name}' has no window that could be closed. FramePilot does not force-terminate processes.");
        }

        var warning =
            "Closing an application only improves the frame rate when that application is currently using a " +
            "resource the game needs (CPU time, memory or GPU work). Measure first, then close.";

        if (string.Equals(baseName, "explorer", StringComparison.OrdinalIgnoreCase))
        {
            warning = "Closing the Windows shell ends the current desktop session. Make sure nothing important is running.";
        }

        return new ProcessCloseDecision(
            ProcessCloseVerdict.RequiresConfirmation,
            $"'{process.Name}' can be asked to close. The application decides whether it closes.",
            warning);
    }

    /// <summary>Removes the file extension from a process name.</summary>
    public static string GetBaseName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        var name = processName.Trim();

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name;
    }

    private static bool IsUnderWindowsDirectory(string? executablePath, string? windowsDirectory)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(windowsDirectory))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(executablePath);
            var fullWindows = Path.GetFullPath(windowsDirectory);

            return fullPath.StartsWith(fullWindows, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
