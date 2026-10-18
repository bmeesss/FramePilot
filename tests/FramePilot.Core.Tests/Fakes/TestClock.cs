using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Tests.Fakes;

/// <summary>A clock that can be advanced by the test.</summary>
public sealed class TestClock : IClock
{
    private DateTimeOffset _now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => _now;

    public TimeSpan Elapsed { get; private set; }

    public void Advance(TimeSpan amount)
    {
        _now = _now.Add(amount);
        Elapsed += amount;
    }
}
