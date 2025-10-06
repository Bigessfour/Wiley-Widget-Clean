using System;
using System.Windows;

#nullable enable

namespace WileyWidget.Services;

/// <summary>
/// Event arguments for view change notifications.
/// Contains information about the current view and its name.
/// </summary>
public class ViewChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current view that was shown or closed.
    /// Null when a view is being closed.
    /// </summary>
    public Window? CurrentView { get; }

    /// <summary>
    /// Gets the name of the view that changed.
    /// </summary>
    public string ViewName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewChangedEventArgs"/> class.
    /// </summary>
    /// <param name="currentView">The current view, or null if closing.</param>
    /// <param name="viewName">The name of the view.</param>
    public ViewChangedEventArgs(Window? currentView, string viewName)
    {
        CurrentView = currentView;
        ViewName = viewName ?? throw new ArgumentNullException(nameof(viewName));
    }
}