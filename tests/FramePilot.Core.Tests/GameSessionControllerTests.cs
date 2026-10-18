using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;
using FramePilot.Core.Sessions;
using FramePilot.Core.Tests.Fakes;

namespace FramePilot.Core.Tests;

public class GameSessionControllerTests
{
    private static readonly Guid Balanced = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid HighPerformance = Guid.Parse("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    [Fact]
    public async Task Start_AppliesThePlanAndWritesTheJournal()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        var outcome = await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            TargetPowerPlanName = "High performance",
        });

        Assert.True(outcome.Success);
        Assert.Equal(GameSessionState.Running, controller.State);
        Assert.Equal(HighPerformance, power.ActivePlan?.Guid);

        Assert.NotNull(journal.Entry);
        Assert.Equal(SessionJournalState.Applied, journal.Entry!.State);
        Assert.Equal(Balanced, journal.Entry.OriginalPowerPlanGuid);
        Assert.Equal(HighPerformance, journal.Entry.AppliedPowerPlanGuid);
    }

    [Fact]
    public async Task Start_WithAlreadyActivePlan_DoesNotChangeAnything()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        var outcome = await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = Balanced,
            TargetPowerPlanName = "Balanced",
        });

        Assert.True(outcome.Success);
        Assert.Equal(0, power.SetCallCount);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
    }

    [Fact]
    public async Task Start_WhenTheChangeIsRejected_RestoresTheOriginalPlan()
    {
        var power = CreatePowerService();
        power.FailingPlans.Add(HighPerformance);

        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        var outcome = await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            TargetPowerPlanName = "High performance",
        });

        Assert.False(outcome.Success);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
        Assert.Equal(GameSessionState.Failed, controller.State);
    }

    [Fact]
    public async Task Start_WhenTheVerificationFails_RestoresTheOriginalPlan()
    {
        var power = CreatePowerService();
        power.ForcedActivePlan = Balanced;

        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        var outcome = await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            TargetPowerPlanName = "High performance",
        });

        Assert.False(outcome.Success);
        Assert.Contains("could not be activated", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
    }

    [Fact]
    public async Task End_RestoresTheOriginalPlanAndClearsTheJournal()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            TargetPowerPlanName = "High performance",
        });

        var outcome = await controller.EndAsync();

        Assert.True(outcome.Success);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
        Assert.Null(journal.Entry);
        Assert.Equal(GameSessionState.Idle, controller.State);
    }

    [Fact]
    public async Task Cancel_RestoresTheOriginalPlan()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        var controller = CreateController(power, journal);

        await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            TargetPowerPlanName = "High performance",
        });

        var outcome = await controller.CancelAsync();

        Assert.True(outcome.Success);
        Assert.Equal(Balanced, power.ActivePlan?.Guid);
        Assert.Null(journal.Entry);
    }

    [Fact]
    public async Task Start_WithMissingExecutable_Fails()
    {
        var power = CreatePowerService();
        var controller = CreateController(power, new FakeSessionJournalStore());

        var outcome = await controller.StartAsync(new GameSessionOptions
        {
            TargetPowerPlanGuid = HighPerformance,
            LaunchGame = true,
            GameExecutablePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe"),
        });

        Assert.False(outcome.Success);
        Assert.Contains("does not exist", outcome.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, power.SetCallCount);
    }

    [Fact]
    public async Task Start_WithoutPowerPlan_Fails()
    {
        var controller = CreateController(CreatePowerService(), new FakeSessionJournalStore());

        var outcome = await controller.StartAsync(new GameSessionOptions());

        Assert.False(outcome.Success);
    }

    [Fact]
    public async Task Start_LaunchesTheGameAndStoresTheProcessId()
    {
        var power = CreatePowerService();
        var journal = new FakeSessionJournalStore();
        var launcher = new FakeGameProcessLauncher();
        var controller = new GameSessionController(power, journal, launcher, new InMemoryAppLog());

        var executable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
        await File.WriteAllTextAsync(executable, "stub");

        try
        {
            var outcome = await controller.StartAsync(new GameSessionOptions
            {
                TargetPowerPlanGuid = HighPerformance,
                TargetPowerPlanName = "High performance",
                LaunchGame = true,
                GameExecutablePath = executable,
                GameArguments = "--windowed",
            });

            Assert.True(outcome.Success);
            Assert.NotNull(launcher.LastRequest);
            Assert.Equal(executable, launcher.LastRequest!.ExecutablePath);
            Assert.Equal("--windowed", launcher.LastRequest.Arguments);
            Assert.Equal(4242, journal.Entry?.GameProcessId);
        }
        finally
        {
            File.Delete(executable);
        }
    }

    private static FakePowerPlanService CreatePowerService() => new(
        new PowerPlanInfo(Balanced, "Balanced", true),
        new PowerPlanInfo(HighPerformance, "High performance", false));

    private static GameSessionController CreateController(
        FakePowerPlanService power,
        FakeSessionJournalStore journal) =>
        new(power, journal, new FakeGameProcessLauncher(), new InMemoryAppLog());
}
