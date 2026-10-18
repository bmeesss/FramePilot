using System;
using System.Threading.Tasks;
using System.Windows;
using FramePilot.Core.Abstractions;

namespace FramePilot.App.Services;

/// <summary>Dialogs that use the native Windows message box.</summary>
public sealed class WpfDialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message, string acceptText = "Yes", string cancelText = "Cancel")
    {
        var result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question,
            MessageBoxResult.Cancel);

        return Task.FromResult(result == MessageBoxResult.OK);
    }

    public Task ShowInfoAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        return Task.CompletedTask;
    }
}
