namespace FramePilot.Core.Abstractions;

/// <summary>
/// Abstraction over the system clock so that time dependent logic (sampling
/// intervals, session journals, capture durations) can be unit tested.
/// </summary>
public interface IClock
{
    /// <summary>Current wall clock time in UTC.</summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>Monotonic elapsed time since the clock was created.</summary>
    TimeSpan Elapsed { get; }
}
