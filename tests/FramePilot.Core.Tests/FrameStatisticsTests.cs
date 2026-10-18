using FramePilot.Core.Metrics;
using FramePilot.Core.PresentMon;

namespace FramePilot.Core.Tests;

public class FrameStatisticsTests
{
    [Fact]
    public void Calculate_ConstantFrameTime_ProducesExpectedValues()
    {
        var frames = CreateFrames(Enumerable.Repeat(10.0, 100));

        var statistics = FrameStatisticsCalculator.Calculate(frames);

        Assert.Equal(100, statistics.PresentedFrameCount);
        Assert.Equal(1.0, statistics.DurationSeconds, 6);
        Assert.Equal(100.0, statistics.AverageFps, 6);
        Assert.Equal(10.0, statistics.MedianFrameTimeMs, 6);
        Assert.Equal(10.0, statistics.P95FrameTimeMs, 6);
        Assert.Equal(10.0, statistics.P99FrameTimeMs, 6);
        Assert.Equal(100.0, statistics.OnePercentLowFps, 6);
        Assert.Equal(0, statistics.LongFrameCount);
    }

    [Fact]
    public void Calculate_WithSingleOutlier_ReportsLowOnePercent()
    {
        var times = Enumerable.Repeat(10.0, 99).Append(100.0).ToArray();

        var statistics = FrameStatisticsCalculator.Calculate(CreateFrames(times));

        // The 99th percentile is 10.9 ms, so only the 100 ms frame is part of
        // the slowest 1 % -> 10 FPS.
        Assert.Equal(10.0, statistics.OnePercentLowFps, 6);
        Assert.Equal(100.0, statistics.MaxFrameTimeMs, 6);
        Assert.Equal(1, statistics.LongFrameCount);
    }

    [Fact]
    public void Calculate_ExcludesDroppedFramesFromTheFrameRate()
    {
        var frames = CreateFrames(new[] { 10.0, 10.0, 10.0, 500.0 });
        frames[3] = frames[3] with { IsDropped = true, WasDisplayed = false };

        var statistics = FrameStatisticsCalculator.Calculate(frames);

        Assert.Equal(3, statistics.PresentedFrameCount);
        Assert.Equal(1, statistics.DroppedFrameCount);
        Assert.Equal(100.0, statistics.AverageFps, 6);
    }

    [Fact]
    public void Calculate_WithoutPresentedFrames_IsNotUsable()
    {
        var frames = CreateFrames(new[] { 10.0 }).Select(f => f with { IsDropped = true, WasDisplayed = false }).ToArray();

        var statistics = FrameStatisticsCalculator.Calculate(frames);

        Assert.False(statistics.HasUsableFrames);
        Assert.Equal(0, statistics.AverageFps);
    }

    [Fact]
    public void Calculate_ReportsLongFrames()
    {
        var times = Enumerable.Repeat(10.0, 20).Append(80.0).ToArray();

        var statistics = FrameStatisticsCalculator.Calculate(CreateFrames(times));

        Assert.Equal(1, statistics.LongFrameCount);
        Assert.Equal(20.0, statistics.LongFrameThresholdMs, 6);
        Assert.Single(statistics.LongFrames);
        Assert.Equal(80.0, statistics.LongFrames[0].FrameTimeMs, 6);
    }

    [Fact]
    public void CalculateOnePercentLow_UsesTheSlowestFrames()
    {
        var frameTimes = new[] { 10.0, 10.0, 10.0, 10.0, 40.0 };
        var threshold = PercentileCalculator.Percentile(frameTimes, 99);

        var low = FrameStatisticsCalculator.CalculateOnePercentLow(frameTimes, threshold);

        Assert.Equal(25.0, low, 6);
    }

    private static List<PresentMonFrame> CreateFrames(IEnumerable<double> frameTimes)
    {
        var frames = new List<PresentMonFrame>();
        var index = 0;

        foreach (var frameTime in frameTimes)
        {
            frames.Add(new PresentMonFrame("game.exe", 1234, frameTime, WasDisplayed: true, IsDropped: false, index++));
        }

        return frames;
    }
}
