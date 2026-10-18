using FramePilot.Core.Metrics;

namespace FramePilot.Core.Benchmarks;

/// <summary>
/// Compares two benchmark configurations. The comparison only uses measured
/// PresentMon data and reports inconclusive results honestly.
/// </summary>
/// <remarks>
/// <para>The decision rules are:</para>
/// <list type="number">
/// <item><description>Fewer than <c>minimumRunsPerConfiguration</c> usable runs
/// on either side: <see cref="ComparisonVerdict.InsufficientData"/>.</description></item>
/// <item><description>Fewer than three usable runs per side (the sample size the
/// rank sum test needs): <see cref="ComparisonVerdict.Inconclusive"/>.</description></item>
/// <item><description>The difference is smaller than <c>minimumEffectPercent</c>:
/// <see cref="ComparisonVerdict.NoReliableDifference"/> - a measurable but
/// practically irrelevant difference is still reported as "no difference".</description></item>
/// <item><description>The difference is larger than the threshold but not
/// separable from the run to run noise (p &gt;= significance level):
/// <see cref="ComparisonVerdict.Inconclusive"/>.</description></item>
/// <item><description>Otherwise the difference is reported as a measured
/// improvement or regression - never as a proven cause.</description></item>
/// </list>
/// </remarks>
public sealed class BenchmarkComparer
{
    public const int DefaultMinimumRuns = 5;
    public const double DefaultMinimumEffectPercent = 2.0;
    public const double DefaultSignificanceLevel = 0.05;

    /// <summary>Compares the two configurations.</summary>
    public BenchmarkComparison Compare(
        IReadOnlyList<BenchmarkRun> runs,
        string baselineConfiguration,
        string optimizedConfiguration,
        int minimumRunsPerConfiguration = DefaultMinimumRuns,
        double minimumEffectPercent = DefaultMinimumEffectPercent,
        double significanceLevel = DefaultSignificanceLevel)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselineConfiguration);
        ArgumentException.ThrowIfNullOrWhiteSpace(optimizedConfiguration);

        var notes = new List<string>();
        var warmUpRuns = runs.Count(r => r.IsWarmUp);
        var usable = runs.Where(r => !r.IsWarmUp).ToList();

        var withoutFrames = usable.Count(r => !r.Statistics.HasUsableFrames);
        if (withoutFrames > 0)
        {
            notes.Add($"{withoutFrames} run(s) contained no presented frames and were excluded from the comparison.");
        }

        usable = usable.Where(r => r.Statistics.HasUsableFrames).ToList();

        var baselineRuns = usable
            .Where(r => string.Equals(r.Configuration, baselineConfiguration, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var optimizedRuns = usable
            .Where(r => string.Equals(r.Configuration, optimizedConfiguration, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (warmUpRuns > 0)
        {
            notes.Add($"{warmUpRuns} warm-up run(s) were excluded from the comparison.");
        }

        notes.Add("A measured difference is not proof of causation. Driver versions, background load, thermal state and the order of the runs can all influence the result.");

        if (baselineRuns.Count == 0 || optimizedRuns.Count == 0)
        {
            var missing = baselineRuns.Count == 0 ? baselineConfiguration : optimizedConfiguration;
            notes.Add("Use the same game, executable, graphics settings, scene or replay, resolution and test duration for every run.");

            return BuildInsufficientData(
                baselineConfiguration,
                optimizedConfiguration,
                baselineRuns,
                optimizedRuns,
                warmUpRuns,
                minimumRunsPerConfiguration,
                notes,
                $"There are no usable runs for '{missing}'. Record at least {minimumRunsPerConfiguration} runs of each configuration.");
        }

        if (baselineRuns.Count < minimumRunsPerConfiguration || optimizedRuns.Count < minimumRunsPerConfiguration)
        {
            var baselineMissing = Math.Max(0, minimumRunsPerConfiguration - baselineRuns.Count);
            var optimizedMissing = Math.Max(0, minimumRunsPerConfiguration - optimizedRuns.Count);

            var message = $"Not enough runs: '{baselineConfiguration}' has {baselineRuns.Count} " +
                          $"(needs {baselineMissing} more), '{optimizedConfiguration}' has {optimizedRuns.Count} " +
                          $"(needs {optimizedMissing} more). " +
                          "FramePilot does not report an improvement from an insufficient number of runs.";

            notes.Add("Use the same game, executable, graphics settings, scene or replay, resolution and test duration for every run.");
            notes.Add("Alternating baseline and optimized runs reduces the influence of time related effects such as thermal throttling.");

            return BuildInsufficientData(
                baselineConfiguration,
                optimizedConfiguration,
                baselineRuns,
                optimizedRuns,
                warmUpRuns,
                minimumRunsPerConfiguration,
                notes,
                message);
        }

        var baselineFps = baselineRuns.Select(r => r.Statistics.AverageFps).ToArray();
        var optimizedFps = optimizedRuns.Select(r => r.Statistics.AverageFps).ToArray();
        var baselineLow = baselineRuns.Select(r => r.Statistics.OnePercentLowFps).ToArray();
        var optimizedLow = optimizedRuns.Select(r => r.Statistics.OnePercentLowFps).ToArray();
        var baselineFrameTime = baselineRuns.Select(r => r.Statistics.MedianFrameTimeMs).ToArray();
        var optimizedFrameTime = optimizedRuns.Select(r => r.Statistics.MedianFrameTimeMs).ToArray();

        var baselineMedian = PercentileCalculator.Median(baselineFps);
        var optimizedMedian = PercentileCalculator.Median(optimizedFps);
        var baselineLowMedian = PercentileCalculator.Median(baselineLow);
        var optimizedLowMedian = PercentileCalculator.Median(optimizedLow);

        var deltaPercent = PercentChange(baselineMedian, optimizedMedian);
        var lowDeltaPercent = PercentChange(baselineLowMedian, optimizedLowMedian);

        var baselineVariation = VariationPercent(baselineFps, baselineMedian);
        var optimizedVariation = VariationPercent(optimizedFps, optimizedMedian);

        var pValue = MannWhitneyTest.CalculateTwoSidedPValue(baselineFps, optimizedFps);

        if (!AreRunsAlternating(runs))
        {
            notes.Add("The runs were not alternated between the configurations. A background task, a warming machine or a driver change during the session can bias the comparison.");
        }

        if (pValue is null)
        {
            notes.Add("The statistical test needs at least three usable runs per configuration; the result cannot be separated from the noise.");

            return new BenchmarkComparison
            {
                BaselineConfiguration = baselineConfiguration,
                OptimizedConfiguration = optimizedConfiguration,
                BaselineRuns = baselineRuns,
                OptimizedRuns = optimizedRuns,
                WarmUpRunsExcluded = warmUpRuns,
                MinimumRunsRequired = minimumRunsPerConfiguration,
                BaselineMedianAverageFps = baselineMedian,
                OptimizedMedianAverageFps = optimizedMedian,
                AverageFpsDeltaPercent = deltaPercent,
                BaselineMedianOnePercentLowFps = baselineLowMedian,
                OptimizedMedianOnePercentLowFps = optimizedLowMedian,
                OnePercentLowDeltaPercent = lowDeltaPercent,
                BaselineMedianFrameTimeMs = PercentileCalculator.Median(baselineFrameTime),
                OptimizedMedianFrameTimeMs = PercentileCalculator.Median(optimizedFrameTime),
                BaselineVariationPercent = baselineVariation,
                OptimizedVariationPercent = optimizedVariation,
                PValue = null,
                MinimumEffectPercent = minimumEffectPercent,
                Verdict = ComparisonVerdict.Inconclusive,
                Headline = "The result is inconclusive",
                Summary = "The recorded runs do not allow a decision. More runs of both configurations are needed.",
                Notes = notes,
            };
        }

        var significant = pValue < significanceLevel;
        var exceedsEffectSize = Math.Abs(deltaPercent) >= minimumEffectPercent;

        ComparisonVerdict verdict;
        string headline;
        string summary;

        if (significant && exceedsEffectSize)
        {
            if (deltaPercent > 0)
            {
                verdict = ComparisonVerdict.MeasuredImprovement;
                headline = $"Measured improvement of {deltaPercent:+0.0;-0.0;0.0} % median average FPS";
                summary = $"Across {baselineRuns.Count} baseline and {optimizedRuns.Count} optimized runs the median average frame rate changed by {deltaPercent:+0.0;-0.0;0.0} % " +
                          $"(from {baselineMedian:0.0} to {optimizedMedian:0.0} FPS, p = {pValue:0.000}). " +
                          "The difference is larger than the run to run noise of these runs. This is an observed difference, not proof that the change caused it.";
            }
            else
            {
                verdict = ComparisonVerdict.MeasuredRegression;
                headline = $"Measured regression of {Math.Abs(deltaPercent):0.0} % median average FPS";
                summary = $"Across {baselineRuns.Count} baseline and {optimizedRuns.Count} optimized runs the median average frame rate changed by {deltaPercent:+0.0;-0.0;0.0} % " +
                          $"(from {baselineMedian:0.0} to {optimizedMedian:0.0} FPS, p = {pValue:0.000}). " +
                          "The optimized configuration measured slower in these runs. This is an observed difference, not proof that the change caused it.";
            }
        }
        else if (significant)
        {
            verdict = ComparisonVerdict.NoReliableDifference;
            headline = "No practically relevant difference between the configurations";
            summary = $"The measured difference of {deltaPercent:+0.0;-0.0;0.0} % is smaller than the minimum effect size of {minimumEffectPercent:0.0} % " +
                      $"(p = {pValue:0.000}). The runs are separable statistically, but the difference is too small to be relevant in practice.";
        }
        else if (exceedsEffectSize)
        {
            verdict = ComparisonVerdict.Inconclusive;
            headline = "The result is inconclusive";
            summary = $"The difference of {deltaPercent:+0.0;-0.0;0.0} % is larger than the minimum effect size of {minimumEffectPercent:0.0} %, " +
                      $"but it cannot be separated from the run to run noise (p = {pValue:0.000}, run to run spread: " +
                      $"baseline {baselineVariation:0.0} %, optimized {optimizedVariation:0.0} %). More runs are needed.";
        }
        else
        {
            verdict = ComparisonVerdict.NoReliableDifference;
            headline = "No reliable difference between the configurations";
            summary = $"The median average frame rate changed by {deltaPercent:+0.0;-0.0;0.0} % (from {baselineMedian:0.0} to {optimizedMedian:0.0} FPS), " +
                      $"which is within the run to run spread of the recorded runs (baseline {baselineVariation:0.0} %, optimized {optimizedVariation:0.0} %, p = {pValue:0.000}). " +
                      "A successful optimisation with no measurable frame rate gain is a valid result.";
        }

        notes.Add($"1 % low: baseline {baselineLowMedian:0.0} FPS, optimized {optimizedLowMedian:0.0} FPS ({lowDeltaPercent:+0.0;-0.0;0.0} %).");

        return new BenchmarkComparison
        {
            BaselineConfiguration = baselineConfiguration,
            OptimizedConfiguration = optimizedConfiguration,
            BaselineRuns = baselineRuns,
            OptimizedRuns = optimizedRuns,
            WarmUpRunsExcluded = warmUpRuns,
            MinimumRunsRequired = minimumRunsPerConfiguration,
            BaselineMedianAverageFps = baselineMedian,
            OptimizedMedianAverageFps = optimizedMedian,
            AverageFpsDeltaPercent = deltaPercent,
            BaselineMedianOnePercentLowFps = baselineLowMedian,
            OptimizedMedianOnePercentLowFps = optimizedLowMedian,
            OnePercentLowDeltaPercent = lowDeltaPercent,
            BaselineMedianFrameTimeMs = PercentileCalculator.Median(baselineFrameTime),
            OptimizedMedianFrameTimeMs = PercentileCalculator.Median(optimizedFrameTime),
            BaselineVariationPercent = baselineVariation,
            OptimizedVariationPercent = optimizedVariation,
            PValue = pValue,
            MinimumEffectPercent = minimumEffectPercent,
            Verdict = verdict,
            Headline = headline,
            Summary = summary,
            Notes = notes,
        };
    }

    private static BenchmarkComparison BuildInsufficientData(
        string baselineConfiguration,
        string optimizedConfiguration,
        IReadOnlyList<BenchmarkRun> baselineRuns,
        IReadOnlyList<BenchmarkRun> optimizedRuns,
        int warmUpRuns,
        int minimumRuns,
        IReadOnlyList<string> notes,
        string message)
    {
        return new BenchmarkComparison
        {
            BaselineConfiguration = baselineConfiguration,
            OptimizedConfiguration = optimizedConfiguration,
            BaselineRuns = baselineRuns,
            OptimizedRuns = optimizedRuns,
            WarmUpRunsExcluded = warmUpRuns,
            MinimumRunsRequired = minimumRuns,
            MinimumEffectPercent = DefaultMinimumEffectPercent,
            Verdict = ComparisonVerdict.InsufficientData,
            Headline = "Not enough data to compare the configurations",
            Summary = message,
            Notes = notes,
        };
    }

    private static double PercentChange(double baseline, double optimized)
    {
        if (baseline <= 0)
        {
            return 0;
        }

        return (optimized - baseline) / baseline * 100.0;
    }

    private static double VariationPercent(IReadOnlyList<double> values, double median)
    {
        if (values.Count < 2 || median <= 0)
        {
            return 0;
        }

        var iqr = PercentileCalculator.InterQuartileRange(values);

        if (iqr is null)
        {
            return 0;
        }

        return iqr.Value / median * 100.0;
    }

    /// <summary>
    /// Returns false when the runs were not alternated between the two
    /// configurations (which makes the comparison vulnerable to time related
    /// effects).
    /// </summary>
    private static bool AreRunsAlternating(IReadOnlyList<BenchmarkRun> runs)
    {
        var ordered = runs
            .Where(r => !r.IsWarmUp)
            .OrderBy(r => r.RunOrder)
            .ToList();

        if (ordered.Count < 4)
        {
            // With fewer than four runs the question cannot be answered
            // meaningfully, so no warning is produced.
            return true;
        }

        var blocks = 1;
        for (var i = 1; i < ordered.Count; i++)
        {
            if (!string.Equals(ordered[i].Configuration, ordered[i - 1].Configuration, StringComparison.OrdinalIgnoreCase))
            {
                blocks++;
            }
        }

        return blocks >= ordered.Count - 1;
    }
}
