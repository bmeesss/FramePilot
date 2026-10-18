using FramePilot.Core.PresentMon;

namespace FramePilot.Core.Benchmarks;

/// <summary>
/// One recorded benchmark run: a single PresentMon CSV file together with the
/// metrics that were derived from it.
/// </summary>
public sealed record BenchmarkRun
{
    /// <summary>Stable identifier of the run.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// The configuration the run belongs to, e.g. "Baseline" or "Optimized".
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>Path of the CSV file the run was created from.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Name of the application that produced the frames.</summary>
    public required string Application { get; init; }

    /// <summary>The measured metrics of the run.</summary>
    public required FrameStatistics Statistics { get; init; }

    /// <summary>
    /// Warm-up runs are excluded from the comparison. A warm-up run is a run
    /// that is discarded before the measurement starts, for example the first
    /// run after a driver or shader cache rebuild.
    /// </summary>
    public bool IsWarmUp { get; init; }

    /// <summary>Position of the run inside the measurement session.</summary>
    public int RunOrder { get; init; }

    /// <summary>When the recording was made.</summary>
    public DateTimeOffset RecordedAt { get; init; }

    /// <summary>Name of the column the frame time was taken from.</summary>
    public string FrameTimeColumn { get; init; } = string.Empty;

    /// <summary>Meaning of the frame time column.</summary>
    public string FrameTimeDescription { get; init; } = string.Empty;

    /// <summary>Free form notes about this run.</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
