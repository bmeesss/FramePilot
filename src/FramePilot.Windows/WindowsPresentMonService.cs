using System.Diagnostics;
using FramePilot.Core.Abstractions;
using FramePilot.Core.PresentMon;

namespace FramePilot.Windows;

/// <summary>
/// Locates and runs a PresentMon executable that the user installed.
/// </summary>
/// <remarks>
/// PresentMon is never bundled with FramePilot. When no executable can be
/// found the service reports that, and the user interface explains how to
/// install it.
/// </remarks>
public sealed class WindowsPresentMonService : IPresentMonService
{
    private readonly PresentMonLocator _locator;
    private readonly Func<string, string?> _versionReader;
    private readonly string? _configuredPath;
    private readonly string? _applicationDirectory;

    public WindowsPresentMonService(
        string? configuredPath = null,
        string? applicationDirectory = null,
        Func<string, string?>? versionReader = null)
    {
        _locator = new PresentMonLocator();
        _versionReader = versionReader ?? ReadFileVersion;
        _configuredPath = configuredPath;
        _applicationDirectory = applicationDirectory ?? AppContext.BaseDirectory;
    }

    public PresentMonLocation? Location { get; private set; }

    public Task<PresentMonLocation?> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = _locator.Locate(_configuredPath, _applicationDirectory);

        Location = path is null ? null : new PresentMonLocation(path, _versionReader(path));

        return Task.FromResult(Location);
    }

    public async Task<IPresentMonCapture> StartCaptureAsync(PresentMonCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var location = Location ?? await RefreshAsync(cancellationToken).ConfigureAwait(false);

        if (location is null)
        {
            throw new InvalidOperationException(
                "PresentMon was not found. Install PresentMon (https://github.com/GameTechDev/PresentMon) or set the " +
                "path of PresentMon.exe in the FramePilot settings.");
        }

        var directory = Path.GetDirectoryName(request.OutputFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new PresentMonCaptureOptions
        {
            OutputFilePath = request.OutputFilePath,
            ProcessName = request.ProcessName,
            ProcessId = request.ProcessId,
            Duration = request.Duration,
            SessionName = "FramePilot-" + Guid.NewGuid().ToString("N")[..8],
        };

        var arguments = PresentMonCommandBuilder.BuildCaptureArguments(options);

        var processId = NativeConsoleProcess.Start(location.ExecutablePath, arguments);

        if (processId is null)
        {
            throw new InvalidOperationException(
                $"'{location.ExecutablePath}' could not be started. Check that the file is a valid PresentMon executable.");
        }

        return new WindowsPresentMonCapture(processId.Value, request.OutputFilePath);
    }

    private static string? ReadFileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var version = info.FileVersion;

            if (string.IsNullOrWhiteSpace(version))
            {
                return null;
            }

            var parts = version.Split('.');
            return parts.Length >= 3
                ? $"{parts[0]}.{parts[1]}.{parts[2]}"
                : version;
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>A running PresentMon capture.</summary>
public sealed class WindowsPresentMonCapture : IPresentMonCapture
{
    private readonly int _processId;
    private bool _disposed;

    public WindowsPresentMonCapture(int processId, string outputFilePath)
    {
        _processId = processId;
        OutputFilePath = outputFilePath;
    }

    public string OutputFilePath { get; }

    public bool IsRunning => TryGetProcess() is { HasExited: false };

    public bool WasStoppedForcibly { get; private set; }

    public async Task<bool> WaitForCompletionAsync(CancellationToken cancellationToken)
    {
        var process = TryGetProcess();

        if (process is null)
        {
            return true;
        }

        using (process)
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
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
    }

    public async Task RequestStopAsync(CancellationToken cancellationToken = default)
    {
        var process = TryGetProcess();

        if (process is null)
        {
            return;
        }

        using (process)
        {
            if (process.HasExited)
            {
                return;
            }

            // Give PresentMon a moment to install its console handler.
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);

            if (NativeConsoleProcess.TryRequestStop(_processId))
            {
                var exited = await WaitForExitAsync(process, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                if (exited)
                {
                    return;
                }
            }

            // Last resort: end the process. The recording can lose its last
            // frame, the analysis skips incomplete rows and reports it.
            try
            {
                process.Kill(entireProcessTree: true);
                WasStoppedForcibly = true;
            }
            catch (Exception)
            {
                // The process may have exited in the meantime.
            }
        }
    }

    public void Dispose() => _disposed = true;

    private Process? TryGetProcess()
    {
        if (_disposed)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById(_processId);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
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
}
