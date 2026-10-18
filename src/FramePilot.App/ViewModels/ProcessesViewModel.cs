using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.Metrics;
using FramePilot.Core.Models;
using FramePilot.Core.Processes;

namespace FramePilot.App.ViewModels;

/// <summary>
/// Lists the running applications and offers a graceful close request for a
/// process the user selected explicitly.
/// </summary>
public sealed class ProcessesViewModel : ViewModelBase
{
    private readonly SystemMonitor _monitor;
    private readonly IProcessController _processController;
    private readonly IDialogService _dialogService;
    private readonly DispatcherTimer _timer;

    private string _filterText = string.Empty;
    private bool _showSystemProcesses = true;
    private ProcessRow? _selectedProcess;
    private string _statusMessage = string.Empty;
    private bool _isRefreshing;

    public ProcessesViewModel(
        SystemMonitor monitor,
        IProcessController processController,
        IDialogService dialogService)
    {
        _monitor = monitor;
        _processController = processController;
        _dialogService = dialogService;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync)
        {
            ErrorHandler = ex => StatusMessage = "The processes could not be read: " + ex.Message,
        };

        CloseSelectedCommand = new AsyncRelayCommand(CloseSelectedAsync)
        {
            ErrorHandler = ex => StatusMessage = "The close request failed: " + ex.Message,
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    public override string Title => "Running Processes";

    public override string Description =>
        "FramePilot shows the processes that use CPU time or memory. It never terminates a process: it can only ask " +
        "the application you select to close itself.";

    public ObservableCollection<ProcessRow> Processes { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CloseSelectedCommand { get; }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public bool ShowSystemProcesses
    {
        get => _showSystemProcesses;
        set
        {
            if (SetProperty(ref _showSystemProcesses, value))
            {
                _ = RefreshAsync();
            }
        }
    }

    public ProcessRow? SelectedProcess
    {
        get => _selectedProcess;
        set => SetProperty(ref _selectedProcess, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;

        try
        {
            var snapshot = await Task.Run(() => _monitor.Sample()).ConfigureAwait(true);

            var filter = FilterText.Trim();

            var rows = snapshot.Processes
                .Where(p => ShowSystemProcesses || !p.IsSystemProcess)
                .Where(p => filter.Length == 0
                            || p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            || p.ProcessId.ToString() == filter)
                .Where(p => p.CpuPercent is > 0.1 || p.WorkingSetBytes > 64L * 1024 * 1024)
                .OrderByDescending(p => p.CpuPercent ?? 0)
                .ThenByDescending(p => p.WorkingSetBytes)
                .Take(200)
                .Select(ProcessRow.FromSnapshot)
                .ToList();

            var selectedId = SelectedProcess?.ProcessId;

            Processes.Clear();

            foreach (var row in rows)
            {
                Processes.Add(row);

                if (selectedId == row.ProcessId)
                {
                    SelectedProcess = row;
                }
            }

            if (selectedId is not null && SelectedProcess is null)
            {
                SelectedProcess = Processes.FirstOrDefault();
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task CloseSelectedAsync()
    {
        var process = SelectedProcess;

        if (process is null)
        {
            StatusMessage = "Select a process first.";
            return;
        }

        var snapshot = process.ToSnapshot();

        var decision = ProcessClosePolicy.Evaluate(snapshot, Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        if (decision.IsBlocked)
        {
            StatusMessage = decision.Reason;
            await _dialogService.ShowWarningAsync("The process cannot be closed", decision.Reason).ConfigureAwait(true);
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            "Close the application?",
            $"FramePilot will ask \"{process.Name}\" (PID {process.ProcessId}) to close.\n\n" +
            $"{decision.Reason}\n\n" +
            $"{decision.Warning}\n\n" +
            "The application can refuse or ask for a confirmation itself.",
            "Ask the application to close",
            "Do nothing").ConfigureAwait(true);

        if (!confirmed)
        {
            StatusMessage = "No close request was sent.";
            return;
        }

        var result = await _processController.RequestGracefulCloseAsync(process.ProcessId).ConfigureAwait(true);

        StatusMessage = result.Message;

        if (!result.Accepted)
        {
            await _dialogService.ShowWarningAsync("The application was not closed", result.Message).ConfigureAwait(true);
        }

        await RefreshAsync();
    }
}
