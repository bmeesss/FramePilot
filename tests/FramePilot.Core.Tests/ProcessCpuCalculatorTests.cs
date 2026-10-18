using FramePilot.Core.Metrics;

namespace FramePilot.Core.Tests;

public class ProcessCpuCalculatorTests
{
    [Fact]
    public void ComputePercent_NormalisesByLogicalProcessors()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromSeconds(1),
            4);

        Assert.NotNull(result);
        Assert.Equal(12.5, result!.Value, 6);
    }

    [Fact]
    public void ComputePercent_SingleCoreCanReachHundredPercent()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            1);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, 6);
    }

    [Fact]
    public void ComputePercent_WithoutPreviousSample_ReturnsNull()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            (TimeSpan?)null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            8);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_WhenProcessRestarted_ReturnsNull()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            8);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_WithoutElapsedTime_ReturnsNull()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero,
            8);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_WithoutProcessorCount_ReturnsNull()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1),
            0);

        Assert.Null(result);
    }

    [Fact]
    public void ComputePercent_ClampsToHundredPercent()
    {
        var result = ProcessCpuCalculator.ComputePercent(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(1),
            1);

        Assert.NotNull(result);
        Assert.Equal(100.0, result!.Value, 6);
    }
}
