using FramePilot.Core.PresentMon;

namespace FramePilot.Core.Tests;

public class PresentMonCommandBuilderTests
{
    [Fact]
    public void BuildCaptureArguments_UsesDocumentedSwitches()
    {
        var arguments = PresentMonCommandBuilder.BuildCaptureArguments(new PresentMonCaptureOptions
        {
            OutputFilePath = @"C:\captures\run.csv",
            ProcessName = "game.exe",
            Duration = TimeSpan.FromSeconds(30),
            SessionName = "FramePilot-test",
        });

        Assert.Contains("--output_file", arguments);
        Assert.Contains(@"C:\captures\run.csv", arguments);
        Assert.Contains("--process_name", arguments);
        Assert.Contains("game.exe", arguments);
        Assert.Contains("--timed", arguments);
        Assert.Contains("30", arguments);
        Assert.Contains("--terminate_after_timed", arguments);
        Assert.Contains("--session_name", arguments);
        Assert.Contains("--no_console_stats", arguments);
    }

    [Fact]
    public void BuildCaptureArguments_KeepsTheProcessNameAsItsOwnArgument()
    {
        var arguments = PresentMonCommandBuilder.BuildCaptureArguments(new PresentMonCaptureOptions
        {
            OutputFilePath = @"C:\captures\run.csv",
            ProcessName = "game.exe",
        });

        var index = arguments.ToList().IndexOf("--process_name");

        Assert.True(index >= 0);
        Assert.Equal("game.exe", arguments[index + 1]);
    }

    [Fact]
    public void BuildCaptureArguments_WithoutDuration_AddsNoTimer()
    {
        var arguments = PresentMonCommandBuilder.BuildCaptureArguments(new PresentMonCaptureOptions
        {
            OutputFilePath = @"C:\captures\run.csv",
        });

        Assert.DoesNotContain("--timed", arguments);
        Assert.DoesNotContain("--terminate_after_timed", arguments);
    }

    [Fact]
    public void BuildCaptureArguments_WithoutOutputFile_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PresentMonCommandBuilder.BuildCaptureArguments(new PresentMonCaptureOptions { OutputFilePath = "  " }));
    }

    [Fact]
    public void BuildCaptureArguments_WithTrackingOptions_AddsTheSwitches()
    {
        var arguments = PresentMonCommandBuilder.BuildCaptureArguments(new PresentMonCaptureOptions
        {
            OutputFilePath = @"C:\captures\run.csv",
            ExcludeDropped = true,
            NoTrackGpu = true,
            NoTrackInput = true,
            NoTrackDisplay = true,
            TerminateOnProcessExit = true,
        });

        Assert.Contains("--exclude_dropped", arguments);
        Assert.Contains("--no_track_gpu", arguments);
        Assert.Contains("--no_track_input", arguments);
        Assert.Contains("--no_track_display", arguments);
        Assert.Contains("--terminate_on_proc_exit", arguments);
    }

    [Fact]
    public void Locate_WithConfiguredPath_UsesThatPath()
    {
        var locator = new PresentMonLocator(_ => true);

        var path = locator.Locate(@"C:\tools\PresentMon.exe", @"C:\app");

        Assert.Equal(@"C:\tools\PresentMon.exe", path);
    }

    [Fact]
    public void Locate_WithMissingConfiguredPath_ReturnsNull()
    {
        var locator = new PresentMonLocator(_ => false);

        Assert.Null(locator.Locate(@"C:\tools\PresentMon.exe", @"C:\app"));
    }

    [Fact]
    public void Locate_WithFileNextToTheApplication_FindsIt()
    {
        var locator = new PresentMonLocator(path => path.EndsWith("PresentMon.exe", StringComparison.OrdinalIgnoreCase));

        var path = locator.Locate(null, @"C:\app");

        Assert.NotNull(path);
        Assert.Contains("PresentMon.exe", path!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Locate_WithConfiguredPathThatIsNotPresentMon_ReturnsNull()
    {
        var locator = new PresentMonLocator(_ => true);

        Assert.Null(locator.Locate(@"C:\tools\NotPresentMon.exe", @"C:\app"));
    }
}
