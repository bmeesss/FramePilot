using System;
using Microsoft.Win32;

namespace FramePilot.App.Services;

/// <summary>File pickers for the user interface.</summary>
public interface IFileDialogService
{
    string? OpenFile(string title, string filter);

    string? SaveFile(string title, string filter, string suggestedFileName);
}

/// <summary>File pickers that use the common Windows dialogs.</summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    public string? OpenFile(string title, string filter)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string title, string filter, string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = suggestedFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
