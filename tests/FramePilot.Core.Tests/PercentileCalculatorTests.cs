using FramePilot.Core.Metrics;

namespace FramePilot.Core.Tests;

public class PercentileCalculatorTests
{
    [Fact]
    public void Percentile_InterpolatesBetweenOrderStatistics()
    {
        var values = new[] { 1.0, 2.0, 3.0, 4.0 };

        Assert.Equal(2.5, PercentileCalculator.Percentile(values, 50), 6);
        Assert.Equal(3.25, PercentileCalculator.Percentile(values, 75), 6);
        Assert.Equal(1.0, PercentileCalculator.Percentile(values, 0), 6);
        Assert.Equal(4.0, PercentileCalculator.Percentile(values, 100), 6);
    }

    [Fact]
    public void Percentile_WithSingleValue_ReturnsThatValue()
    {
        Assert.Equal(16.0, PercentileCalculator.Percentile(new[] { 16.0 }, 99), 6);
    }

    [Fact]
    public void Percentile_WithoutValues_Throws()
    {
        Assert.Throws<ArgumentException>(() => PercentileCalculator.Percentile(Array.Empty<double>(), 50));
    }

    [Fact]
    public void Percentile_WithInvalidPercentile_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCalculator.Percentile(new[] { 1.0 }, 101));
        Assert.Throws<ArgumentOutOfRangeException>(() => PercentileCalculator.Percentile(new[] { 1.0 }, -1));
    }

    [Fact]
    public void Mean_CalculatesArithmeticMean()
    {
        Assert.Equal(2.0, PercentileCalculator.Mean(new[] { 1.0, 2.0, 3.0 }), 6);
    }

    [Fact]
    public void StandardDeviation_UsesSampleCorrection()
    {
        var stdDev = PercentileCalculator.StandardDeviation(new[] { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 });

        Assert.NotNull(stdDev);
        Assert.Equal(2.13809, stdDev!.Value, 4);
    }

    [Fact]
    public void StandardDeviation_WithOneValue_ReturnsNull()
    {
        Assert.Null(PercentileCalculator.StandardDeviation(new[] { 1.0 }));
    }

    [Fact]
    public void InterQuartileRange_CalculatesSpread()
    {
        var iqr = PercentileCalculator.InterQuartileRange(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 });

        Assert.NotNull(iqr);
        Assert.Equal(2.0, iqr!.Value, 6);
    }

    [Fact]
    public void MedianAbsoluteDeviation_IsRobustAgainstOutliers()
    {
        var mad = PercentileCalculator.MedianAbsoluteDeviation(new[] { 1.0, 1.0, 1.0, 1.0, 100.0 });

        Assert.NotNull(mad);
        Assert.Equal(0.0, mad!.Value, 6);
    }
}
