using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using FramePilot.App.Views;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.Sessions;

namespace FramePilot.App.ViewModels;

/// <summary>The shell of the application: navigation, status line and the
/// recovery check that runs when FramePilot starts.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly IAppLog _log;
    private NavigationItem? _selectedItem;
    private string _statusText = "Ready.";

    public MainViewModel(
        DashboardViewModel dashboard,
        GameSessionViewModel gameSession,
        BenchmarkViewModel benchmark,
        ProcessesViewModel processes,
        SettingsViewModel settings,
        SessionRecoveryService recoveryService,
        IAppLog log)
    {
        _log = log;

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Dashboard", "Live system metrics", dashboard, new DashboardView(dashboard)),
            new("Game Sessions", "Reversible power plan sessions", gameSession, new GameSessionView(gameSession)),
            new("Benchmarks", "PresentMon analysis and reports", benchmark, new BenchmarkView(benchmark)),
            new("Processes", "Background applications", processes, new ProcessesView(processes)),
            new("Settings", "Paths, sampling and safety", settings, new SettingsView(settings)),
        };

        SelectedItem = NavigationItems[0];

        _ = RunRecoveryAsync(recoveryService);
    }

    public override string Title => "FramePilot";

    public override string Description => "Evidence based Windows performance and gaming optimization.";

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnPropertyChanged(nameof(SelectedTitle));
                OnPropertyChanged(nameof(SelectedDescription));
            }
        }
    }

    public string SelectedTitle => SelectedItem?.Title ?? string.Empty;

    public string SelectedDescription => SelectedItem?.Description ?? string.Empty;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private async Task RunRecoveryAsync(SessionRecoveryService recoveryService)
    {
        try
        {
            var outcome = await recoveryService.RecoverAsync().ConfigureAwait(true);

            if (outcome.Restored)
            {
                StatusText = outcome.Message;
            }
            else if (!string.IsNullOrWhiteSpace(outcome.Message) &&
                     !outcome.Message.StartsWith("No interrupted", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = outcome.Message;
            }
        }
        catch (Exception ex)
        {
            _log.Warning("The recovery check failed: " + ex.Message);
            StatusText = "The interrupted session check could not be completed.";
        }
    }
}
