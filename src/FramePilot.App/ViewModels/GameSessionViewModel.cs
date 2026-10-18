using System.Collections.ObjectModel;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.Models;
using FramePilot.Core.Sessions;

namespace FramePilot.App.ViewModels;

/// <summary>
/// Manages a game session: the user picks a game and a power plan, FramePilot
/// applies the plan and restores the original plan when the session ends.
/// </summary>
public sealed class GameSessionViewModel : ViewModelBase
{
    private readonly GameSessionController _controller;
    private readonly IPowerPlanService _powerPlanService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IAppLog _log;

    private PowerPlanInfo? _selectedPowerPlan;
    private string _gamePath = string.Empty;
    private string _gameArguments = string.Empty;
    private bool _launchGame = true;
    private string _statusMessage = "No session is running.";
    private string _sessionStateText = "Idle";
    private bool _isBusy;

    public GameSessionViewModel(
        GameSessionController controller,
        IPowerPlanService powerPlanService,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        IAppLog log)
    {
        _controller = controller;
        _powerPlanService = powerPlanService;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _log = log;

        _controller.StateChanged += OnSessionStateChanged;

        LoadPlansCommand = new AsyncRelayCommand(LoadPlansAsync)
        {
            ErrorHandler = ex => StatusMessage = "The power plans could not be read: " + ex.Message,
        };

        BrowseGameCommand = new RelayCommand(BrowseForGame);

        StartCommand = new AsyncRelayCommand(StartSessionAsync)
        {
            ErrorHandler = ex => StatusMessage = "The session could not be started: " + ex.Message,
        };

        EndCommand = new AsyncRelayCommand(EndSessionAsync)
        {
            ErrorHandler = ex => StatusMessage = "The session could not be ended: " + ex.Message,
        };

        CancelCommand = new AsyncRelayCommand(CancelSessionAsync)
        {
            ErrorHandler = ex => StatusMessage = "The session could not be cancelled: " + ex.Message,
        };
    }

    public override string Title => "Game Sessions";

    public override string Description =>
        "FramePilot activates a power plan that already exists on this machine and restores the previous one " +
        "when the session ends. It never creates power plans and never changes one without your confirmation.";

    public ObservableCollection<PowerPlanInfo> PowerPlans { get; } = new();

    public AsyncRelayCommand LoadPlansCommand { get; }

    public RelayCommand BrowseGameCommand { get; }

    public AsyncRelayCommand StartCommand { get; }

    public AsyncRelayCommand EndCommand { get; }

    public AsyncRelayCommand CancelCommand { get; }

    public PowerPlanInfo? SelectedPowerPlan
    {
        get => _selectedPowerPlan;
        set => SetProperty(ref _selectedPowerPlan, value);
    }

    public string GamePath
    {
        get => _gamePath;
        set => SetProperty(ref _gamePath, value);
    }

    public string GameArguments
    {
        get => _gameArguments;
        set => SetProperty(ref _gameArguments, value);
    }

    public bool LaunchGame
    {
        get => _launchGame;
        set => SetProperty(ref _launchGame, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SessionStateText
    {
        get => _sessionStateText;
        private set => SetProperty(ref _sessionStateText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public bool IsSessionRunning => _controller.State is GameSessionState.Running or GameSessionState.Preparing;

    private async Task LoadPlansAsync()
    {
        IsBusy = true;

        try
        {
            var plans = await _powerPlanService.GetPowerPlansAsync().ConfigureAwait(true);

            PowerPlans.Clear();

            foreach (var plan in plans)
            {
                PowerPlans.Add(plan);
            }

            if (SelectedPowerPlan is null)
            {
                SelectedPowerPlan = PowerPlans.FirstOrDefault(p => p.IsActive) ?? PowerPlans.FirstOrDefault();
            }

            StatusMessage = PowerPlans.Count > 0
                ? $"{PowerPlans.Count} power plan(s) found."
                : "No power plans were reported by powercfg.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BrowseForGame()
    {
        var path = _fileDialogService.OpenFile(
            "Select the game executable",
            "Executable files (*.exe)|*.exe|All files (*.*)|*.*");

        if (!string.IsNullOrWhiteSpace(path))
        {
            GamePath = path;
        }
    }

    private async Task StartSessionAsync()
    {
        if (SelectedPowerPlan is null)
        {
            StatusMessage = "Select a power plan first.";
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Start the game session?",
            $"The active power plan will be switched to \"{SelectedPowerPlan.Name}\".\n\n" +
            "A different power plan can increase power consumption and temperatures, and it does not necessarily " +
            "improve the frame rate. The previous plan is restored when the session ends, when the game exits, or " +
            "if FramePilot is closed unexpectedly.\n\n" +
            "Continue?",
            "Start session",
            "Cancel").ConfigureAwait(true);

        if (!confirmed)
        {
            StatusMessage = "The session was not started.";
            return;
        }

        IsBusy = true;

        try
        {
            var options = new GameSessionOptions
            {
                TargetPowerPlanGuid = SelectedPowerPlan.Guid,
                TargetPowerPlanName = SelectedPowerPlan.Name,
                GameExecutablePath = string.IsNullOrWhiteSpace(GamePath) ? null : GamePath,
                GameArguments = string.IsNullOrWhiteSpace(GameArguments) ? null : GameArguments,
                LaunchGame = LaunchGame && !string.IsNullOrWhiteSpace(GamePath),
            };

            var outcome = await _controller.StartAsync(options).ConfigureAwait(true);

            StatusMessage = outcome.Message;

            if (outcome.Success && _controller.IsGameRunning)
            {
                _ = WatchGameAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EndSessionAsync()
    {
        IsBusy = true;

        try
        {
            var outcome = await _controller.EndAsync().ConfigureAwait(true);
            StatusMessage = outcome.Message;

            if (!outcome.Success)
            {
                await _dialogService.ShowWarningAsync("The session could not be ended", outcome.Message).ConfigureAwait(true);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CancelSessionAsync()
    {
        var confirmed = await _dialogService.ConfirmAsync(
            "Cancel the game session?",
            "The original power plan is restored. The game keeps running - FramePilot never terminates it.",
            "Cancel session",
            "Keep the session").ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var outcome = await _controller.CancelAsync().ConfigureAwait(true);
            StatusMessage = outcome.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task WatchGameAsync()
    {
        try
        {
            var outcome = await _controller.WaitForGameExitAsync().ConfigureAwait(true);
            StatusMessage = outcome.Success
                ? "The game exited, the original power plan was restored."
                : outcome.Message;

            _log.Information(StatusMessage);
        }
        catch (Exception ex)
        {
            _log.Warning("The game could not be monitored: " + ex.Message);
        }
    }

    private void OnSessionStateChanged(object? sender, GameSessionStateChangedEventArgs e)
    {
        SessionStateText = e.Current.ToString();

        if (!string.IsNullOrWhiteSpace(e.Message))
        {
            StatusMessage = e.Message;
        }

        OnPropertyChanged(nameof(IsSessionRunning));
    }
}
