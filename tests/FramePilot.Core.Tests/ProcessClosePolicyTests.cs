using FramePilot.Core.Models;
using FramePilot.Core.Processes;

namespace FramePilot.Core.Tests;

public class ProcessClosePolicyTests
{
    private const string WindowsDirectory = @"C:\Windows";

    [Fact]
    public void Evaluate_ForNormalApplication_RequiresConfirmation()
    {
        var process = CreateProcess("Steam.exe", @"C:\Program Files (x86)\Steam\steam.exe");

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        Assert.Equal(ProcessCloseVerdict.RequiresConfirmation, decision.Verdict);
        Assert.False(decision.IsBlocked);
        Assert.Contains("asked to close", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only improves the frame rate", decision.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("csrss.exe")]
    [InlineData("wininit.exe")]
    [InlineData("winlogon.exe")]
    [InlineData("services.exe")]
    [InlineData("lsass.exe")]
    [InlineData("svchost.exe")]
    [InlineData("dwm.exe")]
    [InlineData("MsMpEng.exe")]
    [InlineData("TrustedInstaller.exe")]
    public void Evaluate_ForSystemProcess_IsBlocked(string name)
    {
        var process = CreateProcess(name, @"C:\Windows\System32\" + name);

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        Assert.True(decision.IsBlocked);
    }

    [Fact]
    public void Evaluate_ForProcessFromWindowsDirectory_IsBlocked()
    {
        var process = CreateProcess("weirdservice.exe", @"C:\Windows\System32\weirdservice.exe");

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        Assert.True(decision.IsBlocked);
        Assert.Contains("Windows directory", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_WithoutWindow_IsBlocked()
    {
        var process = CreateProcess("background.exe", @"C:\Tools\background.exe", hasWindow: false);

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        Assert.True(decision.IsBlocked);
        Assert.Contains("force", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ForExplorerInWindowsDirectory_IsBlocked()
    {
        var process = CreateProcess("explorer.exe", @"C:\Windows\explorer.exe");

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        // explorer.exe lives in the Windows directory and is therefore blocked.
        Assert.True(decision.IsBlocked);
    }

    [Fact]
    public void Evaluate_ForExplorerOutsideWindowsDirectory_WarnsAboutTheDesktopSession()
    {
        var process = CreateProcess("explorer.exe", @"C:\Custom\explorer.exe");

        var decision = ProcessClosePolicy.Evaluate(process, WindowsDirectory);

        Assert.Equal(ProcessCloseVerdict.RequiresConfirmation, decision.Verdict);
        Assert.Contains("desktop session", decision.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetBaseName_RemovesTheExtension()
    {
        Assert.Equal("game", ProcessClosePolicy.GetBaseName("game.exe"));
        Assert.Equal("game", ProcessClosePolicy.GetBaseName("GAME.EXE"));
        Assert.Equal("game", ProcessClosePolicy.GetBaseName("game"));
        Assert.Equal(string.Empty, ProcessClosePolicy.GetBaseName(null));
    }

    private static ProcessSnapshot CreateProcess(string name, string path, bool hasWindow = true) =>
        new(1234, name, path, CpuPercent: 5.0, 1024, 2048, hasWindow, IsSystemProcess: false);
}
