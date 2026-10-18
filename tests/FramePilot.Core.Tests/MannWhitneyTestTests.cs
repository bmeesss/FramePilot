using FramePilot.Core.Metrics;

namespace FramePilot.Core.Tests;

public class MannWhitneyTestTests
{
    [Fact]
    public void CalculateTwoSidedPValue_WithIdenticalSamples_ReturnsOne()
    {
        var p = MannWhitneyTest.CalculateTwoSidedPValue(
            new[] { 100.0, 100.0, 100.0, 100.0, 100.0 },
            new[] { 100.0, 100.0, 100.0, 100.0, 100.0 });

        Assert.NotNull(p);
        Assert.Equal(1.0, p!.Value, 6);
    }

    [Fact]
    public void CalculateTwoSidedPValue_WithSeparatedSamples_IsSmall()
    {
        var p = MannWhitneyTest.CalculateTwoSidedPValue(
            new[] { 100.0, 101.0, 99.0, 100.0, 98.0 },
            new[] { 130.0, 131.0, 129.0, 130.0, 128.0 });

        Assert.NotNull(p);
        Assert.True(p < 0.05, $"expected a small p value but got {p}");
    }

    [Fact]
    public void CalculateTwoSidedPValue_WithTooFewSamples_ReturnsNull()
    {
        var p = MannWhitneyTest.CalculateTwoSidedPValue(
            new[] { 100.0, 101.0 },
            new[] { 130.0, 131.0 });

        Assert.Null(p);
    }

    [Fact]
    public void CalculateTwoSidedPValue_WithOverlappingSamples_IsNotSignificant()
    {
        var p = MannWhitneyTest.CalculateTwoSidedPValue(
            new[] { 100.0, 120.0, 100.0, 120.0, 100.0 },
            new[] { 110.0, 110.0, 110.0, 130.0, 100.0 });

        Assert.NotNull(p);
        Assert.True(p > 0.05, $"expected a large p value but got {p}");
    }

    [Fact]
    public void NormalCumulative_MatchesKnownValues()
    {
        Assert.Equal(0.5, MannWhitneyTest.NormalCumulative(0), 6);
        Assert.Equal(0.8413447, MannWhitneyTest.NormalCumulative(1), 5);
        Assert.Equal(0.0227501, MannWhitneyTest.NormalCumulative(-2), 5);
    }
}
