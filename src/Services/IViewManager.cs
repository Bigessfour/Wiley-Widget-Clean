#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace WileyWidget.Services;

/// <summary>
/// Interface for managing application views and windows
/// </summary>
public interface IViewManager
{
    /// <summary>
    /// Shows the main application window
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    Task ShowMainWindowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows the splash screen
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    Task ShowSplashScreenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers an existing window with the view manager
    /// </summary>
    /// <param name="window">The window to register</param>
    void RegisterExistingWindow(Window window);

    /// <summary>
    /// Closes all managed windows
    /// </summary>
    void CloseAllWindows();

    /// <summary>
    /// Gets all managed windows
    /// </summary>
    IReadOnlyCollection<Window> ManagedWindows { get; }
}