using System.Diagnostics;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;

namespace FramePilot.Windows;

/// <summary>
/// Starts the game executable of a session.
/// </summary>
/// <remarks>
/// The executable is started without a shell and the arguments are passed
/// through <see cref="ProcessStartInfo.ArgumentList"/>, so nothing in the
/// argument string can be interpreted as a command.
/// </remarks>
public sealed class WindowsGameProcessLauncher : IGameProcessLauncher
{
    public Task<IGameProcessHandle> LaunchAsync(GameLaunchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ExecutablePath))
        {
            throw new ArgumentException("No executable was given.", nameof(request));
        }

        if (!File.Exists(request.ExecutablePath))
        {
            throw new FileNotFoundException("The game executable does not exist.", request.ExecutablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = false,
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory)
                ? Path.GetDirectoryName(request.ExecutablePath) ?? Environment.CurrentDirectory
                : request.WorkingDirectory,
        };

        foreach (var argument in CommandLineSplitter.Split(request.Arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException($"'{request.ExecutablePath}' could not be started.");
        }

        return Task.FromResult<IGameProcessHandle>(new WindowsGameProcessHandle(process, request.ExecutablePath));
    }
}

/// <summary>Handle to a launched game process.</summary>
public sealed class WindowsGameProcessHandle : IGameProcessHandle
{
    private readonly Process _process;
    private bool _disposed;

    public WindowsGameProcessHandle(Process process, string executablePath)
    {
        _process = process;
        ExecutablePath = executablePath;
    }

    public int ProcessId
    {
        get
        {
            try
            {
                return _process.Id;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }

    public string ExecutablePath { get; }

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (Exception)
            {
                return true;
            }
        }
    }

    public async Task<bool> WaitForExitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception)
        {
            return true;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.Dispose();
    }
}
