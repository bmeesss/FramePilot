namespace FramePilot.Core.Sessions;

/// <summary>State of a recorded game session.</summary>
public enum SessionJournalState
{
    /// <summary>The journal was written, the power plan has not been changed yet.</summary>
    Prepared = 0,

    /// <summary>The temporary power plan is active and has to be restored.</summary>
    Applied = 1,

    /// <summary>The original power plan was restored.</summary>
    Restored = 2,
}

/// <summary>
/// The recovery journal of a game session. It is written before the power
/// plan is changed so that an interrupted FramePilot can restore the original
/// plan on the next start.
/// </summary>
public sealed record SessionJournalEntry
{
    /// <summary>Format version of the journal.</summary>
    public int Version { get; init; } = 1;

    /// <summary>Identifier of the session.</summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>The power plan that was active before the session.</summary>
    public Guid OriginalPowerPlanGuid { get; init; }

    /// <summary>Name of the original power plan.</summary>
    public string OriginalPowerPlanName { get; init; } = string.Empty;

    /// <summary>The temporary power plan that FramePilot activated.</summary>
    public Guid AppliedPowerPlanGuid { get; init; }

    /// <summary>Name of the temporary power plan.</summary>
    public string AppliedPowerPlanName { get; init; } = string.Empty;

    /// <summary>Game executable that was started (optional).</summary>
    public string? GameExecutablePath { get; init; }

    /// <summary>Process id of the game (optional).</summary>
    public int? GameProcessId { get; init; }

    /// <summary>When the session was started.</summary>
    public DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>State of the session.</summary>
    public SessionJournalState State { get; init; } = SessionJournalState.Prepared;
}
