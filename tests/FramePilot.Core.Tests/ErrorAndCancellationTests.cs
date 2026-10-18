using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;
using FramePilot.Core.PresentMon;
using FramePilot.Core.Services;
using FramePilot.Core.Sessions;
using FramePilot.Core.Tests.Fakes;

namespace FramePilot.Core.Tests;

public class ErrorAndCancellationTests
{
    [Fact]
    public async Task StartSession_WithCancelledToken_DoesNotChangeAnything()
    {
        var power = new FakePowerPlanService(
            new PowerPlanInfo(Guid.NewGuid(), "Balanced", true),
            new PowerPlanInfo(Guid.NewGuid(), "High performance", false));

        var controller = new GameSessionController(power, new FakeSessionJournalStore(), new FakeGameProcessLauncher(), new InMemoryAppLog());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await controller.StartAsync(
            new GameSessionOptions { TargetPowerPlanGuid = Guid.NewGuid() },
            cts.Token);

        Assert.False(outcome.Success);
        Assert.Equal(0, power.SetCallCount);
    }

    [Fact]
    public async Task EndSession_WithCancelledToken_ReportsTheCancellation()
    {
        var controller = new GameSessionController(
            new FakePowerPlanService(new PowerPlanInfo(Guid.NewGuid(), "Balanced", true)),
            new FakeSessionJournalStore(),
            new FakeGameProcessLauncher(),
            new InMemoryAppLog());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var outcome = await controller.EndAsync(cts.Token);

        Assert.False(outcome.Success);
        Assert.Contains("cancelled", outcome.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_OfGarbageContent_ThrowsWithAnExplanation()
    {
        var exception = Assert.Throws<PresentMonParseException>(
            () => new PresentMonCsvParser().Parse("this is not a csv file at all"));

        Assert.Equal(PresentMonParseError.UnsupportedSchema, exception.Error);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public async Task Settings_WithCorruptedFile_FallBackToTheDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "framepilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "settings.json");
            await File.WriteAllTextAsync(path, "{ not json");

            var service = new SettingsService(path);
            var settings = await service.LoadAsync();

            Assert.Equal(1.0, settings.SampleIntervalSeconds, 3);
            Assert.Equal(5, settings.MinimumRunsPerConfiguration);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public async Task Settings_RoundTrip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "framepilot-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(path);

            await service.SaveAsync(new AppSettings
            {
                PresentMonPath = @"C:\tools\PresentMon.exe",
                MinimumRunsPerConfiguration = 7,
                SampleIntervalSeconds = 2.5,
            });

            var settings = await service.LoadAsync();

            Assert.Equal(@"C:\tools\PresentMon.exe", settings.PresentMonPath);
            Assert.Equal(7, settings.MinimumRunsPerConfiguration);
            Assert.Equal(2.5, settings.SampleIntervalSeconds, 3);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
