using System.Reflection;

using FramePilot.App.Services;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.PresentMon;
using FramePilot.Core.Services;

namespace FramePilot.App.ViewModels;

/// <summary>User settings, storage locations and the safety notes.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly IAppPaths _paths;
    private readonly IPresentMonService _presentMonService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly IAppLog _log;

    private string _presentMonPath = string.Empty;
    private double _sampleIntervalSeconds = 1.0;
    private int _minimumRuns = 5;
    private string _baselineConfiguration = "Baseline";
    private string _optimizedConfiguration = "Optimized";
    private int _captureDurationSeconds = 60;
    private string _statusMessage = string.Empty;
    private string _presentMonStatus = string.Empty;

    public SettingsViewModel(
        SettingsService settingsService,
        IAppPaths paths,
        IPresentMonService presentMonService,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        IAppLog log)
    {
        _settingsService = settingsService;
        _paths = paths;
        _presentMonService = presentMonService;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _log = log;

        SaveCommand = new AsyncRelayCommand(SaveAsync)
        {
            ErrorHandler = ex => StatusMessage = "The settings could not be saved: " + ex.Message,
        };

        BrowsePresentMonCommand = new RelayCommand(BrowseForPresentMon);

        CheckPresentMonCommand = new AsyncRelayCommand(CheckPresentMonAsync)
        {
            ErrorHandler = ex => PresentMonStatus = "PresentMon could not be checked: " + ex.Message,
        };

        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);

        _ = LoadAsync();
    }

    public override string Title => "Settings";

    public override string Description =>
        "Everything FramePilot stores stays on this machine. There is no telemetry, no analytics and no upload of any kind.";

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand BrowsePresentMonCommand { get; }

    public AsyncRelayCommand CheckPresentMonCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public string PresentMonPath
    {
        get => _presentMonPath;
        set => SetProperty(ref _presentMonPath, value);
    }

    public double SampleIntervalSeconds
    {
        get => _sampleIntervalSeconds;
        set => SetProperty(ref _sampleIntervalSeconds, value);
    }

    public int MinimumRuns
    {
        get => _minimumRuns;
        set => SetProperty(ref _minimumRuns, value);
    }

    public string BaselineConfiguration
    {
        get => _baselineConfiguration;
        set => SetProperty(ref _baselineConfiguration, value);
    }

    public string OptimizedConfiguration
    {
        get => _optimizedConfiguration;
        set => SetProperty(ref _optimizedConfiguration, value);
    }

    public int CaptureDurationSeconds
    {
        get => _captureDurationSeconds;
        set => SetProperty(ref _captureDurationSeconds, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string PresentMonStatus
    {
        get => _presentMonStatus;
        private set => SetProperty(ref _presentMonStatus, value);
    }

    public string DataDirectory => _paths.DataDirectory;

    public string ReportsDirectory => _paths.ReportsDirectory;

    public string CapturesDirectory => _paths.CapturesDirectory;

    public string SessionJournalPath => _paths.SessionJournalPath;

    public string VersionText
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? "unknown" : version.ToString(3);
        }
    }

    private async Task LoadAsync()
    {
        var settings = await _settingsService.LoadAsync().ConfigureAwait(true);

        PresentMonPath = settings.PresentMonPath ?? string.Empty;
        SampleIntervalSeconds = settings.SampleIntervalSeconds;
        MinimumRuns = settings.MinimumRunsPerConfiguration;
        BaselineConfiguration = settings.BaselineConfiguration;
        OptimizedConfiguration = settings.OptimizedConfiguration;
        CaptureDurationSeconds = settings.CaptureDurationSeconds;

        await CheckPresentMonAsync().ConfigureAwait(true);
    }

    private async Task SaveAsync()
    {
        var settings = new AppSettings
        {
            PresentMonPath = string.IsNullOrWhiteSpace(PresentMonPath) ? null : PresentMonPath.Trim(),
            SampleIntervalSeconds = Math.Clamp(SampleIntervalSeconds, 0.25, 60),
            MinimumRunsPerConfiguration = Math.Clamp(MinimumRuns, 1, 100),
            BaselineConfiguration = string.IsNullOrWhiteSpace(BaselineConfiguration) ? "Baseline" : BaselineConfiguration.Trim(),
            OptimizedConfiguration = string.IsNullOrWhiteSpace(OptimizedConfiguration) ? "Optimized" : OptimizedConfiguration.Trim(),
            CaptureDurationSeconds = Math.Clamp(CaptureDurationSeconds, 5, 3600),
        };

        await _settingsService.SaveAsync(settings).ConfigureAwait(true);

        StatusMessage = "The settings were saved.";

        await CheckPresentMonAsync().ConfigureAwait(true);
    }

    private void BrowseForPresentMon()
    {
        var path = _fileDialogService.OpenFile(
            "Select PresentMon.exe",
            "PresentMon (*.exe)|PresentMon*.exe|Executable files (*.exe)|*.exe");

        if (!string.IsNullOrWhiteSpace(path))
        {
            PresentMonPath = path;
        }
    }

    private async Task CheckPresentMonAsync()
    {
        var location = await _presentMonService.RefreshAsync().ConfigureAwait(true);

        PresentMonStatus = location is null
            ? "PresentMon was not found. FramePilot can still analyse CSV recordings that you import manually."
            : $"Found {location.ExecutablePath}" + (location.Version is null ? string.Empty : $" (version {location.Version})");
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _paths.DataDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _log.Warning("The data folder could not be opened: " + ex.Message);
            StatusMessage = "The data folder could not be opened: " + ex.Message;
        }
    }
}
