using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

#nullable enable

namespace WileyWidget.Services.Hosting;

/// <summary>
/// Hosted service that manages the WPF application lifecycle within the Generic Host pattern.
/// This service handles the creation and management of the main window, ensuring proper
/// integration between the host lifetime and WPF application lifecycle.
/// </summary>
public class HostedWpfApplication : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<HostedWpfApplication> _logger;
    private MainWindow? _mainWindow;
    private readonly SemaphoreSlim _startupSemaphore = new(1, 1);

    public HostedWpfApplication(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostLifetime,
        ILogger<HostedWpfApplication> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the WPF application by creating and showing the main window.
    /// This method is called by the host after all other hosted services have started.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startupSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Starting WPF application hosted service");

            // Ensure we're on the UI thread for WPF operations
            if (Application.Current?.Dispatcher.CheckAccess() == false)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => CreateAndShowMainWindow());
            }
            else
            {
                CreateAndShowMainWindow();
            }

            // Register for application shutdown to stop the host when WPF closes
            if (Application.Current != null)
            {
                Application.Current.Exit += OnApplicationExit;
            }

            _logger.LogInformation("WPF application hosted service started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WPF application hosted service");
            throw;
        }
        finally
        {
            _startupSemaphore.Release();
        }
    }

    /// <summary>
    /// Stops the WPF application by closing the main window gracefully.
    /// This method is called when the host is shutting down.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping WPF application hosted service");

        try
        {
            if (_mainWindow != null && Application.Current?.Dispatcher.CheckAccess() == false)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _mainWindow?.Close();
                    _mainWindow = null;
                });
            }
            else
            {
                _mainWindow?.Close();
                _mainWindow = null;
            }

            _logger.LogInformation("WPF application hosted service stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping WPF application hosted service");
            // Don't rethrow during shutdown
        }
    }

    /// <summary>
    /// Creates and shows the main window using dependency injection.
    /// </summary>
    private void CreateAndShowMainWindow()
    {
        try
        {
            // Create the main window through dependency injection
            _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            
            // Set as the application's main window
            if (Application.Current != null)
            {
                Application.Current.MainWindow = _mainWindow;
            }

            // Show the window
            _mainWindow.Show();

            _logger.LogInformation("Main window created and displayed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create and show main window");
            throw;
        }
    }

    /// <summary>
    /// Handles the WPF application exit event by stopping the host.
    /// </summary>
    private void OnApplicationExit(object? sender, ExitEventArgs e)
    {
        _logger.LogInformation("WPF application exit requested, stopping host");
        
        // Unregister the event handler to prevent multiple calls
        if (Application.Current != null)
        {
            Application.Current.Exit -= OnApplicationExit;
        }

        // Stop the host when WPF application exits
        _hostLifetime.StopApplication();
    }

    /// <summary>
    /// Disposes of resources used by the hosted service.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of Dispose pattern.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _startupSemaphore?.Dispose();
            
            if (Application.Current != null)
            {
                Application.Current.Exit -= OnApplicationExit;
            }
        }
    }
}