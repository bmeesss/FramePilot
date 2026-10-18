namespace FramePilot.Core.Abstractions;

/// <summary>
/// Lightweight logging abstraction. FramePilot deliberately avoids a logging
/// framework dependency; this is enough for the diagnostics panel and for
/// tests to assert on behaviour.
/// </summary>
public interface IAppLog
{
    void Write(LogLevel level, string message);

    void Information(string message) => Write(LogLevel.Information, message);

    void Warning(string message) => Write(LogLevel.Warning, message);

    void Error(string message) => Write(LogLevel.Error, message);
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
}

/// <summary>Default implementation that keeps the last messages in memory.</summary>
public sealed class InMemoryAppLog : IAppLog
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();
    private readonly object _gate = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get
        {
            lock (_gate)
            {
                return _entries.ToArray();
            }
        }
    }

    public void Write(LogLevel level, string message)
    {
        lock (_gate)
        {
            _entries.Add((level, message));
        }
    }
}
