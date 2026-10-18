namespace FramePilot.Core.PresentMon;

/// <summary>
/// The CSV dialects that PresentMon can produce. FramePilot only understands
/// the dialects that the official PresentMon console application writes.
/// </summary>
public enum PresentMonSchemaKind
{
    /// <summary>The header did not match any known PresentMon dialect.</summary>
    Unknown = 0,

    /// <summary>
    /// PresentMon 1.x metrics (<c>--v1_metrics</c>). Frame time column:
    /// <c>msBetweenPresents</c>.
    /// </summary>
    PresentMonV1 = 1,

    /// <summary>
    /// PresentMon 2.x metrics (<c>--v2_metrics</c>). Frame time column:
    /// <c>FrameTime</c> (CPU busy + CPU wait of the frame).
    /// </summary>
    PresentMonV2 = 2,

    /// <summary>
    /// PresentMon 2.1+ default output. Frame time column:
    /// <c>MsBetweenPresents</c> (present to present) with
    /// <c>MsBetweenAppStart</c> as a fallback.
    /// </summary>
    PresentMonLegacy = 3,
}

public static class PresentMonSchemaExtensions
{
    public static string Describe(this PresentMonSchemaKind kind) => kind switch
    {
        PresentMonSchemaKind.PresentMonV1 => "PresentMon 1.x metrics (--v1_metrics)",
        PresentMonSchemaKind.PresentMonV2 => "PresentMon 2.x metrics (--v2_metrics)",
        PresentMonSchemaKind.PresentMonLegacy => "PresentMon 2.1+ default metrics",
        _ => "Unknown PresentMon layout",
    };
}
