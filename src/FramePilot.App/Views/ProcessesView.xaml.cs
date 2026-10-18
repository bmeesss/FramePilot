using System;
using System.Windows.Controls;
using FramePilot.App.ViewModels;

namespace FramePilot.App.Views;

/// <summary>The list of running applications.</summary>
public partial class ProcessesView : UserControl
{
    public ProcessesView(ProcessesViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
