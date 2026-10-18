using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;
using FramePilot.Core.Power;

namespace FramePilot.Windows;

/// <summary>
/// Implements the power plan handling with the documented <c>powercfg</c>
/// command line tool.
/// </summary>
/// <remarks>
/// The tool is called without a shell and its arguments are passed through an
/// argument list. Every change is verified by reading the active plan back.
/// </remarks>
public sealed class WindowsPowerPlanService : IPowerPlanService
{
    private readonly ProcessCommandRunner _runner;

    public WindowsPowerPlanService(ProcessCommandRunner? runner = null)
    {
        _runner = runner ?? new ProcessCommandRunner(TimeSpan.FromSeconds(10));
    }

    /// <summary>Full path of the powercfg executable.</summary>
    public static string PowerCfgPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "powercfg.exe");

    public async Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner
            .RunAsync(PowerCfgPath, new[] { "/list" }, TimeSpan.FromSeconds(10), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return Array.Empty<PowerPlanInfo>();
        }

        return PowerPlanParser.ParseList(result.StandardOutput);
    }

    public async Task<PowerPlanInfo?> GetActivePowerPlanAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner
            .RunAsync(PowerCfgPath, new[] { "/getactivescheme" }, TimeSpan.FromSeconds(10), cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return null;
        }

        var guid = PowerPlanParser.ParseActivePlanGuid(result.StandardOutput);
        if (guid is null)
        {
            return null;
        }

        var name = PowerPlanParser.ParseActivePlanName(result.StandardOutput);

        return new PowerPlanInfo(guid.Value, string.IsNullOrWhiteSpace(name) ? guid.Value.ToString() : name, true);
    }

    public async Task<PowerPlanOperationResult> SetActivePowerPlanAsync(Guid powerPlanGuid, CancellationToken cancellationToken = default)
    {
        if (powerPlanGuid == Guid.Empty)
        {
            return PowerPlanOperationResult.Failed("No power plan was specified.");
        }

        var result = await _runner
            .RunAsync(
                PowerCfgPath,
                new[] { "/setactive", powerPlanGuid.ToString() },
                TimeSpan.FromSeconds(10),
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            var message = result.TimedOut
                ? "The powercfg command did not finish in time."
                : $"The power plan could not be activated: {result.StandardError.Trim()}".Trim();

            return PowerPlanOperationResult.Failed(message.Length == 0 ? "The power plan could not be activated." : message);
        }

        // Verify instead of trusting the exit code.
        var active = await GetActivePowerPlanAsync(cancellationToken).ConfigureAwait(false);

        if (active is null)
        {
            return new PowerPlanOperationResult(false, null, null, "The active power plan could not be read back to verify the change.");
        }

        if (active.Guid != powerPlanGuid)
        {
            return new PowerPlanOperationResult(
                false,
                active.Guid,
                active.Name,
                $"The power plan '{active.Name}' is still active, the requested plan was not applied.");
        }

        return new PowerPlanOperationResult(true, active.Guid, active.Name, $"The power plan '{active.Name}' is active.");
    }
}
