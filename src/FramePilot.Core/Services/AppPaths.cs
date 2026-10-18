using FramePilot.Core.Abstractions;

namespace FramePilot.Core.Services;

/// <summary>
/// Default implementation of <see cref="IAppPaths"/>. Everything is stored in
/// the user's local application data folder; nothing is uploaded anywhere.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string ApplicationFolderName = "FramePilot";

    public AppPaths(string? rootDirectory = null)
    {
        var root = rootDirectory;

        if (string.IsNullOrWhiteSpace(root))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            root = Path.Combine(localAppData, ApplicationFolderName);
        }

        DataDirectory = root;
        ReportsDirectory = Path.Combine(root, "Reports");
        CapturesDirectory = Path.Combine(root, "Captures");
        SessionJournalPath = Path.Combine(root, "session-journal.json");
        SettingsPath = Path.Combine(root, "settings.json");
    }

    public string DataDirectory { get; }

    public string ReportsDirectory { get; }

    public string CapturesDirectory { get; }

    public string SessionJournalPath { get; }

    public string SettingsPath { get; }

    /// <summary>Creates the data directories if they do not exist yet.</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ReportsDirectory);
        Directory.CreateDirectory(CapturesDirectory);
    }
}
