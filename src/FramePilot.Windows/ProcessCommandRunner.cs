using System.Diagnostics;
using System.Text;

namespace FramePilot.Windows;

/// <summary>Result of an external command.</summary>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;

    public string CombinedOutput =>
        string.IsNullOrWhiteSpace(StandardError) ? StandardOutput : StandardOutput + Environment.NewLine + StandardError;
}

/// <summary>
/// Runs an external command without a shell. Arguments are passed through
/// <see cref="ProcessStartInfo.ArgumentList"/>, so user supplied values can
/// never be interpreted as a command.
/// </summary>
public sealed class ProcessCommandRunner
{
    private readonly TimeSpan _defaultTimeout;

    public ProcessCommandRunner(TimeSpan? defaultTimeout = null)
    {
        _defaultTimeout = defaultTimeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardOutput.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                standardError.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, string.Empty, $"'{fileName}' could not be started: {ex.Message}", false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = new CancellationTokenSource(timeout ?? _defaultTimeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellationToken);

        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);

            if (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return new CommandResult(-1, standardOutput.ToString(), standardError.ToString(), timedOut: true);
            }

            throw;
        }

        return new CommandResult(process.ExitCode, standardOutput.ToString(), standardError.ToString(), timedOut: false);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // The process may have exited between the check and the call.
        }
    }
}
