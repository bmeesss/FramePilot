using System;
using System.Windows.Controls;
using FramePilot.App.ViewModels;

namespace FramePilot.App.Views;

/// <summary>The game session manager.</summary>
public partial class GameSessionView : UserControl
{
    public GameSessionView(GameSessionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        DataContext = viewModel;
    }
}
