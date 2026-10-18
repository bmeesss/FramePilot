using System.Text;

namespace FramePilot.Core.Infrastructure;

/// <summary>
/// Splits a command line string into individual arguments.
/// </summary>
/// <remarks>
/// The splitter follows the rules of the Windows command line parser:
/// whitespace separates arguments, double quotes group them and a backslash
/// before a quote escapes it. The result is used with
/// <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>, so the
/// values are never re-interpreted by a shell.
/// </remarks>
public static class CommandLineSplitter
{
    public static IReadOnlyList<string> Split(string? commandLine)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return result;
        }

        var current = new StringBuilder();
        var inQuotes = false;
        var hasValue = false;

        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];

            if (c == '\\')
            {
                var backslashes = 0;
                while (i < commandLine.Length && commandLine[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i < commandLine.Length && commandLine[i] == '"')
                {
                    current.Append('\\', backslashes / 2);
                    if (backslashes % 2 == 0)
                    {
                        // The quote delimits a value.
                        inQuotes = !inQuotes;
                    }
                    else
                    {
                        // The quote is escaped and part of the value.
                        current.Append('"');
                    }
                }
                else
                {
                    current.Append('\\', backslashes);
                    i--;
                }

                hasValue = true;
                continue;
            }

            if (c == '"')
            {
                if (inQuotes && i + 1 < commandLine.Length && commandLine[i + 1] == '"')
                {
                    // A doubled quote inside a quoted value is a literal quote.
                    current.Append('"');
                    hasValue = true;
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                hasValue = true;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (hasValue)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    hasValue = false;
                }

                continue;
            }

            current.Append(c);
            hasValue = true;
        }

        if (hasValue)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
