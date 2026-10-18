namespace FramePilot.Core.PresentMon;

/// <summary>Reason a PresentMon CSV file could not be analysed.</summary>
public enum PresentMonParseError
{
    /// <summary>The file was empty or contained only whitespace.</summary>
    EmptyFile,

    /// <summary>The first line did not look like a PresentMon CSV header.</summary>
    MissingHeader,

    /// <summary>The header did not match any supported PresentMon dialect.</summary>
    UnsupportedSchema,

    /// <summary>The file was a valid PresentMon CSV but contained no usable frame rows.</summary>
    NoFrameData,
}

/// <summary>
/// Thrown when a PresentMon CSV file cannot be analysed. The message always
/// explains what was wrong so the user can act on it.
/// </summary>
public sealed class PresentMonParseException : Exception
{
    public PresentMonParseException(PresentMonParseError error, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
    }

    public PresentMonParseError Error { get; }
}
