using System;
using System.Windows.Controls;
using FramePilot.App.ViewModels;

namespace FramePilot.App.Views;

/// <summary>The benchmark analysis and report page.</summary>
public partial class BenchmarkView : UserControl
{
    public BenchmarkView(BenchmarkViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
