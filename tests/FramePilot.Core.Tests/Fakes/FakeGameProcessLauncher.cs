using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Tests.Fakes;

/// <summary>A launcher that records the request and returns a fake process.</summary>
public sealed class FakeGameProcessLauncher : IGameProcessLauncher
{
    public GameLaunchRequest? LastRequest { get; private set; }

    public bool ShouldThrow { get; set; }

    public FakeGameProcessHandle Handle { get; } = new();

    public Task<IGameProcessHandle> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("The game could not be started.");
        }

        LastRequest = request;
        return Task.FromResult<IGameProcessHandle>(Handle);
    }
}

/// <summary>A fake game process.</summary>
public sealed class FakeGameProcessHandle : IGameProcessHandle
{
    public int ProcessId => 4242;

    public string ExecutablePath => "C:\\Games\\game.exe";

    public bool HasExited { get; private set; }

    public void Exit() => HasExited = true;

    public Task<bool> WaitForExitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    public void Dispose()
    {
    }
}
