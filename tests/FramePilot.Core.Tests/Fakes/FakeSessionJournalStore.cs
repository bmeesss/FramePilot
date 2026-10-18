using FramePilot.Core.Abstractions;
using FramePilot.Core.Sessions;

namespace FramePilot.Core.Tests.Fakes;

/// <summary>An in memory journal store.</summary>
public sealed class FakeSessionJournalStore : ISessionJournalStore
{
    public string FilePath => "memory://session-journal.json";

    public SessionJournalEntry? Entry { get; private set; }

    public int SaveCount { get; private set; }

    public int ClearCount { get; private set; }

    public Task<SessionJournalEntry?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Entry);

    public Task SaveAsync(SessionJournalEntry entry, CancellationToken cancellationToken = default)
    {
        Entry = entry;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Entry = null;
        ClearCount++;
        return Task.CompletedTask;
    }
}
