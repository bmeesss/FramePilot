namespace FramePilot.Core.Abstractions;

/// <summary>
/// Launches the game executable for a game session. The implementation must
/// use <see cref="System.Diagnostics.ProcessStartInfo"/> with an argument list
/// (never a shell string).
/// </summary>
public interface IGameProcessLauncher
{
    Task<IGameProcessHandle> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Everything needed to start the game.</summary>
public sealed record GameLaunchRequest(
    string ExecutablePath,
    string? Arguments,
    string? WorkingDirectory);

/// <summary>Handle to a launched game process.</summary>
public interface IGameProcessHandle : IDisposable
{
    int ProcessId { get; }

    string ExecutablePath { get; }

    bool HasExited { get; }

    /// <summary>Completes when the game exits. Returns false when the wait was cancelled.</summary>
    Task<bool> WaitForExitAsync(CancellationToken cancellationToken);
}
