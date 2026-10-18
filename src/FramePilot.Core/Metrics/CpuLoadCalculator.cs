using FramePilot.Core.Models;

namespace FramePilot.Core.Metrics;

/// <summary>
/// Computes overall CPU utilisation from two consecutive samples of the
/// cumulative processor time counters.
/// </summary>
/// <remarks>
/// <para>
/// The Windows <c>GetSystemTimes</c> API returns idle, kernel and user time
/// where <em>kernel time includes idle time</em>. Utilisation is therefore
/// <c>(deltaTotal - deltaIdle) / deltaTotal</c> over the sampling window,
/// which is exactly the average across all logical processors.
/// </para>
/// <para>
/// The first sample of a session has no predecessor and therefore cannot
/// produce a utilisation value; <c>null</c> is returned in that case instead of
/// a made up number.
/// </para>
/// </remarks>
public static class CpuLoadCalculator
{
    /// <summary>
    /// Returns the CPU utilisation percentage between two samples, or
    /// <c>null</c> when it cannot be computed.
    /// </summary>
    public static double? ComputeUtilization(ProcessorTimes? previous, ProcessorTimes current)
    {
        if (previous is null)
        {
            return null;
        }

        var totalDelta = (current.Total - previous.Value.Total).TotalMilliseconds;
        var idleDelta = (current.Idle - previous.Value.Idle).TotalMilliseconds;

        if (totalDelta <= 0)
        {
            // Counter reset, or the sample was taken twice within the same tick.
            return null;
        }

        if (idleDelta < 0)
        {
            return null;
        }

        var busyDelta = totalDelta - idleDelta;
        var utilization = 100.0 * busyDelta / totalDelta;

        if (double.IsNaN(utilization) || double.IsInfinity(utilization))
        {
            return null;
        }

        return Math.Clamp(utilization, 0.0, 100.0);
    }

    /// <summary>
    /// Computes the per logical processor utilisation, i.e. the share of one
    /// core that was busy. The Windows API used by FramePilot only exposes
    /// aggregated counters, so the value is derived by scaling the aggregated
    /// utilisation with the number of logical processors.
    /// </summary>
    public static double? ComputePerCoreUtilization(double? aggregateUtilizationPercent, int logicalProcessorCount)
    {
        if (aggregateUtilizationPercent is null || logicalProcessorCount <= 0)
        {
            return null;
        }

        var value = aggregateUtilizationPercent.Value * logicalProcessorCount;
        return Math.Clamp(value, 0.0, 100.0);
    }
}
