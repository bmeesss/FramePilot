using FramePilot.Core.Models;

namespace FramePilot.Core.Metrics;

/// <summary>
/// Converts raw memory counters into the values shown on the dashboard.
/// </summary>
public static class MemoryCalculator
{
    /// <summary>
    /// Builds a memory snapshot from the total and available physical memory.
    /// Returns <see cref="MemorySnapshot.Unavailable"/> when the total is not
    /// known, so the UI can display "Unavailable" instead of a wrong number.
    /// </summary>
    public static MemorySnapshot Compute(long totalBytes, long availableBytes)
    {
        if (totalBytes <= 0)
        {
            return MemorySnapshot.Unavailable;
        }

        if (availableBytes < 0)
        {
            availableBytes = 0;
        }

        if (availableBytes > totalBytes)
        {
            availableBytes = totalBytes;
        }

        var used = totalBytes - availableBytes;
        var usedPercent = 100.0 * used / totalBytes;

        return new MemorySnapshot(totalBytes, availableBytes, used, usedPercent);
    }
}
