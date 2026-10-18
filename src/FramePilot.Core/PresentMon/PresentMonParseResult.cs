namespace FramePilot.Core.PresentMon;

/// <summary>
/// The result of parsing a PresentMon CSV file.
/// </summary>
public sealed class PresentMonParseResult
{
    public PresentMonParseResult(
        PresentMonSchemaKind schema,
        string frameTimeColumn,
        string frameTimeDescription,
        IReadOnlyList<PresentMonFrame> frames,
        IReadOnlyList<string> warnings,
        int totalDataRows,
        int skippedRows)
    {
        Schema = schema;
        FrameTimeColumn = frameTimeColumn;
        FrameTimeDescription = frameTimeDescription;
        Frames = frames;
        Warnings = warnings;
        TotalDataRows = totalDataRows;
        SkippedRows = skippedRows;
        ProcessGroups = BuildGroups(frames);
    }

    /// <summary>The detected PresentMon dialect.</summary>
    public PresentMonSchemaKind Schema { get; }

    /// <summary>Name of the column the frame time was taken from.</summary>
    public string FrameTimeColumn { get; }

    /// <summary>Human readable meaning of the frame time column.</summary>
    public string FrameTimeDescription { get; }

    /// <summary>All usable frames of the recording.</summary>
    public IReadOnlyList<PresentMonFrame> Frames { get; }

    /// <summary>Non fatal problems that were found while parsing.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Number of data rows in the file (including skipped ones).</summary>
    public int TotalDataRows { get; }

    /// <summary>Number of data rows that could not be used.</summary>
    public int SkippedRows { get; }

    /// <summary>The frames grouped by process, so that multi process recordings can be split.</summary>
    public IReadOnlyList<PresentMonProcessGroup> ProcessGroups { get; }

    private static IReadOnlyList<PresentMonProcessGroup> BuildGroups(IReadOnlyList<PresentMonFrame> frames)
    {
        var groups = new List<PresentMonProcessGroup>();

        var byProcess = new Dictionary<(string Application, int ProcessId), List<PresentMonFrame>>();

        foreach (var frame in frames)
        {
            var key = (frame.Application, frame.ProcessId);
            if (!byProcess.TryGetValue(key, out var list))
            {
                list = new List<PresentMonFrame>();
                byProcess[key] = list;
            }

            list.Add(frame);
        }

        foreach (var pair in byProcess.OrderBy(p => p.Key.Application, StringComparer.OrdinalIgnoreCase))
        {
            groups.Add(new PresentMonProcessGroup(pair.Key.Application, pair.Key.ProcessId, pair.Value));
        }

        return groups;
    }
}

/// <summary>All frames that belong to one process.</summary>
public sealed record PresentMonProcessGroup(string Application, int ProcessId, IReadOnlyList<PresentMonFrame> Frames);
