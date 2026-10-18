using System.Text.RegularExpressions;
using FramePilot.Core.Models;

namespace FramePilot.Core.Power;

/// <summary>
/// Parses the output of the Windows <c>powercfg</c> tool.
/// </summary>
/// <remarks>
/// <para>
/// The tool output is localized, so only the language independent parts are
/// used: the GUID of the plan and the <c>*</c> marker that denotes the active
/// plan. The plan name is taken from the parentheses behind the GUID when it
/// is present.
/// </para>
/// <para>Example of an English output:</para>
/// <code>
/// Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *
/// Power Scheme GUID: 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c  (High performance)
/// </code>
/// </remarks>
public static partial class PowerPlanParser
{
    [GeneratedRegex(@"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    [GeneratedRegex(@"\(([^)]*)\)")]
    private static partial Regex NamePattern();

    /// <summary>Parses the output of <c>powercfg /list</c>.</summary>
    public static IReadOnlyList<PowerPlanInfo> ParseList(string? output)
    {
        var result = new List<PowerPlanInfo>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (line.Length == 0)
            {
                continue;
            }

            var match = GuidPattern().Match(line);
            if (!match.Success)
            {
                continue;
            }

            if (!Guid.TryParse(match.Value, out var guid))
            {
                continue;
            }

            var nameMatch = NamePattern().Match(line);
            var name = nameMatch.Success && nameMatch.Groups.Count > 1
                ? nameMatch.Groups[1].Value.Trim()
                : string.Empty;

            if (name.Length == 0)
            {
                name = guid.ToString();
            }

            // powercfg marks the active plan with a trailing '*'.
            var isActive = line.EndsWith("*", StringComparison.Ordinal);

            result.Add(new PowerPlanInfo(guid, name, isActive));
        }

        return result;
    }

    /// <summary>
    /// Extracts the GUID of the active plan from the output of
    /// <c>powercfg /getactivescheme</c> (or from a <c>powercfg /list</c> output).
    /// </summary>
    public static Guid? ParseActivePlanGuid(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            var match = GuidPattern().Match(line);
            if (match.Success && Guid.TryParse(match.Value, out var guid))
            {
                return guid;
            }
        }

        return null;
    }

    /// <summary>Extracts the name of the active plan, when the output contains one.</summary>
    public static string? ParseActivePlanName(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim('\r', ' ', '\t');
            if (!GuidPattern().IsMatch(line))
            {
                continue;
            }

            var nameMatch = NamePattern().Match(line);
            if (nameMatch.Success && nameMatch.Groups.Count > 1)
            {
                var name = nameMatch.Groups[1].Value.Trim();
                if (name.Length > 0)
                {
                    return name;
                }
            }
        }

        return null;
    }

    /// <summary>Parses a GUID and removes the surrounding braces.</summary>
    public static bool TryParseGuid(string? text, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim().Trim('{', '}');

        return Guid.TryParse(trimmed, out guid);
    }
}
