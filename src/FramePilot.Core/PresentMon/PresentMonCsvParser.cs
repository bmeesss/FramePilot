using System.Globalization;
using System.Text;

namespace FramePilot.Core.PresentMon;

/// <summary>
/// Parses CSV files produced by the official PresentMon console application.
/// </summary>
/// <remarks>
/// <para>
/// FramePilot supports the three layouts that PresentMon can write:
/// </para>
/// <list type="bullet">
/// <item><description>PresentMon 1.x (<c>--v1_metrics</c>): <c>msBetweenPresents</c>.</description></item>
/// <item><description>PresentMon 2.x (<c>--v2_metrics</c>): <c>FrameTime</c>.</description></item>
/// <item><description>PresentMon 2.1+ default output: <c>MsBetweenPresents</c>.</description></item>
/// </list>
/// <para>
/// Any other file is rejected with a descriptive error instead of being
/// silently interpreted. Rows that cannot be parsed are skipped and reported
/// as warnings, so a truncated recording (for example when PresentMon was
/// killed) still yields usable data.
/// </para>
/// </remarks>
public sealed class PresentMonCsvParser
{
    private const string ColumnApplication = "Application";
    private const string ColumnProcessId = "ProcessID";
    private const string ColumnFrameTimeV2 = "FrameTime";
    private const string ColumnFrameTimeV1 = "msBetweenPresents";
    private const string ColumnFrameTimeLegacy = "MsBetweenPresents";
    private const string ColumnFrameTimeLegacyFallback = "MsBetweenAppStart";
    private const string ColumnDropped = "Dropped";
    private const string ColumnDisplayedTime = "DisplayedTime";
    private const string ColumnUntilDisplayed = "MsUntilDisplayed";
    private const string ColumnUntilDisplayedV1 = "msUntilDisplayed";

    /// <summary>Frame times above this value are considered corrupt.</summary>
    private const double MaximumPlausibleFrameTimeMs = 100_000;

    /// <summary>Parses a CSV document that was read into memory.</summary>
    /// <exception cref="PresentMonParseException">The document is not a usable PresentMon CSV.</exception>
    public PresentMonParseResult Parse(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new PresentMonParseException(
                PresentMonParseError.EmptyFile,
                "The selected file is empty. A PresentMon recording must contain a header row and at least one frame.");
        }

        var lines = SplitLines(csvText);

        var headerIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex < 0)
        {
            throw new PresentMonParseException(
                PresentMonParseError.EmptyFile,
                "The selected file does not contain any data.");
        }

        var header = SplitCsvLine(lines[headerIndex]);
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim().Trim('"');
            if (name.Length > 0 && !columns.ContainsKey(name))
            {
                columns[name] = i;
            }
        }

        var schema = DetectSchema(columns, header);

        if (schema == PresentMonSchemaKind.Unknown)
        {
            throw new PresentMonParseException(
                PresentMonParseError.UnsupportedSchema,
                "This file is not a supported PresentMon recording. Expected a PresentMon CSV header containing " +
                "'FrameTime' (PresentMon 2.x metrics) or 'MsBetweenPresents' / 'msBetweenPresents' (PresentMon 1.x and 2.1+). " +
                $"Header found: {string.Join(", ", header)}");
        }

        var frameTimeColumn = ResolveFrameTimeColumn(columns, schema);
        var applicationColumn = FindColumn(columns, ColumnApplication);
        var processIdColumn = FindColumn(columns, ColumnProcessId);
        var droppedColumn = FindColumn(columns, ColumnDropped);
        var displayColumn = ResolveDisplayColumn(columns, schema);

        var frames = new List<PresentMonFrame>();
        var warnings = new List<string>();
        var dataRows = 0;
        var skippedRows = 0;
        var truncatedTail = false;

        for (var lineIndex = headerIndex + 1; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            dataRows++;
            var fields = SplitCsvLine(line);

            if (fields.Count != header.Count)
            {
                skippedRows++;
                var isLastRow = lineIndex == lines.Count - 1;
                truncatedTail = truncatedTail || isLastRow;
                if (warnings.Count < 25)
                {
                    warnings.Add(isLastRow
                        ? $"The last row of the file is incomplete ({fields.Count} of {header.Count} columns) and was skipped. " +
                          "This usually happens when a recording was interrupted."
                        : $"Row {lineIndex + 1} has {fields.Count} columns instead of {header.Count} and was skipped.");
                }

                continue;
            }

            var application = applicationColumn >= 0 ? fields[applicationColumn].Trim() : string.Empty;
            if (application.Length == 0)
            {
                application = "<unknown>";
            }

            if (processIdColumn < 0 || !int.TryParse(fields[processIdColumn].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId))
            {
                skippedRows++;
                if (warnings.Count < 25)
                {
                    warnings.Add($"Row {lineIndex + 1} has no readable ProcessID and was skipped.");
                }

                continue;
            }

            if (!TryParseFrameTime(fields[frameTimeColumn], out var frameTime))
            {
                skippedRows++;
                if (warnings.Count < 25)
                {
                    warnings.Add($"Row {lineIndex + 1} has no usable '{header[frameTimeColumn].Trim()}' value and was skipped.");
                }

                continue;
            }

            var wasDisplayed = true;
            if (displayColumn >= 0)
            {
                wasDisplayed = !string.Equals(fields[displayColumn].Trim(), "NA", StringComparison.OrdinalIgnoreCase);
            }

            var isDropped = false;
            if (droppedColumn >= 0)
            {
                isDropped = ParseDroppedFlag(fields[droppedColumn]);
            }
            else if (!wasDisplayed)
            {
                // The 2.x layouts have no explicit Dropped column; a frame that
                // was never displayed was not shown to the user.
                isDropped = true;
            }

            frames.Add(new PresentMonFrame(application, processId, frameTime, wasDisplayed, isDropped, frames.Count));
        }

        if (frames.Count == 0)
        {
            var detail = skippedRows > 0
                ? $" All {skippedRows} data rows were invalid."
                : string.Empty;

            throw new PresentMonParseException(
                PresentMonParseError.NoFrameData,
                $"The file is a valid PresentMon CSV but contains no usable frame rows.{detail}");
        }

        if (truncatedTail)
        {
            warnings.Add("The recording was truncated; the analysis only covers the complete rows.");
        }

        var frameTimeColumnName = header[frameTimeColumn].Trim();
        var description = DescribeFrameTimeColumn(schema, frameTimeColumnName);

        return new PresentMonParseResult(
            schema,
            frameTimeColumnName,
            description,
            frames,
            warnings,
            dataRows,
            skippedRows);
    }

    /// <summary>Reads and parses a CSV file from disk.</summary>
    /// <exception cref="PresentMonParseException">The file is missing or not a usable PresentMon CSV.</exception>
    public PresentMonParseResult ParseFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new PresentMonParseException(
                PresentMonParseError.EmptyFile,
                $"The file '{path}' does not exist.");
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new PresentMonParseException(
                PresentMonParseError.EmptyFile,
                $"The file '{path}' could not be read: {ex.Message}",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PresentMonParseException(
                PresentMonParseError.EmptyFile,
                $"The file '{path}' could not be read: access was denied.",
                ex);
        }

        return Parse(text);
    }

    /// <summary>
    /// Splits a CSV line into its fields. Quoted fields and escaped quotes
    /// (<c>""</c>) are supported.
    /// </summary>
    public static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        if (line is null)
        {
            return fields;
        }

        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(c);
            }
        }

        fields.Add(builder.ToString());
        return fields;
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var builder = new StringBuilder();
        var start = 0;

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            start = 1;
        }

        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                lines.Add(builder.ToString());
                builder.Clear();
            }
            else if (c == '\n')
            {
                lines.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(c);
            }
        }

        if (builder.Length > 0)
        {
            lines.Add(builder.ToString());
        }

        return lines;
    }

    private static PresentMonSchemaKind DetectSchema(IReadOnlyDictionary<string, int> columns, IReadOnlyList<string> header)
    {
        if (columns.ContainsKey(ColumnFrameTimeV2))
        {
            return PresentMonSchemaKind.PresentMonV2;
        }

        if (columns.ContainsKey(ColumnFrameTimeV1))
        {
            return PresentMonSchemaKind.PresentMonV1;
        }

        if (columns.ContainsKey(ColumnFrameTimeLegacy) || columns.ContainsKey(ColumnFrameTimeLegacyFallback))
        {
            return PresentMonSchemaKind.PresentMonLegacy;
        }

        // Fall back to a case insensitive search so that recordings produced by
        // slightly different PresentMon builds are still usable.
        foreach (var column in header)
        {
            var name = column.Trim().Trim('"');
            if (string.Equals(name, ColumnFrameTimeV2, StringComparison.OrdinalIgnoreCase))
            {
                return PresentMonSchemaKind.PresentMonV2;
            }

            if (string.Equals(name, ColumnFrameTimeLegacy, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, ColumnFrameTimeLegacyFallback, StringComparison.OrdinalIgnoreCase))
            {
                return PresentMonSchemaKind.PresentMonLegacy;
            }
        }

        return PresentMonSchemaKind.Unknown;
    }

    private static int ResolveFrameTimeColumn(IReadOnlyDictionary<string, int> columns, PresentMonSchemaKind schema)
    {
        switch (schema)
        {
            case PresentMonSchemaKind.PresentMonV2:
                return columns[ColumnFrameTimeV2];
            case PresentMonSchemaKind.PresentMonV1:
                return columns[ColumnFrameTimeV1];
            default:
                return columns.TryGetValue(ColumnFrameTimeLegacy, out var legacy)
                    ? legacy
                    : columns[ColumnFrameTimeLegacyFallback];
        }
    }

    private static int ResolveDisplayColumn(IReadOnlyDictionary<string, int> columns, PresentMonSchemaKind schema)
    {
        if (schema == PresentMonSchemaKind.PresentMonV2)
        {
            return FindColumn(columns, ColumnDisplayedTime);
        }

        if (schema == PresentMonSchemaKind.PresentMonV1)
        {
            return FindColumn(columns, ColumnUntilDisplayedV1);
        }

        return FindColumn(columns, ColumnUntilDisplayed);
    }

    private static int FindColumn(IReadOnlyDictionary<string, int> columns, string name)
    {
        if (columns.TryGetValue(name, out var index))
        {
            return index;
        }

        foreach (var pair in columns)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return -1;
    }

    private static bool TryParseFrameTime(string raw, out double frameTime)
    {
        frameTime = 0;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();

        if (string.Equals(value, "NA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out frameTime))
        {
            return false;
        }

        if (double.IsNaN(frameTime) || double.IsInfinity(frameTime))
        {
            return false;
        }

        if (frameTime <= 0 || frameTime > MaximumPlausibleFrameTimeMs)
        {
            return false;
        }

        return true;
    }

    private static bool ParseDroppedFlag(string raw)
    {
        var value = raw.Trim();

        if (value.Length == 0 || string.Equals(value, "NA", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag) && flag != 0;
    }

    private static string DescribeFrameTimeColumn(PresentMonSchemaKind schema, string columnName)
    {
        // The column name comes from the file; this helper maps the semantics
        // of the known columns to a human readable description.
        return schema switch
        {
            PresentMonSchemaKind.PresentMonV2 =>
                $"{columnName}: time from the start of the frame until the CPU started the next frame (CPU busy + CPU wait).",
            PresentMonSchemaKind.PresentMonV1 =>
                $"{columnName}: time between two consecutive Present() calls.",
            PresentMonSchemaKind.PresentMonLegacy =>
                $"{columnName}: time between two consecutive Present() calls.",
            _ => columnName,
        };
    }
}
