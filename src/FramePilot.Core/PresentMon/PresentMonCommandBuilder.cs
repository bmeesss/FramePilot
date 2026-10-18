namespace FramePilot.Core.PresentMon;

/// <summary>
/// Options for a PresentMon capture. All of them map to documented PresentMon
/// command line switches.
/// </summary>
public sealed record PresentMonCaptureOptions
{
    /// <summary>Absolute path of the CSV file that PresentMon should write.</summary>
    public required string OutputFilePath { get; init; }

    /// <summary>Only record this process (the executable name, e.g. "game.exe").</summary>
    public string? ProcessName { get; init; }

    /// <summary>Only record this process id.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Stop the recording automatically after this duration.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Use a unique ETW session name (recommended).</summary>
    public string? SessionName { get; init; }

    /// <summary>Drop frames that were not displayed from the CSV.</summary>
    public bool ExcludeDropped { get; init; }

    /// <summary>Stop PresentMon when the recorded process exits.</summary>
    public bool TerminateOnProcessExit { get; init; }

    /// <summary>Do not collect GPU timings (fewer columns, marginally lower overhead).</summary>
    public bool NoTrackGpu { get; init; }

    /// <summary>Do not collect input latency information.</summary>
    public bool NoTrackInput { get; init; }

    /// <summary>Do not collect display information (requires the two options above).</summary>
    public bool NoTrackDisplay { get; init; }
}

/// <summary>
/// Builds the argument list for the PresentMon console application.
/// </summary>
/// <remarks>
/// The arguments are returned as a list and are passed to
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>, so no value
/// is ever concatenated into a command line string and no shell is involved.
/// </remarks>
public static class PresentMonCommandBuilder
{
    /// <summary>
    /// Builds the arguments for a capture. Only documented PresentMon switches
    /// are used.
    /// </summary>
    public static IReadOnlyList<string> BuildCaptureArguments(PresentMonCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.OutputFilePath))
        {
            throw new ArgumentException("An output file path is required.", nameof(options));
        }

        var arguments = new List<string>
        {
            "--output_file",
            options.OutputFilePath,
            "--no_console_stats",
        };

        if (!string.IsNullOrWhiteSpace(options.ProcessName))
        {
            arguments.Add("--process_name");
            arguments.Add(options.ProcessName);
        }

        if (options.ProcessId is > 0)
        {
            arguments.Add("--process_id");
            arguments.Add(options.ProcessId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(options.SessionName))
        {
            arguments.Add("--session_name");
            arguments.Add(options.SessionName);
        }

        if (options.Duration is { } duration && duration > TimeSpan.Zero)
        {
            var seconds = (int)Math.Ceiling(duration.TotalSeconds);
            arguments.Add("--timed");
            arguments.Add(seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            arguments.Add("--terminate_after_timed");
        }

        if (options.TerminateOnProcessExit)
        {
            arguments.Add("--terminate_on_proc_exit");
        }

        if (options.ExcludeDropped)
        {
            arguments.Add("--exclude_dropped");
        }

        if (options.NoTrackGpu)
        {
            arguments.Add("--no_track_gpu");
        }

        if (options.NoTrackInput)
        {
            arguments.Add("--no_track_input");
        }

        if (options.NoTrackDisplay)
        {
            // PresentMon requires the GPU and input tracking to be disabled as
            // well before display tracking can be switched off.
            arguments.Add("--no_track_display");
        }

        return arguments;
    }
}
