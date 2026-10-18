using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Sessions;

/// <summary>
/// Manages a game session: it applies a temporary power plan, remembers the
/// original plan and restores it when the session ends.
/// </summary>
/// <remarks>
/// <para>
/// The controller never changes a power plan without an explicit request from
/// the user, and it never creates new plans. It only activates plans that
/// already exist on the machine.
/// </para>
/// <para>
/// A recovery journal is written before the plan is changed, so that an
/// interrupted FramePilot can restore the original plan on the next start.
/// </para>
/// </remarks>
public sealed class GameSessionController
{
    private readonly IPowerPlanService _powerPlanService;
    private readonly ISessionJournalStore _journalStore;
    private readonly IGameProcessLauncher _launcher;
    private readonly IAppLog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IGameProcessHandle? _gameProcess;

    public GameSessionController(
        IPowerPlanService powerPlanService,
        ISessionJournalStore journalStore,
        IGameProcessLauncher launcher,
        IAppLog log)
    {
        _powerPlanService = powerPlanService ?? throw new ArgumentNullException(nameof(powerPlanService));
        _journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>Current state of the session manager.</summary>
    public GameSessionState State { get; private set; } = GameSessionState.Idle;

    /// <summary>The journal of the running session, if there is one.</summary>
    public SessionJournalEntry? ActiveJournal { get; private set; }

    /// <summary>True while a game process is being monitored.</summary>
    public bool IsGameRunning => _gameProcess is { HasExited: false };

    /// <summary>Raised whenever <see cref="State"/> changes.</summary>
    public event EventHandler<GameSessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Starts a session: the original plan is remembered, the journal is
    /// written and the requested plan is activated and verified.
    /// </summary>
    public async Task<SessionOutcome> StartAsync(GameSessionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TargetPowerPlanGuid == Guid.Empty)
        {
            return SessionOutcome.Fail("No power plan was selected.");
        }

        if (options.LaunchGame)
        {
            if (string.IsNullOrWhiteSpace(options.GameExecutablePath))
            {
                return SessionOutcome.Fail("No game executable was selected.");
            }

            if (!File.Exists(options.GameExecutablePath))
            {
                return SessionOutcome.Fail($"The game executable '{options.GameExecutablePath}' does not exist.");
            }
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SessionOutcome.Fail("The operation was cancelled.");
        }

        try
        {
            SetState(GameSessionState.Preparing, "Preparing the game session.");

            var original = await _powerPlanService.GetActivePowerPlanAsync(cancellationToken).ConfigureAwait(false);
            if (original is null)
            {
                SetState(GameSessionState.Failed, "The active power plan could not be determined.");
                return SessionOutcome.Fail("The active power plan could not be determined, so no changes were made.");
            }

            var journal = new SessionJournalEntry
            {
                OriginalPowerPlanGuid = original.Guid,
                OriginalPowerPlanName = original.Name,
                AppliedPowerPlanGuid = options.TargetPowerPlanGuid,
                AppliedPowerPlanName = options.TargetPowerPlanName,
                GameExecutablePath = options.GameExecutablePath,
                State = SessionJournalState.Prepared,
                StartedAtUtc = DateTimeOffset.UtcNow,
            };

            await _journalStore.SaveAsync(journal, cancellationToken).ConfigureAwait(false);
            ActiveJournal = journal;

            if (original.Guid == options.TargetPowerPlanGuid)
            {
                journal = journal with { State = SessionJournalState.Applied };
                await _journalStore.SaveAsync(journal, cancellationToken).ConfigureAwait(false);
                ActiveJournal = journal;

                _log.Information($"Game session started. The selected plan '{original.Name}' was already active.");

                await LaunchGameAsync(options, journal, cancellationToken).ConfigureAwait(false);

                SetState(GameSessionState.Running, $"Session started. The plan '{original.Name}' was already active.");
                return new SessionOutcome(true, $"The session started. '{original.Name}' was already the active plan.", journal);
            }

            var result = await _powerPlanService
                .SetActivePowerPlanAsync(options.TargetPowerPlanGuid, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                await RestoreOriginalPlanAsync(journal, cancellationToken).ConfigureAwait(false);
                SetState(GameSessionState.Failed, result.Message);
                return SessionOutcome.Fail(result.Message);
            }

            if (result.ActivePlanGuid != options.TargetPowerPlanGuid)
            {
                var message =
                    $"The power plan '{options.TargetPowerPlanName}' could not be activated (the active plan is still " +
                    $"'{result.ActivePlanName ?? "unknown"}'). The original plan was restored.";

                _log.Warning(message);
                await RestoreOriginalPlanAsync(journal, cancellationToken).ConfigureAwait(false);
                SetState(GameSessionState.Failed, message);
                return SessionOutcome.Fail(message);
            }

            journal = journal with { State = SessionJournalState.Applied };
            await _journalStore.SaveAsync(journal, cancellationToken).ConfigureAwait(false);
            ActiveJournal = journal;

            _log.Information($"Game session started. Power plan changed from '{original.Name}' to '{options.TargetPowerPlanName}'.");

            await LaunchGameAsync(options, journal, cancellationToken).ConfigureAwait(false);

            SetState(GameSessionState.Running, $"Session started with the plan '{options.TargetPowerPlanName}'.");
            return new SessionOutcome(true, $"The session started with the plan '{options.TargetPowerPlanName}'.", journal);
        }
        catch (OperationCanceledException)
        {
            // The cancellation token is already cancelled, the restore must
            // still run to completion.
            await RestoreAfterFailureAsync(CancellationToken.None).ConfigureAwait(false);
            SetState(GameSessionState.Failed, "The session was cancelled.");
            return SessionOutcome.Fail("The session was cancelled.");
        }
        catch (Exception ex)
        {
            _log.Error("The game session could not be started: " + ex.Message);
            await RestoreAfterFailureAsync(CancellationToken.None).ConfigureAwait(false);
            SetState(GameSessionState.Failed, ex.Message);
            return SessionOutcome.Fail("The game session could not be started: " + ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Waits until the monitored game exits and then restores the original
    /// power plan.
    /// </summary>
    public async Task<SessionOutcome> WaitForGameExitAsync(CancellationToken cancellationToken = default)
    {
        var handle = _gameProcess;

        if (handle is null)
        {
            return SessionOutcome.Fail("No game process is being monitored.");
        }

        try
        {
            await handle.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SessionOutcome.Fail("Waiting for the game was cancelled.");
        }

        _log.Information("The game process exited, the original power plan is restored.");
        return await EndAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends the session and restores the original power plan.</summary>
    public async Task<SessionOutcome> EndAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return SessionOutcome.Fail("The operation was cancelled.");
        }

        try
        {
            var journal = ActiveJournal ?? await _journalStore.LoadAsync(cancellationToken).ConfigureAwait(false);

            if (journal is null)
            {
                SetState(GameSessionState.Idle, "No session is running.");
                return new SessionOutcome(true, "There was no active session.");
            }

            SetState(GameSessionState.Restoring, "Restoring the original power plan.");

            var restored = await RestoreOriginalPlanAsync(journal, cancellationToken).ConfigureAwait(false);
            ActiveJournal = null;

            if (restored)
            {
                SetState(GameSessionState.Idle, "The original power plan was restored.");
                return new SessionOutcome(true, "The original power plan was restored.", journal);
            }

            SetState(GameSessionState.Failed, "The original power plan could not be restored.");
            return new SessionOutcome(false, "The original power plan could not be restored. The recovery journal was kept, FramePilot will try again on the next start.", journal);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Cancels the session: the game process is not killed, the original power
    /// plan is restored and the journal is cleared.
    /// </summary>
    public async Task<SessionOutcome> CancelAsync(CancellationToken cancellationToken = default)
    {
        var outcome = await EndAsync(cancellationToken).ConfigureAwait(false);
        _gameProcess?.Dispose();
        _gameProcess = null;
        return outcome;
    }

    private async Task LaunchGameAsync(GameSessionOptions options, SessionJournalEntry journal, CancellationToken cancellationToken)
    {
        if (!options.LaunchGame || string.IsNullOrWhiteSpace(options.GameExecutablePath))
        {
            return;
        }

        var request = new GameLaunchRequest(
            options.GameExecutablePath,
            options.GameArguments,
            options.WorkingDirectory);

        _gameProcess = await _launcher.LaunchAsync(request, cancellationToken).ConfigureAwait(false);

        var updated = journal with { GameProcessId = _gameProcess.ProcessId };
        await _journalStore.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        ActiveJournal = updated;
    }

    private async Task<bool> RestoreOriginalPlanAsync(SessionJournalEntry journal, CancellationToken cancellationToken)
    {
        try
        {
            var plans = await _powerPlanService.GetPowerPlansAsync(cancellationToken).ConfigureAwait(false);
            var originalExists = plans.Any(p => p.Guid == journal.OriginalPowerPlanGuid);

            if (!originalExists)
            {
                _log.Warning("The original power plan no longer exists, the journal is cleared without restoring it.");
                await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var result = await _powerPlanService
                .SetActivePowerPlanAsync(journal.OriginalPowerPlanGuid, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success || result.ActivePlanGuid != journal.OriginalPowerPlanGuid)
            {
                _log.Error("The original power plan could not be restored: " + result.Message);
                return false;
            }

            var restored = journal with { State = SessionJournalState.Restored };
            await _journalStore.SaveAsync(restored, cancellationToken).ConfigureAwait(false);
            await _journalStore.ClearAsync(cancellationToken).ConfigureAwait(false);

            _log.Information($"The original power plan '{journal.OriginalPowerPlanName}' was restored.");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error("The original power plan could not be restored: " + ex.Message);
            return false;
        }
    }

    private async Task RestoreAfterFailureAsync(CancellationToken cancellationToken)
    {
        var journal = ActiveJournal;

        if (journal is null)
        {
            try
            {
                journal = await _journalStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                journal = null;
            }
        }

        if (journal is null)
        {
            return;
        }

        await RestoreOriginalPlanAsync(journal, cancellationToken).ConfigureAwait(false);
        ActiveJournal = null;
    }

    private void SetState(GameSessionState state, string message)
    {
        var previous = State;
        State = state;
        StateChanged?.Invoke(this, new GameSessionStateChangedEventArgs(previous, state, message));
    }
}
