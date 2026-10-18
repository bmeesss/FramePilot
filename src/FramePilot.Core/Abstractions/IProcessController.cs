using FramePilot.Core.Models;

namespace FramePilot.Core.Abstractions;

/// <summary>
/// Process enumeration and *graceful* close requests. FramePilot never
/// force-terminates a process; implementations must only use documented
/// window close messages.
/// </summary>
public interface IProcessController
{
    /// <summary>All processes that can be inspected by the current user.</summary>
    Task<IReadOnlyList<ProcessSnapshot>> GetProcessesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the process to close by posting a close message to its windows.
    /// The process is free to refuse; the result reports what happened.
    /// </summary>
    Task<ProcessCloseResult> RequestGracefulCloseAsync(int processId, CancellationToken cancellationToken = default);
}

/// <summary>Result of a graceful close request.</summary>
public sealed record ProcessCloseResult(bool Accepted, bool Exited, string Message)
{
    public static ProcessCloseResult Refused(string message) => new(false, false, message);
}
