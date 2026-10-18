using FramePilot.Core.Sessions;
using FramePilot.Core.Tests.Fakes;

namespace FramePilot.Core.Tests;

public class SessionJournalTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsTheEntry()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new FileSessionJournalStore(Path.Combine(directory, "journal.json"));
            var entry = new SessionJournalEntry
            {
                OriginalPowerPlanGuid = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"),
                OriginalPowerPlanName = "Balanced",
                AppliedPowerPlanGuid = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
                AppliedPowerPlanName = "High performance",
                GameExecutablePath = @"C:\Games\game.exe",
                GameProcessId = 4242,
                StartedAtUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                State = SessionJournalState.Applied,
            };

            await store.SaveAsync(entry);

            var loaded = await store.LoadAsync();

            Assert.NotNull(loaded);
            Assert.Equal(entry.OriginalPowerPlanGuid, loaded!.OriginalPowerPlanGuid);
            Assert.Equal(entry.AppliedPowerPlanGuid, loaded.AppliedPowerPlanGuid);
            Assert.Equal(entry.AppliedPowerPlanName, loaded.AppliedPowerPlanName);
            Assert.Equal(entry.GameProcessId, loaded.GameProcessId);
            Assert.Equal(SessionJournalState.Applied, loaded.State);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Load_WithoutFile_ReturnsNull()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var store = new FileSessionJournalStore(Path.Combine(directory, "journal.json"));

            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Load_WithCorruptedFile_ReturnsNull()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "journal.json");
            await File.WriteAllTextAsync(path, "{ this is not json ");

            var store = new FileSessionJournalStore(path);

            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Clear_RemovesTheFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "journal.json");
            var store = new FileSessionJournalStore(path);

            await store.SaveAsync(new SessionJournalEntry());
            Assert.True(File.Exists(path));

            await store.ClearAsync();

            Assert.False(File.Exists(path));
            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Save_DoesNotLeaveTemporaryFilesBehind()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "journal.json");
            var store = new FileSessionJournalStore(path);

            await store.SaveAsync(new SessionJournalEntry());

            Assert.False(File.Exists(path + ".tmp"));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "framepilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // The temporary directory is cleaned up by the operating system.
        }
    }
}
