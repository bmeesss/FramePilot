using FramePilot.Core.Abstractions;
using FramePilot.Core.Models;

namespace FramePilot.Core.Tests.Fakes;

/// <summary>
/// A power plan service that only changes an in memory state. The tests never
/// touch the settings of the machine they run on.
/// </summary>
public sealed class FakePowerPlanService : IPowerPlanService
{
    public FakePowerPlanService(params PowerPlanInfo[] plans)
    {
        Plans = plans.ToList();
        ActivePlan = Plans.FirstOrDefault();
    }

    public List<PowerPlanInfo> Plans { get; }

    public PowerPlanInfo? ActivePlan { get; set; }

    /// <summary>Plans for which the change is rejected.</summary>
    public HashSet<Guid> FailingPlans { get; } = new();

    /// <summary>When set, the change is reported as successful but a different plan stays active.</summary>
    public Guid? ForcedActivePlan { get; set; }

    public int SetCallCount { get; private set; }

    public List<Guid> AppliedPlans { get; } = new();

    public Task<IReadOnlyList<PowerPlanInfo>> GetPowerPlansAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PowerPlanInfo>>(Plans);

    public Task<PowerPlanInfo?> GetActivePowerPlanAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ActivePlan);

    public Task<PowerPlanOperationResult> SetActivePowerPlanAsync(Guid powerPlanGuid, CancellationToken cancellationToken = default)
    {
        SetCallCount++;
        AppliedPlans.Add(powerPlanGuid);

        if (FailingPlans.Contains(powerPlanGuid))
        {
            return Task.FromResult(PowerPlanOperationResult.Failed("The power plan could not be activated."));
        }

        if (ForcedActivePlan is { } forced)
        {
            return Task.FromResult(new PowerPlanOperationResult(true, forced, "Something else", "ok"));
        }

        var target = Plans.FirstOrDefault(p => p.Guid == powerPlanGuid);

        if (target is null)
        {
            return Task.FromResult(PowerPlanOperationResult.Failed("The power plan does not exist."));
        }

        ActivePlan = new PowerPlanInfo(target.Guid, target.Name, true);

        return Task.FromResult(new PowerPlanOperationResult(true, target.Guid, target.Name, "ok"));
    }
}
