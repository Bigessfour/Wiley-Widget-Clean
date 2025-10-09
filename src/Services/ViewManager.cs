#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace WileyWidget.Services;

/// <summary>
/// Implementation of IViewManager for managing application views and windows
/// </summary>
public class ViewManager : IViewManager
{
    private readonly ILogger<ViewManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<Window> _managedWindows = new();

    /// <summary>
    /// Gets all managed windows
    /// </summary>
    public IReadOnlyCollection<Window> ManagedWindows => _managedWindows.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the ViewManager class
    /// </summary>
    /// <param name="logger">The logger instance</param>
    /// <param name="serviceProvider">The service provider</param>
    public ViewManager(ILogger<ViewManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Shows the main application window
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    public async Task ShowMainWindowAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("[VIEWMANAGER] ShowMainWindowAsync called");
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Console.WriteLine("[VIEWMANAGER] Inside dispatcher invoke");
                var mainWindow = _serviceProvider.GetService(typeof(MainWindow)) as MainWindow;
                if (mainWindow != null)
                {
                    Console.WriteLine("[VIEWMANAGER] MainWindow resolved, calling Show()");
                    mainWindow.Show();
                    _managedWindows.Add(mainWindow);
                    _logger.LogInformation("Main window shown successfully");
                }
                else
                {
                    Console.WriteLine("[VIEWMANAGER] MainWindow not resolved");
                    _logger.LogError("Failed to resolve MainWindow from service provider");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VIEWMANAGER] Exception: {ex.Message}");
            _logger.LogError(ex, "Error showing main window");
            throw;
        }
    }

    /// <summary>
    /// Shows the splash screen
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the async operation</returns>
    public async Task ShowSplashScreenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var splashScreen = _serviceProvider.GetService(typeof(SplashScreenWindow)) as SplashScreenWindow;
                if (splashScreen != null)
                {
                    splashScreen.Show();
                    _managedWindows.Add(splashScreen);
                    _logger.LogInformation("Splash screen shown successfully");
                }
                else
                {
                    _logger.LogError("Failed to resolve SplashScreenWindow from service provider");
                }
            }, System.Windows.Threading.DispatcherPriority.Normal, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing splash screen");
            throw;
        }
    }

    /// <summary>
    /// Registers an existing window with the view manager
    /// </summary>
    /// <param name="window">The window to register</param>
    public void RegisterExistingWindow(Window window)
    {
        if (window == null) throw new ArgumentNullException(nameof(window));

        if (!_managedWindows.Contains(window))
        {
            _managedWindows.Add(window);
            _logger.LogInformation("Window registered: {WindowType}", window.GetType().Name);
        }
    }

    /// <summary>
    /// Closes all managed windows
    /// </summary>
    public void CloseAllWindows()
    {
        foreach (var window in _managedWindows.ToList())
        {
            try
            {
                window.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing window: {WindowType}", window.GetType().Name);
            }
        }

        _managedWindows.Clear();
        _logger.LogInformation("All managed windows closed");
    }
}