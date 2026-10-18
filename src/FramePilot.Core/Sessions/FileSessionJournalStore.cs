using System.Text.Json;
using System.Text.Json.Serialization;

namespace FramePilot.Core.Sessions;

/// <summary>
/// Stores the recovery journal as a JSON file. The file is written
/// atomically (temporary file plus replace) so that a crash during the write
/// cannot leave a half written journal behind.
/// </summary>
public sealed class FileSessionJournalStore : Abstractions.ISessionJournalStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public FileSessionJournalStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
    }

    public string FilePath { get; }

    public async Task<SessionJournalEntry?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var entry = await JsonSerializer
                .DeserializeAsync<SessionJournalEntry>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return entry;
        }
        catch (JsonException)
        {
            // A corrupted journal must not stop the application from starting.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task SaveAsync(SessionJournalEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = FilePath + ".tmp";

        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous))
        {
            await JsonSerializer
                .SerializeAsync(stream, entry, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, FilePath, overwrite: true);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }

        var temporaryPath = FilePath + ".tmp";
        if (File.Exists(temporaryPath))
        {
            File.Delete(temporaryPath);
        }

        return Task.CompletedTask;
    }
}
