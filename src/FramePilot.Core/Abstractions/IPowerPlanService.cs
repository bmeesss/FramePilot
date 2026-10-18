using FramePilot.Core.Models;

namespace FramePilot.Core.Abstractions;

/// <summary>
/// Access to the Windows power plans. The implementation is expected to use
/// the documented <c>powercfg</c> command line interface and to verify the
/// result of a change instead of assuming success.
/// </summary>
public interface IPowerPlanService
{
    /// <summary>All power plans that exist on this machine.</summary>
    Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default);

    /// <summary>The power plan that is currently active, or <c>null</c> when it cannot be determined.</summary>
    Task<PowerPlanInfo?> GetActivePowerPlanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates the given power plan and returns the plan that is active
    /// afterwards (verified, not assumed).
    /// </summary>
    Task<PowerPlanOperationResult> SetActivePowerPlanAsync(Guid powerPlanGuid, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a power plan change.</summary>
public sealed record PowerPlanOperationResult(
    bool Success,
    Guid? ActivePlanGuid,
    string? ActivePlanName,
    string Message)
{
    public static PowerPlanOperationResult Failed(string message) => new(false, null, null, message);
}
