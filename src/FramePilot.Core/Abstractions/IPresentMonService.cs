namespace FramePilot.Core.Abstractions;

/// <summary>
/// Locates and runs the (user supplied) PresentMon executable. PresentMon is
/// never bundled with FramePilot.
/// </summary>
public interface IPresentMonService
{
    /// <summary>Information about the PresentMon executable that was found, if any.</summary>
    PresentMonLocation? Location { get; }

    /// <summary>Re-scans the configured locations for a PresentMon executable.</summary>
    Task<PresentMonLocation?> RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts a capture and returns a handle to the running capture.</summary>
    Task<IPresentMonCapture> StartCaptureAsync(PresentMonCaptureRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Location and version of a PresentMon executable.</summary>
public sealed record PresentMonLocation(string ExecutablePath, string? Version)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Version)
        ? Path.GetFileName(ExecutablePath)
        : $"PresentMon {Version}";
}

/// <summary>Parameters of a PresentMon capture.</summary>
public sealed record PresentMonCaptureRequest(
    string OutputFilePath,
    string? ProcessName,
    int? ProcessId,
    TimeSpan Duration);

/// <summary>A running PresentMon capture.</summary>
public interface IPresentMonCapture : IDisposable
{
    /// <summary>Path of the CSV file that is being written.</summary>
    string OutputFilePath { get; }

    /// <summary>True while the PresentMon process is still running.</summary>
    bool IsRunning { get; }

    /// <summary>
    /// True when the capture had to be ended by force, which can mean that the
    /// last frame of the recording is missing.
    /// </summary>
    bool WasStoppedForcibly { get; }

    /// <summary>Waits for the capture to finish. Returns false when the wait was cancelled.</summary>
    Task<bool> WaitForCompletionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Requests a graceful stop. The CSV file is closed cleanly whenever the
    /// PresentMon process supports a console close event.
    /// </summary>
    Task RequestStopAsync(CancellationToken cancellationToken = default);
}
