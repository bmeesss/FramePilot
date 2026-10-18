namespace FramePilot.Core.PresentMon;

/// <summary>
/// A single frame that was presented by an application and recorded by
/// PresentMon.
/// </summary>
/// <param name="Application">Process name as written by PresentMon.</param>
/// <param name="ProcessId">Process id of the presenting process.</param>
/// <param name="FrameTimeMs">Frame time in milliseconds.</param>
/// <param name="WasDisplayed">
/// False when PresentMon reported the frame as not displayed (the display
/// columns contain "NA"). Frames that were never shown cannot be counted as
/// presented frames.
/// </param>
/// <param name="IsDropped">
/// True when the frame was explicitly marked as dropped (only the 1.x layout
/// carries an explicit <c>Dropped</c> column).
/// </param>
/// <param name="RowIndex">Zero based index of the row inside the CSV file.</param>
public sealed record PresentMonFrame(
    string Application,
    int ProcessId,
    double FrameTimeMs,
    bool WasDisplayed,
    bool IsDropped,
    int RowIndex);
