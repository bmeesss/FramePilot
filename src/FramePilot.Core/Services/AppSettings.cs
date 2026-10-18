using System.Text.Json;
using System.Text.Json.Serialization;

namespace FramePilot.Core.Services;

/// <summary>User settings of FramePilot. They are stored locally as JSON.</summary>
public sealed record AppSettings
{
    /// <summary>Path of the PresentMon executable, if the user configured one.</summary>
    public string? PresentMonPath { get; init; }

    /// <summary>Interval between two dashboard samples in seconds.</summary>
    public double SampleIntervalSeconds { get; init; } = 1.0;

    /// <summary>Number of runs per configuration that are required for a comparison.</summary>
    public int MinimumRunsPerConfiguration { get; init; } = 5;

    /// <summary>Name of the baseline configuration.</summary>
    public string BaselineConfiguration { get; init; } = "Baseline";

    /// <summary>Name of the optimized configuration.</summary>
    public string OptimizedConfiguration { get; init; } = "Optimized";

    /// <summary>Default duration of a PresentMon capture in seconds.</summary>
    public int CaptureDurationSeconds { get; init; } = 60;

    /// <summary>Show processes that belong to the operating system in the process list.</summary>
    public bool ShowSystemProcesses { get; init; } = true;
}

/// <summary>Loads and stores the <see cref="AppSettings"/>.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    /// <summary>Reads the settings, falling back to the defaults.</summary>
    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var settings = await JsonSerializer
                .DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return settings ?? new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Writes the settings atomically.</summary>
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = _filePath + ".tmp";

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer
                    .SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }
}
