namespace FramePilot.Core.Abstractions;

/// <summary>
/// Confirmation and message dialogs. Kept behind an interface so that view
/// models stay testable and never touch WPF directly.
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a modal question and returns the user's answer.</summary>
    Task<bool> ConfirmAsync(string title, string message, string acceptText = "Yes", string cancelText = "Cancel");

    /// <summary>Shows an informational message.</summary>
    Task ShowInfoAsync(string title, string message);

    /// <summary>Shows a warning message.</summary>
    Task ShowWarningAsync(string title, string message);

    /// <summary>Shows an error message.</summary>
    Task ShowErrorAsync(string title, string message);
}
