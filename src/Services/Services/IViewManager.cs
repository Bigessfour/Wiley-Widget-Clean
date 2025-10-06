using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Syncfusion.Windows.Tools.Controls;

#nullable enable

namespace WileyWidget.Services;

/// <summary>
/// Interface for managing view creation, display, and lifecycle operations.
/// Provides centralized control over window management with thread safety and event notifications.
/// </summary>
public interface IViewManager
{
    /// <summary>
    /// Shows the main window asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowMainWindowAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Shows the splash screen asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowSplashScreenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks if the splash screen is currently shown.
    /// </summary>
    /// <returns>True if the splash screen is currently displayed, false otherwise.</returns>
    bool IsSplashScreenShown();

    /// <summary>
    /// Registers an existing window with the view manager for state tracking.
    /// This is useful for windows created before the ViewManager was available.
    /// </summary>
    /// <param name="window">The window to register.</param>
    void RegisterExistingWindow(System.Windows.Window window);

    /// <summary>
    /// Closes the splash screen asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseSplashScreenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Shows a view of the specified type asynchronously.
    /// </summary>
    /// <typeparam name="TView">The type of window to show.</typeparam>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowViewAsync<TView>(CancellationToken cancellationToken) where TView : Window;

    /// <summary>
    /// Closes a view of the specified type asynchronously.
    /// </summary>
    /// <typeparam name="TView">The type of window to close.</typeparam>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseViewAsync<TView>(CancellationToken cancellationToken) where TView : Window;

    /// <summary>
    /// Closes a view of the specified type asynchronously.
    /// </summary>
    /// <param name="viewType">The type of window to close.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseViewAsync(Type viewType, CancellationToken cancellationToken);

    /// <summary>
    /// Navigates to a view of the specified type asynchronously, optionally closing previous views.
    /// </summary>
    /// <typeparam name="TView">The type of window to navigate to.</typeparam>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <param name="closePrevious">Whether to close all previous views before showing the new one.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NavigateToAsync<TView>(CancellationToken cancellationToken, bool closePrevious = true) where TView : Window;

    /// <summary>
    /// Event raised when a view is shown or closed.
    /// </summary>
    event EventHandler<ViewChangedEventArgs> ViewChanged;

    #region DockingManager Panel Management

    /// <summary>
    /// Registers a DockingManager instance for panel management operations.
    /// This should be called from MainWindow after DockingManager is initialized.
    /// </summary>
    /// <param name="dockingManager">The DockingManager instance to manage.</param>
    void RegisterDockingManager(DockingManager dockingManager);

    /// <summary>
    /// Shows a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowPanelAsync<TView>(CancellationToken cancellationToken);

    /// <summary>
    /// Shows a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to show.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ShowPanelAsync(string panelName, CancellationToken cancellationToken);

    /// <summary>
    /// Hides a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HidePanelAsync<TView>(CancellationToken cancellationToken);

    /// <summary>
    /// Hides a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to hide.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HidePanelAsync(string panelName, CancellationToken cancellationToken);

    /// <summary>
    /// Toggles the visibility of a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TogglePanelAsync<TView>(CancellationToken cancellationToken);

    /// <summary>
    /// Toggles the visibility of a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to toggle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TogglePanelAsync(string panelName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current state of a panel in the DockingManager.
    /// </summary>
    /// <param name="panelName">The name of the panel.</param>
    /// <returns>The current DockState, or null if panel not found.</returns>
    DockState? GetPanelState(string panelName);

    /// <summary>
    /// Activates (brings to front) a specific panel in the DockingManager.
    /// </summary>
    /// <param name="panelName">The name of the panel to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ActivatePanelAsync(string panelName, CancellationToken cancellationToken);

    #endregion
}