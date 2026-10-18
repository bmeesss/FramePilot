using FramePilot.Core.Metrics;

namespace FramePilot.Core.Tests;

public class MemoryCalculatorTests
{
    [Fact]
    public void Compute_CalculatesUsedAndAvailable()
    {
        var snapshot = MemoryCalculator.Compute(16L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(16L * 1024 * 1024 * 1024, snapshot.TotalBytes);
        Assert.Equal(4L * 1024 * 1024 * 1024, snapshot.AvailableBytes);
        Assert.Equal(12L * 1024 * 1024 * 1024, snapshot.UsedBytes);
        Assert.Equal(75.0, snapshot.UsedPercent, 6);
    }

    [Fact]
    public void Compute_WithoutTotalMemory_IsUnavailable()
    {
        var snapshot = MemoryCalculator.Compute(0, 0);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal(0, snapshot.UsedPercent);
    }

    [Fact]
    public void Compute_ClampsAvailableMemoryToTotal()
    {
        var snapshot = MemoryCalculator.Compute(1000, 5000);

        Assert.Equal(1000, snapshot.AvailableBytes);
        Assert.Equal(0, snapshot.UsedBytes);
        Assert.Equal(0, snapshot.UsedPercent, 6);
    }

    [Fact]
    public void Compute_ClampsNegativeAvailableMemory()
    {
        var snapshot = MemoryCalculator.Compute(1000, -10);

        Assert.Equal(0, snapshot.AvailableBytes);
        Assert.Equal(1000, snapshot.UsedBytes);
        Assert.Equal(100.0, snapshot.UsedPercent, 6);
    }
}
