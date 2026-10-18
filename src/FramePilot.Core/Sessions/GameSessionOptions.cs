namespace FramePilot.Core.Sessions;

/// <summary>Everything the user configured for a game session.</summary>
public sealed record GameSessionOptions
{
    /// <summary>The power plan that should be active during the session.</summary>
    public Guid TargetPowerPlanGuid { get; init; }

    /// <summary>Display name of the target power plan.</summary>
    public string TargetPowerPlanName { get; init; } = string.Empty;

    /// <summary>Full path of the game executable.</summary>
    public string? GameExecutablePath { get; init; }

    /// <summary>Optional command line arguments for the game.</summary>
    public string? GameArguments { get; init; }

    /// <summary>Optional working directory for the game.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>True when FramePilot should start the game itself.</summary>
    public bool LaunchGame { get; init; }
}

/// <summary>Lifecycle state of the game session manager.</summary>
public enum GameSessionState
{
    /// <summary>No session is running.</summary>
    Idle = 0,

    /// <summary>The session is being prepared (journal written, plan applied).</summary>
    Preparing = 1,

    /// <summary>The session is running and the temporary plan is active.</summary>
    Running = 2,

    /// <summary>The original plan is being restored.</summary>
    Restoring = 3,

    /// <summary>The last operation failed.</summary>
    Failed = 4,
}

/// <summary>Result of a game session operation.</summary>
public sealed record SessionOutcome(bool Success, string Message, SessionJournalEntry? Journal = null)
{
    public static SessionOutcome Fail(string message) => new(false, message);
}

/// <summary>Raised when the state of the game session changes.</summary>
public sealed class GameSessionStateChangedEventArgs : EventArgs
{
    public GameSessionStateChangedEventArgs(GameSessionState previous, GameSessionState current, string message)
    {
        Previous = previous;
        Current = current;
        Message = message;
    }

    public GameSessionState Previous { get; }

    public GameSessionState Current { get; }

    public string Message { get; }
}
