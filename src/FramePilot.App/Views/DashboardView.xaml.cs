using System;
using System.Windows.Controls;
using FramePilot.App.ViewModels;

namespace FramePilot.App.Views;

/// <summary>The live dashboard of the system metrics.</summary>
public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
