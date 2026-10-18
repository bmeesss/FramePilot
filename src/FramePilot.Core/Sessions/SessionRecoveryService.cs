using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Sessions;

/// <summary>Result of the recovery check that runs when FramePilot starts.</summary>
public sealed record RecoveryOutcome(bool Restored, bool JournalCleared, string Message)
{
    public static RecoveryOutcome NothingToDo(string message) => new(false, true, message);
}

/// <summary>
/// Restores the power plan of a session that was interrupted (for example
/// because FramePilot or the machine was shut down during a game session).
/// </summary>
/// <remarks>
/// The original plan is only restored when the plan that is currently active
/// is still the temporary plan FramePilot applied. If the user changed the
/// power plan themselves in the meantime, that choice is left untouched.
/// </remarks>
public sealed class SessionRecoveryService
{
    private readonly IPowerPlanService _powerPlanService;
    private readonly ISessionJournalStore _journalStore;
    private readonly IAppLog _log;

    public SessionRecoveryService(
        IPowerPlanService powerPlanService,
        ISessionJournalStore journalStore,
        IAppLog log)
    {
        _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Checks the journal and restores the original plan when needed.</summary>
    public async Task<RecoveryOutcome> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var journal = await _journalStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (journal is null)
        {
            return RecoveryOutcome.NothingToDo("No interrupted game session was found.");
        }

        if (journal.State != SessionJournalState.Applied)
        {
            await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return RecoveryOutcome.NothingToDo("The journal of the last session did not contain an active power plan change.");
        }

        var active = await _powerPlanService.GetActivePowerPlanAsync(cancellationToken).ConfigureAwait(false);

        if (active is null)
        {
            return new RecoveryOutcome(false, false, "The active power plan could not be determined, so nothing was changed.");
        }

        if (active.Guid != journal.AppliedPowerPlanGuid)
        {
            _log.Information(
                "The active power plan was changed outside of FramePilot, the current choice was kept untouched.");

            await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);

            return new RecoveryOutcome(
                false,
                true,
                $"An interrupted game session was found, but the power plan was changed outside of FramePilot. " +
                $"The active plan '{active.Name}' was left untouched.");
        }

        var plans = await _powerPlanService.GetPowerPlansAsync(cancellationToken).ConfigureAwait(false);
        if (!plans.Any(p => p.Guid == journal.OriginalPowerPlanGuid))
        {
            await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return new RecoveryOutcome(
                false,
                true,
                $"An interrupted game session was found, but the original power plan '{journal.OriginalPowerPlanName}' no longer exists. " +
                "The current plan was left untouched.");
        }

        var result = await _powerPlanService
            .SetActivePowerPlanAsync(journal.OriginalPowerPlanGuid, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success || result.ActivePlanGuid != journal.OriginalPowerPlanGuid)
        {
            _log.Error("The recovery of the interrupted session failed: " + result.Message);
            return new RecoveryOutcome(
                false,
                false,
                "The interrupted game session could not be recovered automatically. " + result.Message);
        }

        await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        _log.Information($"The interrupted game session was recovered, the power plan '{journal.OriginalPowerPlanName}' was restored.");

        return new RecoveryOutcome(
            true,
            true,
            $"An interrupted game session was recovered: the original power plan '{journal.OriginalPowerPlanName}' was restored.");
    }
}
