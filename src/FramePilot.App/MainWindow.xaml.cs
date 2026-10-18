using System;
using System.Windows;
using FramePilot.App.ViewModels;

namespace FramePilot.App;

/// <summary>The main window of FramePilot.</summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
