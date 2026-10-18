namespace FramePilot.Core.Benchmarks;

/// <summary>The outcome of comparing two benchmark configurations.</summary>
public enum ComparisonVerdict
{
    /// <summary>Not enough runs were recorded to say anything about the difference.</summary>
    InsufficientData = 0,

    /// <summary>
    /// The data is too noisy (or too little of it) to decide whether the
    /// configurations differ.
    /// </summary>
    Inconclusive = 1,

    /// <summary>
    /// The two configurations were not measurably different - this includes the
    /// case "the optimisation did not help".
    /// </summary>
    NoReliableDifference = 2,

    /// <summary>The optimized configuration measured higher frame rates.</summary>
    MeasuredImprovement = 3,

    /// <summary>The optimized configuration measured lower frame rates.</summary>
    MeasuredRegression = 4,
}

/// <summary>
/// The result of a before/after comparison. Every number in here comes from
/// recorded PresentMon data.
/// </summary>
public sealed record BenchmarkComparison
{
    public required string BaselineConfiguration { get; init; }

    public required string OptimizedConfiguration { get; init; }

    public required IReadOnlyList<BenchmarkRun> BaselineRuns { get; init; }

    public required IReadOnlyList<BenchmarkRun> OptimizedRuns { get; init; }

    /// <summary>Number of warm-up runs that were excluded.</summary>
    public int WarmUpRunsExcluded { get; init; }

    /// <summary>Number of runs that were required per configuration.</summary>
    public int MinimumRunsRequired { get; init; }

    public double BaselineMedianAverageFps { get; init; }

    public double OptimizedMedianAverageFps { get; init; }

    /// <summary>Relative change of the median average frame rate.</summary>
    public double AverageFpsDeltaPercent { get; init; }

    public double BaselineMedianOnePercentLowFps { get; init; }

    public double OptimizedMedianOnePercentLowFps { get; init; }

    /// <summary>Relative change of the median 1 % low.</summary>
    public double OnePercentLowDeltaPercent { get; init; }

    /// <summary>Median frame time of the baseline runs (milliseconds).</summary>
    public double BaselineMedianFrameTimeMs { get; init; }

    /// <summary>Median frame time of the optimized runs (milliseconds).</summary>
    public double OptimizedMedianFrameTimeMs { get; init; }

    /// <summary>Run to run spread of the baseline runs (IQR / median, in percent).</summary>
    public double BaselineVariationPercent { get; init; }

    /// <summary>Run to run spread of the optimized runs (IQR / median, in percent).</summary>
    public double OptimizedVariationPercent { get; init; }

    /// <summary>p value of the Mann-Whitney U test, if it could be computed.</summary>
    public double? PValue { get; init; }

    /// <summary>Smallest relative difference that is reported as a difference at all.</summary>
    public double MinimumEffectPercent { get; init; }

    public required ComparisonVerdict Verdict { get; init; }

    /// <summary>One line summary of the result.</summary>
    public required string Headline { get; init; }

    /// <summary>Detailed explanation of the result.</summary>
    public required string Summary { get; init; }

    /// <summary>Additional remarks (methodology, caveats, recommendations).</summary>
    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    /// <summary>True when the measurement supports a difference between the configurations.</summary>
    public bool ShowsReliableDifference =>
        Verdict is ComparisonVerdict.MeasuredImprovement or ComparisonVerdict.MeasuredRegression;
}
