using System.Globalization;
using System.Text;

namespace FramePilot.Core.Benchmarks;

/// <summary>
/// Renders a benchmark comparison as a Markdown report. The report only
/// contains measured values; it never contains an estimated frame rate.
/// </summary>
public static class BenchmarkReportWriter
{
    public static string WriteMarkdown(BenchmarkComparison comparison, DateTimeOffset generatedAtUtc, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        var sb = new StringBuilder();

        sb.AppendLine("# FramePilot benchmark report");
        sb.AppendLine();
        sb.AppendLine($"* Generated: {generatedAtUtc.ToString("u", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"* Baseline configuration: **{comparison.BaselineConfiguration}**");
        sb.AppendLine($"* Optimized configuration: **{comparison.OptimizedConfiguration}**");
        sb.AppendLine($"* Verdict: **{comparison.Verdict}**");
        sb.AppendLine();

        sb.AppendLine("## Result");
        sb.AppendLine();
        sb.AppendLine($"**{comparison.Headline}**");
        sb.AppendLine();
        sb.AppendLine(comparison.Summary);
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(context))
        {
            sb.AppendLine("## Test setup");
            sb.AppendLine();
            sb.AppendLine(context);
            sb.AppendLine();
        }

        sb.AppendLine("## Comparison");
        sb.AppendLine();
        sb.AppendLine("| Metric | " + comparison.BaselineConfiguration + " (median) | " + comparison.OptimizedConfiguration + " (median) | Change |");
        sb.AppendLine("| --- | ---: | ---: | ---: |");
        sb.AppendLine($"| Average FPS | {comparison.BaselineMedianAverageFps:0.0} | {comparison.OptimizedMedianAverageFps:0.0} | {comparison.AverageFpsDeltaPercent:+0.0;-0.0;0.0} % |");
        sb.AppendLine($"| 1 % low FPS | {comparison.BaselineMedianOnePercentLowFps:0.0} | {comparison.OptimizedMedianOnePercentLowFps:0.0} | {comparison.OnePercentLowDeltaPercent:+0.0;-0.0;0.0} % |");
        sb.AppendLine($"| Median frame time | {comparison.BaselineMedianFrameTimeMs:0.00} ms | {comparison.OptimizedMedianFrameTimeMs:0.00} ms | - |");
        sb.AppendLine($"| Runs | {comparison.BaselineRuns.Count} | {comparison.OptimizedRuns.Count} | - |");
        sb.AppendLine($"| Run to run spread (IQR/median) | {comparison.BaselineVariationPercent:0.0} % | {comparison.OptimizedVariationPercent:0.0} % | - |");
        sb.AppendLine();
        sb.AppendLine($"Statistical test: two sided Mann-Whitney U test on the per run average frame rates, p = {(comparison.PValue is { } p ? p.ToString("0.000", CultureInfo.InvariantCulture) : "not computed")}.");
        sb.AppendLine();

        sb.AppendLine("## Individual runs");
        sb.AppendLine();
        sb.AppendLine("| # | Configuration | File | Frames | Avg FPS | Median frame | p95 frame | p99 frame | 1 % low | Warm-up |");
        sb.AppendLine("| ---: | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | :---: |");

        var order = comparison.BaselineRuns.Concat(comparison.OptimizedRuns)
            .OrderBy(r => r.RunOrder)
            .ToList();

        var index = 1;
        foreach (var run in order)
        {
            var stats = run.Statistics;
            sb.AppendLine(
                $"| {index++} | {run.Configuration} | {Path.GetFileName(run.SourcePath)} | {stats.PresentedFrameCount} | " +
                $"{stats.AverageFps:0.0} | {stats.MedianFrameTimeMs:0.00} ms | {stats.P95FrameTimeMs:0.00} ms | " +
                $"{stats.P99FrameTimeMs:0.00} ms | {stats.OnePercentLowFps:0.0} | {(run.IsWarmUp ? "yes" : "no")} |");
        }

        sb.AppendLine();

        if (comparison.Notes.Count > 0)
        {
            sb.AppendLine("## Notes");
            sb.AppendLine();
            foreach (var note in comparison.Notes)
            {
                sb.AppendLine($"* {note}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("## Method");
        sb.AppendLine();
        sb.AppendLine("* Frame times come from PresentMon CSV recordings; FramePilot does not estimate frame rates from CPU or memory usage.");
        sb.AppendLine("* Average FPS = presented frames / duration of the recording.");
        sb.AppendLine("* 1 % low = mean of 1000 / frame time over the frames whose frame time is at or above the 99th percentile of the recording.");
        sb.AppendLine("* Percentiles use linear interpolation between the two closest order statistics.");
        sb.AppendLine("* A measured difference is an observation of these runs. It does not prove that the change caused the difference.");
        sb.AppendLine();

        return sb.ToString();
    }
}
