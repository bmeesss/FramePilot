using System.Collections.ObjectModel;
using FramePilot.Core.Abstractions;
using FramePilot.Core.Benchmarks;
using FramePilot.Core.Infrastructure;
using FramePilot.Core.PresentMon;
using FramePilot.Core.Services;

namespace FramePilot.App.ViewModels;

/// <summary>
/// Imports PresentMon recordings, analyses them and compares two
/// configurations.
/// </summary>
public sealed class BenchmarkViewModel : ViewModelBase
{
    private readonly IPresentMonService _presentMonService;
    private readonly IDialogService _dialogService;
    private readonly IFileDialogService _fileDialogService;
    private readonly SettingsService _settingsService;
    private readonly IAppPaths _paths;
    private readonly IAppLog _log;

    private IPresentMonCapture? _capture;
    private string _reportText = "Import a PresentMon recording to get started.";
    private string _statusMessage = string.Empty;
    private string _presentMonStatus = "PresentMon has not been checked yet.";
    private string _captureProcessName = string.Empty;
    private int _captureDurationSeconds = 60;
    private bool _isCapturing;
    private string _baselineConfiguration = "Baseline";
    private string _optimizedConfiguration = "Optimized";
    private int _minimumRuns = 5;

    public BenchmarkViewModel(
        IPresentMonService presentMonService,
        IDialogService dialogService,
        IFileDialogService fileDialogService,
        SettingsService settingsService,
        IAppPaths paths,
        IAppLog log)
    {
        _presentMonService = presentMonService;
        _dialogService = dialogService;
        _fileDialogService = fileDialogService;
        _settingsService = settingsService;
        _paths = paths;
        _log = log;

        ImportCommand = new AsyncRelayCommand(ImportAsync)
        {
            ErrorHandler = ex => StatusMessage = "The recording could not be imported: " + ex.Message,
        };

        CompareCommand = new AsyncRelayCommand(CompareAsync)
        {
            ErrorHandler = ex => StatusMessage = "The comparison failed: " + ex.Message,
        };

        SaveReportCommand = new AsyncRelayCommand(SaveReportAsync)
        {
            ErrorHandler = ex => StatusMessage = "The report could not be saved: " + ex.Message,
        };

        CheckPresentMonCommand = new AsyncRelayCommand(CheckPresentMonAsync)
        {
            ErrorHandler = ex => PresentMonStatus = "PresentMon could not be checked: " + ex.Message,
        };

        StartCaptureCommand = new AsyncRelayCommand(StartCaptureAsync)
        {
            ErrorHandler = ex => StatusMessage = "The recording could not be started: " + ex.Message,
        };

        StopCaptureCommand = new AsyncRelayCommand(StopCaptureAsync)
        {
            ErrorHandler = ex => StatusMessage = "The recording could not be stopped: " + ex.Message,
        };

        _ = LoadSettingsAsync();
    }

    public override string Title => "Benchmark Reports";

    public override string Description =>
        "FramePilot analyses real PresentMon CSV recordings. It never estimates a frame rate from CPU or memory usage.";

    public ObservableCollection<BenchmarkRunRow> Runs { get; } = new();

    public ObservableCollection<string> ConfigurationNames { get; } = new() { "Baseline", "Optimized" };

    public AsyncRelayCommand ImportCommand { get; }

    public AsyncRelayCommand CompareCommand { get; }

    public AsyncRelayCommand SaveReportCommand { get; }

    public AsyncRelayCommand CheckPresentMonCommand { get; }

    public AsyncRelayCommand StartCaptureCommand { get; }

    public AsyncRelayCommand StopCaptureCommand { get; }

    public string ReportText
    {
        get => _reportText;
        private set => SetProperty(ref _reportText, value);
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

    public string CaptureProcessName
    {
        get => _captureProcessName;
        set => SetProperty(ref _captureProcessName, value);
    }

    public int CaptureDurationSeconds
    {
        get => _captureDurationSeconds;
        set => SetProperty(ref _captureDurationSeconds, value);
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set => SetProperty(ref _isCapturing, value);
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

    public int MinimumRuns
    {
        get => _minimumRuns;
        set => SetProperty(ref _minimumRuns, value);
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _settingsService.LoadAsync().ConfigureAwait(true);

        BaselineConfiguration = settings.BaselineConfiguration;
        OptimizedConfiguration = settings.OptimizedConfiguration;
        MinimumRuns = settings.MinimumRunsPerConfiguration;
        CaptureDurationSeconds = settings.CaptureDurationSeconds;

        await CheckPresentMonAsync().ConfigureAwait(true);
    }

    private async Task ImportAsync()
    {
        var path = _fileDialogService.OpenFile(
            "Select a PresentMon recording",
            "PresentMon recordings (*.csv)|*.csv|All files (*.*)|*.*");

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await ImportFileAsync(path).ConfigureAwait(true);
    }

    private async Task ImportFileAsync(string path)
    {
        var parser = new PresentMonCsvParser();
        var result = parser.ParseFile(path);

        var statistics = FrameStatisticsCalculator.Calculate(result.Frames);
        var application = result.ProcessGroups.Count > 0 ? result.ProcessGroups[0].Application : "<unknown>";

        var run = new BenchmarkRun
        {
            Id = Guid.NewGuid().ToString("N"),
            Configuration = BaselineConfiguration,
            SourcePath = path,
            Application = application,
            Statistics = statistics,
            RunOrder = Runs.Count,
            FrameTimeColumn = result.FrameTimeColumn,
            FrameTimeDescription = result.FrameTimeDescription,
        };

        Runs.Add(BenchmarkRunRow.FromRun(run, ConfigurationNames));

        var warnings = result.Warnings.Count > 0 ? " " + string.Join(" ", result.Warnings) : string.Empty;

        StatusMessage = $"Imported {Path.GetFileName(path)}: {statistics.PresentedFrameCount} frames, " +
                        $"{statistics.AverageFps:0.0} FPS average, 1 % low {statistics.OnePercentLowFps:0.0} FPS.{warnings}";

        if (result.Warnings.Count > 0)
        {
            await _dialogService.ShowWarningAsync("The recording was imported with warnings", string.Join(Environment.NewLine, result.Warnings))
                .ConfigureAwait(true);
        }
    }

    private async Task CompareAsync()
    {
        var runs = Runs.Select((row, index) => row.ToRun(index)).ToList();

        var comparer = new BenchmarkComparer();
        var comparison = comparer.Compare(runs, BaselineConfiguration, OptimizedConfiguration, MinimumRuns);

        ReportText = BenchmarkReportWriter.WriteMarkdown(comparison, DateTimeOffset.UtcNow, BuildContext());

        StatusMessage = comparison.Headline;

        if (comparison.Verdict == ComparisonVerdict.InsufficientData ||
            comparison.Verdict == ComparisonVerdict.Inconclusive)
        {
            await _dialogService.ShowWarningAsync("The result is not conclusive", comparison.Summary).ConfigureAwait(true);
        }
    }

    private async Task SaveReportAsync()
    {
        if (string.IsNullOrWhiteSpace(ReportText) || ReportText.StartsWith("Import a PresentMon", StringComparison.Ordinal))
        {
            StatusMessage = "There is no report to save yet.";
            return;
        }

        var suggested = $"FramePilot-report-{DateTime.Now:yyyyMMdd-HHmm}.md";

        var path = _fileDialogService.SaveFile("Save the benchmark report", "Markdown (*.md)|*.md|Text (*.txt)|*.txt", suggested);

        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, ReportText).ConfigureAwait(true);

        StatusMessage = $"The report was saved to {path}";
    }

    private async Task CheckPresentMonAsync()
    {
        var location = await _presentMonService.RefreshAsync().ConfigureAwait(true);

        PresentMonStatus = location is null
            ? "PresentMon was not found. Install it from https://github.com/GameTechDev/PresentMon or set the path " +
              "of PresentMon.exe in the settings. FramePilot does not ship the tool."
            : $"Ready: {location.ExecutablePath}" + (location.Version is null ? string.Empty : $" (version {location.Version})");
    }

    private async Task StartCaptureAsync()
    {
        var location = await _presentMonService.RefreshAsync().ConfigureAwait(true);

        if (location is null)
        {
            PresentMonStatus = "PresentMon was not found. Install it from https://github.com/GameTechDev/PresentMon " +
                              "or set the path of PresentMon.exe in the settings.";
            StatusMessage = "A recording needs PresentMon, which is not available on this machine.";
            return;
        }

        Directory.CreateDirectory(_paths.CapturesDirectory);

        var fileName = $"PresentMon-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        var outputPath = Path.Combine(_paths.CapturesDirectory, fileName);

        var request = new PresentMonCaptureRequest(
            outputPath,
            string.IsNullOrWhiteSpace(CaptureProcessName) ? null : CaptureProcessName.Trim(),
            null,
            TimeSpan.FromSeconds(Math.Max(5, CaptureDurationSeconds)));

        _capture = await _presentMonService.StartCaptureAsync(request).ConfigureAwait(true);
        IsCapturing = true;

        StatusMessage = $"Recording started ({fileName}). PresentMon stops automatically after {request.Duration.TotalSeconds:0} seconds.";

        _ = WaitForCaptureAsync();
    }

    private async Task StopCaptureAsync()
    {
        if (_capture is null)
        {
            return;
        }

        var capture = _capture;
        _capture = null;

        await capture.RequestStopAsync().ConfigureAwait(true);

        StatusMessage = capture.WasStoppedForcibly
            ? "The recording was ended by force. The last frame of the recording can be missing - the analysis skips incomplete rows."
            : "The recording was stopped and the CSV file was closed properly.";
    }

    private async Task WaitForCaptureAsync()
    {
        var capture = _capture;

        if (capture is null)
        {
            return;
        }

        try
        {
            await capture.WaitForCompletionAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _log.Warning("The recording ended unexpectedly: " + ex.Message);
        }
        finally
        {
            IsCapturing = false;
        }

        if (File.Exists(capture.OutputFilePath))
        {
            await ImportFileAsync(capture.OutputFilePath).ConfigureAwait(true);
        }
        else
        {
            StatusMessage = "The recording did not produce a CSV file.";
        }
    }

    private string BuildContext()
    {
        var lines = new List<string>
        {
            $"* Baseline configuration: {BaselineConfiguration}",
            $"* Optimized configuration: {OptimizedConfiguration}",
            $"* Minimum runs per configuration: {MinimumRuns}",
            "* Use the same game, executable, graphics settings, scene or replay, resolution and test duration for every run.",
            "* Alternating baseline and optimized runs reduces time related bias (thermal state, background load).",
            "* Mark the first run after a driver or shader cache rebuild as a warm-up run; warm-up runs are excluded.",
        };

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>One imported recording with the values that were measured.</summary>
public sealed class BenchmarkRunRow : ObservableObject
{
    private string _configuration;
    private bool _isWarmUp;

    private BenchmarkRunRow(BenchmarkRun run, string configuration, bool isWarmUp)
    {
        Run = run;
        _configuration = configuration;
        _isWarmUp = isWarmUp;
    }

    public BenchmarkRun Run { get; }

    public string FileName => Path.GetFileName(Run.SourcePath);

    public string FullPath => Run.SourcePath;

    public string Application => Run.Application;

    public int FrameCount => Run.Statistics.PresentedFrameCount;

    public double AverageFps => Run.Statistics.AverageFps;

    public double OnePercentLowFps => Run.Statistics.OnePercentLowFps;

    public double MedianFrameTimeMs => Run.Statistics.MedianFrameTimeMs;

    public double P95FrameTimeMs => Run.Statistics.P95FrameTimeMs;

    public double P99FrameTimeMs => Run.Statistics.P99FrameTimeMs;

    public string FrameTimeColumn => Run.FrameTimeColumn;

    public string Configuration
    {
        get => _configuration;
        set => SetProperty(ref _configuration, value);
    }

    public bool IsWarmUp
    {
        get => _isWarmUp;
        set => SetProperty(ref _isWarmUp, value);
    }

    public static BenchmarkRunRow FromRun(BenchmarkRun run, IEnumerable<string> knownConfigurations)
    {
        var configuration = knownConfigurations.FirstOrDefault(c => string.Equals(c, run.Configuration, StringComparison.OrdinalIgnoreCase))
                            ?? run.Configuration;

        return new BenchmarkRunRow(run, configuration, run.IsWarmUp);
    }

    public BenchmarkRun ToRun(int order) => Run with
    {
        Configuration = Configuration,
        IsWarmUp = IsWarmUp,
        RunOrder = order,
    };
}
