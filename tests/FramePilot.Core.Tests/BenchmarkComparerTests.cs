using FramePilot.Core.Benchmarks;
using FramePilot.Core.PresentMon;

namespace FramePilot.Core.Tests;

public class BenchmarkComparerTests
{
    [Fact]
    public void Compare_WithTooFewRuns_ReportsInsufficientData()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 3; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
            runs.Add(CreateRun("Optimized", 120, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.InsufficientData, comparison.Verdict);
        Assert.False(comparison.ShowsReliableDifference);
        Assert.Contains("Not enough runs", comparison.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_WithClearImprovement_ReportsMeasuredImprovement()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
            runs.Add(CreateRun("Optimized", 130, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.MeasuredImprovement, comparison.Verdict);
        Assert.True(comparison.ShowsReliableDifference);
        Assert.Equal(100.0, comparison.BaselineMedianAverageFps, 6);
        Assert.Equal(130.0, comparison.OptimizedMedianAverageFps, 6);
        Assert.Equal(30.0, comparison.AverageFpsDeltaPercent, 6);
        Assert.NotNull(comparison.PValue);
        Assert.Contains("not proof", comparison.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_WithClearRegression_ReportsMeasuredRegression()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 130, i));
            runs.Add(CreateRun("Optimized", 100, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.MeasuredRegression, comparison.Verdict);
        Assert.True(comparison.AverageFpsDeltaPercent < 0);
    }

    [Fact]
    public void Compare_WithIdenticalResults_ReportsNoReliableDifference()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
            runs.Add(CreateRun("Optimized", 100, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.NoReliableDifference, comparison.Verdict);
        Assert.Equal(0.0, comparison.AverageFpsDeltaPercent, 6);
    }

    [Fact]
    public void Compare_WithNoisyResults_ReportsInconclusive()
    {
        var baseline = new[] { 100.0, 120.0, 100.0, 120.0, 100.0 };
        var optimized = new[] { 110.0, 110.0, 110.0, 130.0, 100.0 };

        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < baseline.Length; i++)
        {
            runs.Add(CreateRun("Baseline", baseline[i], i));
            runs.Add(CreateRun("Optimized", optimized[i], i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.Inconclusive, comparison.Verdict);
    }

    [Fact]
    public void Compare_ExcludesWarmUpRuns()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
            runs.Add(CreateRun("Optimized", 130, i));
        }

        runs.Add(CreateRun("Baseline", 5, 99) with { IsWarmUp = true });
        runs.Add(CreateRun("Optimized", 5, 99) with { IsWarmUp = true });

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(2, comparison.WarmUpRunsExcluded);
        Assert.Equal(5, comparison.BaselineRuns.Count);
        Assert.Equal(5, comparison.OptimizedRuns.Count);
        Assert.Equal(ComparisonVerdict.MeasuredImprovement, comparison.Verdict);
    }

    [Fact]
    public void Compare_WithoutAnyRuns_ReportsInsufficientData()
    {
        var comparison = new BenchmarkComparer().Compare(Array.Empty<BenchmarkRun>(), "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.InsufficientData, comparison.Verdict);
    }

    [Fact]
    public void Compare_WithoutOneConfiguration_ReportsInsufficientData()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");

        Assert.Equal(ComparisonVerdict.InsufficientData, comparison.Verdict);
        Assert.Contains("no usable runs", comparison.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportWriter_ContainsMeasuredValuesOnly()
    {
        var runs = new List<BenchmarkRun>();
        for (var i = 0; i < 5; i++)
        {
            runs.Add(CreateRun("Baseline", 100, i));
            runs.Add(CreateRun("Optimized", 130, i));
        }

        var comparison = new BenchmarkComparer().Compare(runs, "Baseline", "Optimized");
        var markdown = BenchmarkReportWriter.WriteMarkdown(comparison, DateTimeOffset.UtcNow);

        Assert.Contains("Measured improvement", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100.0", markdown, StringComparison.Ordinal);
        Assert.Contains("130.0", markdown, StringComparison.Ordinal);
        Assert.Contains("not proof", markdown, StringComparison.OrdinalIgnoreCase);
    }

    private static BenchmarkRun CreateRun(string configuration, double averageFps, int order)
    {
        var frameTime = 1000.0 / averageFps;
        var frames = new List<PresentMonFrame>();

        for (var i = 0; i < 100; i++)
        {
            frames.Add(new PresentMonFrame("game.exe", 1234, frameTime, WasDisplayed: true, IsDropped: false, i));
        }

        return new BenchmarkRun
        {
            Id = $"{configuration}-{order}",
            Configuration = configuration,
            SourcePath = $@"C:\captures\{configuration}-{order}.csv",
            Application = "game.exe",
            Statistics = FrameStatisticsCalculator.Calculate(frames),
            RunOrder = order,
        };
    }
}
