using System;
using FramePilot.Core.Infrastructure;

namespace FramePilot.App.ViewModels;

/// <summary>Base class of all FramePilot view models.</summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Title of the page.</summary>
    public abstract string Title { get; }

    /// <summary>Short description that is shown below the title.</summary>
    public abstract string Description { get; }
}

/// <summary>An entry of the navigation bar.</summary>
public sealed class NavigationItem
{
    public NavigationItem(string title, string description, ViewModelBase viewModel, object view)
    {
        Title = title;
        Description = description;
        ViewModel = viewModel;
        View = view;
    }

    public string Title { get; }

    public string Description { get; }

    public ViewModelBase ViewModel { get; }

    public object View { get; }
}
