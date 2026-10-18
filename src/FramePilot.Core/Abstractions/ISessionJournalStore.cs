namespace FramePilot.Core.Abstractions;

/// <summary>
/// Persistent journal of the last game session. The journal makes it possible
/// to restore the original power plan after an unclean shutdown.
/// </summary>
public interface ISessionJournalStore
{
    /// <summary>Path of the journal file.</summary>
    string FilePath { get; }

    /// <summary>Reads the journal, or returns <c>null</c> when no session was recorded.</summary>
    Task<SessionJournalEntry?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes the journal atomically (temp file + replace).</summary>
    Task SaveAsync(SessionJournalEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Removes the journal file if it exists.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
