using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Syncfusion.Windows.Tools.Controls;

#nullable enable

namespace WileyWidget.Services;

/// <summary>
/// Represents the state of a view in the view management system.
/// </summary>
public class ViewState
{
    /// <summary>
    /// Gets the window instance.
    /// </summary>
    public Window View { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the view is currently open.
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the view is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewState"/> class.
    /// </summary>
    /// <param name="view">The window instance to track.</param>
    public ViewState(Window view)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
    }
}

/// <summary>
/// Implementation of IViewManager that provides centralized view management with thread safety.
/// Handles creation, display, and lifecycle management of windows with proper Dispatcher integration.
/// </summary>
public class ViewManager : IViewManager, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ViewManager> _logger;
    private readonly Dictionary<Type, ViewState> _viewStates = new();
    private readonly SemaphoreSlim _viewSemaphore = new(1, 1);
    private DockingManager? _dockingManager;

    /// <summary>
    /// Mapping of View types to DockingManager panel names for panel management.
    /// </summary>
    private readonly Dictionary<Type, string> _viewToPanelMapping = new()
    {
        { typeof(Views.EnterprisePanelView), "EnterprisePanel" },
        { typeof(Views.BudgetPanelView), "BudgetPanel" },
        { typeof(Views.DashboardPanelView), "DashboardPanel" },
        { typeof(Views.ToolsPanelView), "ToolsPanel" },
        { typeof(Views.SettingsPanelView), "SettingsPanel" },
        { typeof(Views.AIAssistPanelView), "AIAssistPanel" },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public ViewManager(IServiceProvider serviceProvider, ILogger<ViewManager> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task ShowSplashScreenAsync(CancellationToken cancellationToken)
    {
        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            // ✅ FIX: Always create UI on STA thread using Dispatcher.InvokeAsync
            SplashScreenWindow? splashScreen = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                splashScreen = App.SplashScreenInstance ?? _serviceProvider.GetService<SplashScreenWindow>();
                if (splashScreen != null)
                {
                    App.SetSplashScreenInstance(splashScreen);
                    if (!splashScreen.IsVisible)
                    {
                        splashScreen.Show();
                    }
                    splashScreen.Activate();
                }
            });

            if (splashScreen == null)
            {
                _logger.LogWarning("No splash screen registered in DI container");
                return;
            }

            _viewStates[typeof(SplashScreenWindow)] = new ViewState(splashScreen) { IsOpen = true, IsActive = true };
            ViewChanged?.Invoke(this, new ViewChangedEventArgs(splashScreen, nameof(SplashScreenWindow)));
            _logger.LogInformation("Splash screen displayed");
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public bool IsSplashScreenShown()
    {
        return _viewStates.TryGetValue(typeof(SplashScreenWindow), out var state) && state.IsOpen;
    }

    /// <inheritdoc/>
    public void RegisterExistingWindow(System.Windows.Window window)
    {
        if (window == null) return;
        
        var windowType = window.GetType();
        _viewStates[windowType] = new ViewState(window) { IsOpen = window.IsVisible, IsActive = window.IsActive };
        _logger.LogInformation("Registered existing window {WindowType} with ViewManager", windowType.Name);
    }

    /// <inheritdoc/>
    public async Task ShowMainWindowAsync(CancellationToken cancellationToken)
    {
        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            // ✅ FIX: Create MainWindow on STA thread using Dispatcher.InvokeAsync
            MainWindow? mainWindow = null;
            
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    Application.Current!.MainWindow = mainWindow;
                    mainWindow.Show();
                    mainWindow.Activate();
                    mainWindow.Topmost = true; // Bring to front
                    mainWindow.Topmost = false; // Allow other windows to be on top
                    mainWindow.Focus();
                    // Ensure window is visible and properly positioned
                    mainWindow.WindowState = System.Windows.WindowState.Normal;
                    mainWindow.ShowInTaskbar = true;
                    mainWindow.Visibility = System.Windows.Visibility.Visible;
                    _logger.LogInformation("MainWindow created and shown - Visibility: {Visibility}, WindowState: {WindowState}, IsVisible: {IsVisible}",
                        mainWindow.Visibility, mainWindow.WindowState, mainWindow.IsVisible);
                });
            }
            else
            {
                mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                Application.Current!.MainWindow = mainWindow;
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.Topmost = true; // Bring to front
                mainWindow.Topmost = false; // Allow other windows to be on top
                mainWindow.Focus();
                // Ensure window is visible and properly positioned
                mainWindow.WindowState = System.Windows.WindowState.Normal;
                mainWindow.ShowInTaskbar = true;
                mainWindow.Visibility = System.Windows.Visibility.Visible;
                _logger.LogInformation("MainWindow created and shown - Visibility: {Visibility}, WindowState: {WindowState}, IsVisible: {IsVisible}",
                    mainWindow.Visibility, mainWindow.WindowState, mainWindow.IsVisible);
            }

            if (mainWindow != null)
            {
                _viewStates[typeof(MainWindow)] = new ViewState(mainWindow) { IsOpen = true, IsActive = true };
                ViewChanged?.Invoke(this, new ViewChangedEventArgs(mainWindow, nameof(MainWindow)));
                _logger.LogInformation("Main window displayed and activated");
            }
            else
            {
                _logger.LogError("Failed to create MainWindow - service provider returned null");
            }
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseSplashScreenAsync(CancellationToken cancellationToken)
    {
        if (!_viewStates.TryGetValue(typeof(SplashScreenWindow), out var state))
            return;

        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (state.View is SplashScreenWindow splash && state.View.IsVisible)
            {
                if (Application.Current?.Dispatcher.CheckAccess() == false)
                {
                    await Application.Current.Dispatcher.InvokeAsync(async () => await splash.FadeOutAndCloseAsync());
                }
                else
                {
                    await splash.FadeOutAndCloseAsync();
                }
            }

            _viewStates.Remove(typeof(SplashScreenWindow));
            App.SetSplashScreenInstance(null);
            ViewChanged?.Invoke(this, new ViewChangedEventArgs(null, nameof(SplashScreenWindow)));
            _logger.LogInformation("Splash screen closed");
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task ShowViewAsync<TView>(CancellationToken cancellationToken) where TView : Window
    {
        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_viewStates.TryGetValue(typeof(TView), out var state) && state.IsOpen)
            {
                _logger.LogWarning("View {ViewName} is already open", typeof(TView).Name);
                if (Application.Current?.Dispatcher.CheckAccess() == false)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => state.View.Activate());
                }
                else
                {
                    state.View.Activate();
                }
                return;
            }

            // ✅ FIX: Create view on STA thread using Dispatcher.InvokeAsync
            TView? view = null;
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    view = _serviceProvider.GetRequiredService<TView>();
                    view.Show();
                });
            }
            else
            {
                view = _serviceProvider.GetRequiredService<TView>();
                view.Show();
            }

            if (view != null)
            {
                _viewStates[typeof(TView)] = new ViewState(view) { IsOpen = true, IsActive = true };
                ViewChanged?.Invoke(this, new ViewChangedEventArgs(view, typeof(TView).Name));
                _logger.LogInformation("View {ViewName} displayed", typeof(TView).Name);
            }
            else
            {
                _logger.LogError("Failed to create view {ViewName} - service provider returned null", typeof(TView).Name);
            }
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseViewAsync<TView>(CancellationToken cancellationToken) where TView : Window
    {
        if (!_viewStates.TryGetValue(typeof(TView), out var state))
            return;

        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            await CloseViewInternalAsync(state.View, typeof(TView), cancellationToken);
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseViewAsync(Type viewType, CancellationToken cancellationToken)
    {
        if (!_viewStates.TryGetValue(viewType, out var state))
            return;

        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            await CloseViewInternalAsync(state.View, viewType, cancellationToken);
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <summary>
    /// Internal method to close a view without acquiring the semaphore (used by NavigateToAsync).
    /// </summary>
    private async Task CloseViewInternalAsync(Window view, Type viewType, CancellationToken cancellationToken)
    {
        if (view.IsVisible)
        {
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => view.Close());
            }
            else
            {
                view.Close();
            }
        }

        _viewStates.Remove(viewType);
        ViewChanged?.Invoke(this, new ViewChangedEventArgs(null, viewType.Name));
        _logger.LogInformation("View {ViewName} closed", viewType.Name);
    }

    /// <inheritdoc/>
    public async Task NavigateToAsync<TView>(CancellationToken cancellationToken, bool closePrevious = true) where TView : Window
    {
        await _viewSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (closePrevious && _viewStates.Any())
            {
                var previousViews = _viewStates.ToList(); // Get snapshot of active views
                foreach (var kvp in previousViews)
                {
                    await CloseViewInternalAsync(kvp.Value.View, kvp.Key, cancellationToken);
                }
            }

            await ShowViewAsync<TView>(cancellationToken);
        }
        finally
        {
            _viewSemaphore.Release();
        }
    }

    /// <inheritdoc/>
    public event EventHandler<ViewChangedEventArgs>? ViewChanged;

    #region DockingManager Panel Management

    /// <summary>
    /// Registers a DockingManager instance for panel management operations.
    /// This should be called from MainWindow after DockingManager is initialized.
    /// </summary>
    /// <param name="dockingManager">The DockingManager instance to manage.</param>
    public void RegisterDockingManager(DockingManager dockingManager)
    {
        ArgumentNullException.ThrowIfNull(dockingManager);
        _dockingManager = dockingManager;
        _logger.LogInformation("DockingManager registered with ViewManager for panel management");
    }

    /// <summary>
    /// Shows a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowPanelAsync<TView>(CancellationToken cancellationToken)
    {
        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot show panel: DockingManager not registered");
            return;
        }

        if (!_viewToPanelMapping.TryGetValue(typeof(TView), out var panelName))
        {
            _logger.LogWarning("No panel mapping found for view type {ViewType}", typeof(TView).Name);
            return;
        }

        await ShowPanelAsync(panelName, cancellationToken);
    }

    /// <summary>
    /// Shows a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to show.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ShowPanelAsync(string panelName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panelName);

        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot show panel '{PanelName}': DockingManager not registered", panelName);
            return;
        }

        await Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var panel = _dockingManager.Children.OfType<FrameworkElement>()
                    .FirstOrDefault(e => e.Name == panelName);

                if (panel != null)
                {
                    var state = DockingManager.GetState(panel);
                    if (state == DockState.Hidden)
                    {
                        DockingManager.SetState(panel, DockState.Dock);
                        _logger.LogInformation("Panel '{PanelName}' shown", panelName);
                    }
                    else
                    {
                        _logger.LogDebug("Panel '{PanelName}' is already visible", panelName);
                    }

                    // Activate the panel to bring it to front
                    _dockingManager.ActivateWindow(panel);
                }
                else
                {
                    _logger.LogWarning("Panel '{PanelName}' not found in DockingManager", panelName);
                }
            });
        }, cancellationToken);
    }

    /// <summary>
    /// Hides a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HidePanelAsync<TView>(CancellationToken cancellationToken)
    {
        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot hide panel: DockingManager not registered");
            return;
        }

        if (!_viewToPanelMapping.TryGetValue(typeof(TView), out var panelName))
        {
            _logger.LogWarning("No panel mapping found for view type {ViewType}", typeof(TView).Name);
            return;
        }

        await HidePanelAsync(panelName, cancellationToken);
    }

    /// <summary>
    /// Hides a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to hide.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task HidePanelAsync(string panelName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panelName);

        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot hide panel '{PanelName}': DockingManager not registered", panelName);
            return;
        }

        await Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var panel = _dockingManager.Children.OfType<FrameworkElement>()
                    .FirstOrDefault(e => e.Name == panelName);

                if (panel != null)
                {
                    DockingManager.SetState(panel, DockState.Hidden);
                    _logger.LogInformation("Panel '{PanelName}' hidden", panelName);
                }
                else
                {
                    _logger.LogWarning("Panel '{PanelName}' not found in DockingManager", panelName);
                }
            });
        }, cancellationToken);
    }

    /// <summary>
    /// Toggles the visibility of a specific panel in the DockingManager.
    /// </summary>
    /// <typeparam name="TView">The view type associated with the panel.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task TogglePanelAsync<TView>(CancellationToken cancellationToken)
    {
        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot toggle panel: DockingManager not registered");
            return;
        }

        if (!_viewToPanelMapping.TryGetValue(typeof(TView), out var panelName))
        {
            _logger.LogWarning("No panel mapping found for view type {ViewType}", typeof(TView).Name);
            return;
        }

        await TogglePanelAsync(panelName, cancellationToken);
    }

    /// <summary>
    /// Toggles the visibility of a specific panel in the DockingManager by panel name.
    /// </summary>
    /// <param name="panelName">The name of the panel to toggle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task TogglePanelAsync(string panelName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panelName);

        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot toggle panel '{PanelName}': DockingManager not registered", panelName);
            return;
        }

        await Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var panel = _dockingManager.Children.OfType<FrameworkElement>()
                    .FirstOrDefault(e => e.Name == panelName);

                if (panel != null)
                {
                    var currentState = DockingManager.GetState(panel);
                    var newState = currentState == DockState.Hidden ? DockState.Dock : DockState.Hidden;
                    DockingManager.SetState(panel, newState);
                    
                    if (newState == DockState.Dock)
                    {
                        _dockingManager.ActivateWindow(panel);
                    }

                    _logger.LogInformation("Panel '{PanelName}' toggled from {OldState} to {NewState}", 
                        panelName, currentState, newState);
                }
                else
                {
                    _logger.LogWarning("Panel '{PanelName}' not found in DockingManager", panelName);
                }
            });
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the current state of a panel in the DockingManager.
    /// </summary>
    /// <param name="panelName">The name of the panel.</param>
    /// <returns>The current DockState, or null if panel not found.</returns>
    public DockState? GetPanelState(string panelName)
    {
        ArgumentNullException.ThrowIfNull(panelName);

        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot get panel state: DockingManager not registered");
            return null;
        }

        return Application.Current.Dispatcher.Invoke(() =>
        {
            var panel = _dockingManager.Children.OfType<FrameworkElement>()
                .FirstOrDefault(e => e.Name == panelName);

            return panel != null ? DockingManager.GetState(panel) : null;
        });
    }

    /// <summary>
    /// Activates (brings to front) a specific panel in the DockingManager.
    /// </summary>
    /// <param name="panelName">The name of the panel to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ActivatePanelAsync(string panelName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(panelName);

        if (_dockingManager == null)
        {
            _logger.LogWarning("Cannot activate panel '{PanelName}': DockingManager not registered", panelName);
            return;
        }

        await Task.Run(() =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var panel = _dockingManager.Children.OfType<FrameworkElement>()
                    .FirstOrDefault(e => e.Name == panelName);

                if (panel != null)
                {
                    _dockingManager.ActivateWindow(panel);
                    _logger.LogInformation("Panel '{PanelName}' activated", panelName);
                }
                else
                {
                    _logger.LogWarning("Panel '{PanelName}' not found in DockingManager", panelName);
                }
            });
        }, cancellationToken);
    }

    #endregion

    /// <summary>
    /// Disposes of resources used by the ViewManager.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    /// <param name="disposing">Whether this is being called from Dispose() or finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewSemaphore?.Dispose();
        }
    }
}