using FramePilot.Core.Metrics;
using FramePilot.Core.Models;

namespace FramePilot.Core.Tests;

public class CpuLoadCalculatorTests
{
    [Fact]
    public void ComputeUtilization_WithoutPreviousSample_ReturnsNull()
    {
        var current = new ProcessorTimes(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        var result = CpuLoadCalculator.ComputeUtilization(null, current);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeUtilization_UsesBusyShareOfTotalTime()
    {
        // Kernel time contains the idle time.
        var previous = new ProcessorTimes(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        var current = new ProcessorTimes(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));

        var result = CpuLoadCalculator.ComputeUtilization(previous, current);

        Assert.NotNull(result);

        // total delta = 5 s, idle delta = 1 s -> 4 s busy of 5 s -> 80 %
        Assert.Equal(80.0, result!.Value, 6);
    }

    [Fact]
    public void ComputeUtilization_FullyIdle_ReturnsZero()
    {
        var previous = new ProcessorTimes(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.Zero);
        var current = new ProcessorTimes(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.Zero);

        var result = CpuLoadCalculator.ComputeUtilization(previous, current);

        Assert.NotNull(result);
        Assert.Equal(0.0, result!.Value, 6);
    }

    [Fact]
    public void ComputeUtilization_WithUnchangedCounters_ReturnsNull()
    {
        var sample = new ProcessorTimes(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        var result = CpuLoadCalculator.ComputeUtilization(sample, sample);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeUtilization_WithCounterReset_ReturnsNull()
    {
        var previous = new ProcessorTimes(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10));
        var current = new ProcessorTimes(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        var result = CpuLoadCalculator.ComputeUtilization(previous, current);

        Assert.Null(result);
    }

    [Fact]
    public void ComputeUtilization_ClampsToHundredPercent()
    {
        var previous = new ProcessorTimes(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1), TimeSpan.Zero);
        var current = new ProcessorTimes(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2), TimeSpan.Zero);

        var result = CpuLoadCalculator.ComputeUtilization(previous, current);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, 6);
    }

    [Fact]
    public void ComputeTotalCoreUtilization_ScalesWithProcessorCount()
    {
        var result = CpuLoadCalculator.ComputeTotalCoreUtilization(50.0, 8);

        Assert.NotNull(result);
        Assert.Equal(400.0, result!.Value, 6);
    }

    [Fact]
    public void ComputeTotalCoreUtilization_IsClampedToTheTotalCapacity()
    {
        var result = CpuLoadCalculator.ComputeTotalCoreUtilization(100.0, 8);

        Assert.NotNull(result);
        Assert.Equal(800.0, result!.Value, 6);
    }

    [Fact]
    public void ComputeTotalCoreUtilization_WithoutUtilization_ReturnsNull()
    {
        Assert.Null(CpuLoadCalculator.ComputeTotalCoreUtilization(null, 8));
    }
}
