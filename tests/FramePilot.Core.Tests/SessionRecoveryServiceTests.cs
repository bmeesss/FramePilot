using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;
using FramePilot.Core.Sessions;
using FramePilot.Core.Tests.Fakes;

namespace FramePilot.Core.Tests;

public class SessionRecoveryServiceTests
{
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid HighPerformance = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid PowerSaver = Guid.Parse("a1841308-3541-4fab-bc81-f71556f20b4a");

    [Fact]
    public async Task Recover_WithoutJournal_DoesNothing()
    {
        var power = CreatePowerService();
        var service = new SessionRecoveryService(power, new FakeSessionJournalStore(), new InMemoryAppLog());

        var outcome = await service.RecoverAsync();

        Assert.False(outcome.Restored);
        Assert.Equal(0, power.SetCallCount);
    }

    [Fact]
    public async Task Recover_WithTemporaryPlanStillActive_RestoresTheOriginalPlan()
    {
        var power = CreatePowerService();
        power.ActivePlan = new PowerPlanInfo(HighPerformance, "High performance", true);

        var journal = new FakeSessionJournalStore();
        await journal.SaveAsync(CreateJournal());

        var service = new SessionRecoveryService(power, journal, new InMemoryAppLog());

        var outcome = await service.RecoverAsync();

        Assert.True(outcome.Restored);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Recover_WithUserChangedPlan_KeepsTheUserChoice()
    {
        var power = CreatePowerService();
        power.ActivePlan = new PowerPlanInfo(PowerSaver, "Power saver", true);

        var journal = new FakeSessionJournalStore();
        await journal.SaveAsync(CreateJournal());

        var service = new SessionRecoveryService(power, journal, new InMemoryAppLog());

        var outcome = await service.RecoverAsync();

        Assert.False(outcome.Restored);
        Assert.Equal(PowerSaver, power.ActivePlan?.Guid);
        Assert.Equal(0, power.SetCallCount);
        Assert.Null(journal.Entry);
        Assert.Contains("left untouched", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recover_WithMissingOriginalPlan_KeepsTheCurrentPlan()
    {
        var power = CreatePowerService();
        power.Plans.RemoveAll(p => p.Guid == Balanced);
        power.ActivePlan = new PowerPlanInfo(HighPerformance, "High performance", true);

        var journal = new FakeSessionJournalStore();
        await journal.SaveAsync(CreateJournal());

        var service = new SessionRecoveryService(power, journal, new InMemoryAppLog());

        var outcome = await service.RecoverAsync();

        Assert.False(outcome.Restored);
        Assert.Equal(HighPerformance, power.ActivePlan?.Guid);
        Assert.Contains("no longer exists", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recover_WithPreparedJournal_JustClearsIt()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        await journal.SaveAsync(CreateJournal() with { State = SessionJournalState.Prepared });

        var service = new SessionRecoveryService(power, journal, new InMemoryAppLog());

        var outcome = await service.RecoverAsync();

        Assert.False(outcome.Restored);
        Assert.Equal(0, power.SetCallCount);
        Assert.Null(journal.Entry);
    }

    private static SessionJournalEntry CreateJournal() => new()
    {
        OriginalPowerPlanGuid = Balanced,
        OriginalPowerPlanName = "Balanced",
        AppliedPowerPlanGuid = HighPerformance,
        AppliedPowerPlanName = "High performance",
        State = SessionJournalState.Applied,
        StartedAtUtc = DateTimeOffset.UtcNow,
    };

    private static FakePowerPlanService CreatePowerService() => new(
        new PowerPlanInfo(Balanced, "Balanced", true),
        new PowerPlanInfo(HighPerformance, "High performance", false),
        new PowerPlanInfo(PowerSaver, "Power saver", false));
}
