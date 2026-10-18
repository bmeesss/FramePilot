namespace FramePilot.Core.PresentMon;

/// <summary>
/// The metrics FramePilot derives from a PresentMon recording. Every value is
/// computed from recorded frames only - FramePilot never estimates frame rates
/// from CPU or memory usage.
/// </summary>
public sealed record FrameStatistics
{
    /// <summary>All rows that were parsed from the recording.</summary>
    public int TotalFrameCount { get; init; }

    /// <summary>Frames that were actually presented to the display.</summary>
    public int PresentedFrameCount { get; init; }

    /// <summary>Frames that were dropped or never displayed.</summary>
    public int DroppedFrameCount { get; init; }

    /// <summary>Length of the recording in seconds (sum of the frame times).</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Average frames per second (presented frames / duration).</summary>
    public double AverageFps { get; init; }

    /// <summary>Median frame time in milliseconds.</summary>
    public double MedianFrameTimeMs { get; init; }

    /// <summary>95th percentile frame time in milliseconds.</summary>
    public double P95FrameTimeMs { get; init; }

    /// <summary>99th percentile frame time in milliseconds.</summary>
    public double P99FrameTimeMs { get; init; }

    /// <summary>Arithmetic mean of the frame times in milliseconds.</summary>
    public double MeanFrameTimeMs { get; init; }

    /// <summary>Standard deviation of the frame times in milliseconds.</summary>
    public double FrameTimeStdDevMs { get; init; }

    /// <summary>Lowest frame time in milliseconds.</summary>
    public double MinFrameTimeMs { get; init; }

    /// <summary>Highest frame time in milliseconds.</summary>
    public double MaxFrameTimeMs { get; init; }

    /// <summary>
    /// 1 % low: the average frame rate of the slowest 1 % of the frames.
    /// </summary>
    public double OnePercentLowFps { get; init; }

    /// <summary>
    /// 1 % high: the average frame rate of the fastest 1 % of the frames.
    /// </summary>
    public double OnePercentHighFps { get; init; }

    /// <summary>Frame time above which a frame counts as "unusually long" (2x the median).</summary>
    public double LongFrameThresholdMs { get; init; }

    /// <summary>Number of frames above <see cref="LongFrameThresholdMs"/>.</summary>
    public int LongFrameCount { get; init; }

    /// <summary>The longest frames of the recording, worst first.</summary>
    public IReadOnlyList<FrameTimeOutlier> LongFrames { get; init; } = Array.Empty<FrameTimeOutlier>();

    /// <summary>False when the recording did not contain a single presented frame.</summary>
    public bool HasUsableFrames => PresentedFrameCount > 0;
}

/// <summary>A single unusually long frame.</summary>
public sealed record FrameTimeOutlier(int RowIndex, double FrameTimeMs, double Fps);
