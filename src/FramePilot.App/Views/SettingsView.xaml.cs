using System;
using System.Windows.Controls;
using FramePilot.App.ViewModels;

namespace FramePilot.App.Views;

/// <summary>The settings page.</summary>
public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
