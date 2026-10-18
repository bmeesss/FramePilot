using System.Diagnostics;
using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Infrastructure;

/// <summary>The real system clock.</summary>
public sealed class SystemClock : IClock
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeSpan Elapsed => _stopwatch.Elapsed;
}
