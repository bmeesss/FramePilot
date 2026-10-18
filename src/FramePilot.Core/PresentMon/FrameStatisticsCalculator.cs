using FramePilot.Core.Metrics;

namespace FramePilot.Core.PresentMon;

/// <summary>
/// Derives frame performance metrics from the frames of a PresentMon
/// recording.
/// </summary>
/// <remarks>
/// <para><b>Average FPS</b> is <c>presented frames / duration</c> where the
/// duration is the sum of the recorded frame times.</para>
/// <para><b>1 % low</b> is the average frame rate of the slowest 1 % of the
/// frames: the 99th percentile of the frame times is used as the threshold and
/// the mean of <c>1000 / frame time</c> over all frames at or above that
/// threshold is reported. This is the definition used by CapFrameX-style
/// tools and it is stated explicitly so the number can be reproduced.</para>
/// <para><b>Unusually long frames</b> are frames that are slower than twice
/// the median frame time of the same recording.</para>
/// </remarks>
public static class FrameStatisticsCalculator
{
    private const int MaxReportedOutliers = 10;

    /// <summary>Calculates the statistics of a recording.</summary>
    public static FrameStatistics Calculate(IReadOnlyList<PresentMonFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var presented = frames
            .Where(f => f.WasDisplayed && !f.IsDropped)
            .ToArray();

        var dropped = frames.Count - presented.Length;

        if (presented.Length == 0)
        {
            return new FrameStatistics
            {
                TotalFrameCount = frames.Count,
                PresentedFrameCount = 0,
                DroppedFrameCount = dropped,
            };
        }

        var frameTimes = presented.Select(f => f.FrameTimeMs).ToArray();
        var durationSeconds = frameTimes.Sum() / 1000.0;
        var averageFps = durationSeconds > 0 ? presented.Length / durationSeconds : 0;

        var median = PercentileCalculator.Median(frameTimes);
        var p95 = PercentileCalculator.Percentile(frameTimes, 95);
        var p99 = PercentileCalculator.Percentile(frameTimes, 99);

        var longThreshold = median * 2.0;
        var longFrames = frameTimes
            .Select((value, index) => new { Value = value, Index = index })
            .Where(x => x.Value > longThreshold)
            .OrderByDescending(x => x.Value)
            .ToArray();

        var outliers = longFrames
            .Take(MaxReportedOutliers)
            .Select(x => new FrameTimeOutlier(presented[x.Index].RowIndex, x.Value, 1000.0 / x.Value))
            .ToArray();

        return new FrameStatistics
        {
            TotalFrameCount = frames.Count,
            PresentedFrameCount = presented.Length,
            DroppedFrameCount = dropped,
            DurationSeconds = durationSeconds,
            AverageFps = averageFps,
            MedianFrameTimeMs = median,
            P95FrameTimeMs = p95,
            P99FrameTimeMs = p99,
            MeanFrameTimeMs = PercentileCalculator.Mean(frameTimes),
            FrameTimeStdDevMs = PercentileCalculator.StandardDeviation(frameTimes) ?? 0,
            MinFrameTimeMs = frameTimes.Min(),
            MaxFrameTimeMs = frameTimes.Max(),
            OnePercentLowFps = CalculateOnePercentLow(frameTimes, p99),
            OnePercentHighFps = CalculateOnePercentHigh(frameTimes),
            LongFrameThresholdMs = longThreshold,
            LongFrameCount = longFrames.Length,
            LongFrames = outliers,
        };
    }

    /// <summary>
    /// Average frame rate of the slowest 1 % of the frames.
    /// </summary>
    public static double CalculateOnePercentLow(IReadOnlyList<double> frameTimes, double p99Threshold)
    {
        ArgumentNullException.ThrowIfNull(frameTimes);

        if (frameTimes.Count == 0)
        {
            return 0;
        }

        var worst = frameTimes.Where(t => t >= p99Threshold).ToArray();

        if (worst.Length == 0)
        {
            worst = new[] { frameTimes.Max() };
        }

        return worst.Average(t => 1000.0 / t);
    }

    private static double CalculateOnePercentHigh(IReadOnlyList<double> frameTimes)
    {
        if (frameTimes.Count == 0)
        {
            return 0;
        }

        var p1 = PercentileCalculator.Percentile(frameTimes, 1);
        var best = frameTimes.Where(t => t <= p1).ToArray();

        if (best.Length == 0)
        {
            best = new[] { frameTimes.Min() };
        }

        return best.Average(t => 1000.0 / t);
    }
}
