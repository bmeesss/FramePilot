namespace FramePilot.Core.Abstractions;

/// <summary>
/// Locations FramePilot uses for local data. Everything stays on the machine
/// unless the user explicitly exports a report.
/// </summary>
public interface IAppPaths
{
    /// <summary>Root folder for all FramePilot data.</summary>
    string DataDirectory { get; }

    /// <summary>Folder where imported/created benchmark reports are stored.</summary>
    string ReportsDirectory { get; }

    /// <summary>Folder where PresentMon CSV captures are written.</summary>
    string CapturesDirectory { get; }

    /// <summary>Path of the recovery journal for game sessions.</summary>
    string SessionJournalPath { get; }

    /// <summary>Path of the user settings file.</summary>
    string SettingsPath { get; }
}
