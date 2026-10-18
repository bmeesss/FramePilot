namespace FramePilot.Core.Metrics;

/// <summary>
/// Computes the CPU utilisation of a single process from two consecutive
/// samples of its processor time.
/// </summary>
/// <remarks>
/// The processor time of a process is the sum of its user and kernel time and
/// is measured in CPU time, i.e. it can exceed the wall clock time when the
/// process uses several cores. The percentage is therefore normalised by the
/// number of logical processors, which makes 100 % mean "all cores saturated".
/// </remarks>
public static class ProcessCpuCalculator
{
    /// <summary>
    /// Returns the CPU usage of a process in percent, or <c>null</c> when the
    /// value cannot be computed (first sample, process restarted, no elapsed
    /// time).
    /// </summary>
    public static double? ComputePercent(
        TimeSpan previousProcessorTime,
        TimeSpan currentProcessorTime,
        TimeSpan elapsed,
        int logicalProcessorCount)
    {
        if (logicalProcessorCount <= 0)
        {
            return null;
        }

        if (elapsed <= TimeSpan.Zero)
        {
            return null;
        }

        var delta = currentProcessorTime - previousProcessorTime;

        if (delta < TimeSpan.Zero)
        {
            // The process was restarted (or the counter was reset).
            return null;
        }

        var elapsedMs = elapsed.TotalMilliseconds;
        if (elapsedMs <= 0)
        {
            return null;
        }

        var percent = 100.0 * delta.TotalMilliseconds / elapsedMs / logicalProcessorCount;

        if (double.IsNaN(percent) || double.IsInfinity(percent))
        {
            return null;
        }

        return Math.Clamp(percent, 0.0, 100.0);
    }

    /// <summary>
    /// Returns the CPU usage of a process, or <c>null</c> when the process was
    /// not seen during the previous sample.
    /// </summary>
    public static double? ComputePercent(
        TimeSpan? previousProcessorTime,
        TimeSpan currentProcessorTime,
        TimeSpan elapsed,
        int logicalProcessorCount)
    {
        if (previousProcessorTime is null)
        {
            return null;
        }

        return ComputePercent(previousProcessorTime.Value, currentProcessorTime, elapsed, logicalProcessorCount);
    }
}
