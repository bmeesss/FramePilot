namespace FramePilot.Core.PresentMon;

/// <summary>
/// Finds a PresentMon executable. PresentMon is never bundled with FramePilot;
/// the user has to install it (or point FramePilot at a copy).
/// </summary>
public sealed class PresentMonLocator
{
    private readonly Func<string, bool> _fileExists;

    public PresentMonLocator(Func<string, bool>? fileExists = null)
    {
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>
    /// Returns the path of a PresentMon executable, or <c>null</c> when none
    /// was found.
    /// </summary>
    /// <param name="configuredPath">Path the user configured in the settings.</param>
    /// <param name="applicationDirectory">Directory of the running application.</param>
    public string? Locate(string? configuredPath, string? applicationDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            // An explicitly configured path is used as-is; if it is wrong the
            // user has to fix the setting instead of silently using a
            // different binary.
            return IsPresentMonExecutable(configuredPath) ? Path.GetFullPath(configuredPath) : null;
        }

        foreach (var candidate in EnumerateCandidates(applicationDirectory))
        {
            if (IsPresentMonExecutable(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>All locations that are inspected, in order.</summary>
    public static IEnumerable<string> EnumerateCandidates(string? applicationDirectory)
    {
        if (!string.IsNullOrWhiteSpace(applicationDirectory))
        {
            yield return Path.Combine(applicationDirectory, "PresentMon.exe");
            yield return Path.Combine(applicationDirectory, "PresentMon", "PresentMon.exe");
            yield return Path.Combine(applicationDirectory, "tools", "PresentMon.exe");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            yield return Path.Combine(localAppData, "PresentMon", "PresentMon.exe");
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            yield return Path.Combine(programFiles, "PresentMon", "PresentMon.exe");
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "PresentMon", "PresentMon.exe");
        }
    }

    private bool IsPresentMonExecutable(string path)
    {
        try
        {
            if (!_fileExists(path))
            {
                return false;
            }

            var name = Path.GetFileName(path);
            return name.StartsWith("PresentMon", StringComparison.OrdinalIgnoreCase) &&
                   name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
