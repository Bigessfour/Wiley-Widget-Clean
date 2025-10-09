using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.Windows.Tools.Controls;
using WileyWidget.Services;

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
    private readonly IStartupProgressReporter _startupProgress;
    private MainWindow? _mainWindow;
    private readonly SemaphoreSlim _startupSemaphore = new(1, 1);
    private readonly Lazy<SplashScreenWindow?> _splashScreen;
    private readonly Lazy<MainWindow> _lazyMainWindow;

    public HostedWpfApplication(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostLifetime,
        ILogger<HostedWpfApplication> logger,
        IStartupProgressReporter startupProgress)
    {
        Console.WriteLine("[HOSTEDWPF] Constructor called");
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] Constructor called\n");
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _startupProgress = startupProgress ?? throw new ArgumentNullException(nameof(startupProgress));
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] Constructor completed\n");
        
        // Lazy initialization for splash screen to defer creation until needed
        _splashScreen = new Lazy<SplashScreenWindow?>(() => serviceProvider.GetService<SplashScreenWindow>());
        
        // Lazy initialization for MainWindow to optimize startup performance
        _lazyMainWindow = new Lazy<MainWindow>(() => serviceProvider.GetRequiredService<MainWindow>());
    }

    /// <summary>
    /// Starts the WPF application by creating and showing the main window.
    /// This method is called by the host after all other hosted services have started.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[HOSTEDWPF] StartAsync called - BEGIN");
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] StartAsync called - BEGIN\n");
        var serviceStopwatch = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("[HOSTEDWPF] About to create stopwatch");
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] About to create stopwatch\n");
        var serviceId = Guid.NewGuid().ToString("N")[..8];
        Console.WriteLine($"[HOSTEDWPF] ServiceId created: {serviceId}");
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] ServiceId created: {serviceId}\n");
        
        // Detect cold vs warm startup for performance tracking
        var startupMarkerPath = Path.Combine(Path.GetTempPath(), "wiley_widget_startup_marker.tmp");
        var isColdStartup = !File.Exists(startupMarkerPath) || 
                           (File.Exists(startupMarkerPath) && 
                            (DateTime.UtcNow - File.GetLastWriteTimeUtc(startupMarkerPath)).TotalMinutes > 30);
        
        _logger.LogInformation("Starting WPF application hosted service - ID: {ServiceId}, Startup Type: {StartupType}", 
            serviceId, isColdStartup ? "Cold" : "Warm");
        
        await _startupSemaphore.WaitAsync(cancellationToken);
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] Semaphore acquired\n");
        try
        {
            _logger.LogInformation("Starting WPF application hosted service - ID: {ServiceId}", serviceId);
            System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] About to create windowCreationStopwatch\n");

            var windowCreationStopwatch = System.Diagnostics.Stopwatch.StartNew();
            System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] windowCreationStopwatch created\n");
            
            // Track dependency injection performance
            var diStopwatch = System.Diagnostics.Stopwatch.StartNew();
            System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] diStopwatch created\n");

            _startupProgress.Report(85, "Preparing main window...");

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _logger.LogError("Application dispatcher unavailable - cannot create MainWindow [{ServiceId}]", serviceId);
                throw new InvalidOperationException("Application dispatcher unavailable during hosted startup");
            }

            if (dispatcher.CheckAccess())
            {
                System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] On UI thread, calling CreateAndShowMainWindow directly\n");
                _logger.LogInformation("Already on UI thread, creating MainWindow directly [{ServiceId}]", serviceId);
                CreateAndShowMainWindow(serviceId);
            }
            else
            {
                System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] Not on UI thread, invoking CreateAndShowMainWindow\n");
                _logger.LogInformation("Invoking CreateAndShowMainWindow on UI dispatcher [{ServiceId}]", serviceId);
                var dispatcherOperation = dispatcher.InvokeAsync(
                    () => CreateAndShowMainWindow(serviceId),
                    System.Windows.Threading.DispatcherPriority.Send,
                    cancellationToken);
                await dispatcherOperation.Task.ConfigureAwait(true);
                _logger.LogInformation("CreateAndShowMainWindow completed on UI dispatcher [{ServiceId}]", serviceId);
            }

            diStopwatch.Stop();
            _logger.LogInformation("Dependency injection and window creation completed in {WindowCreationMs}ms, " +
                "DI overhead: {DIMs}ms [{ServiceId}]", 
                windowCreationStopwatch.ElapsedMilliseconds, 
                diStopwatch.ElapsedMilliseconds, 
                serviceId);

            _startupProgress.Report(95, "Finalizing user interface...");

            // Register for application shutdown to stop the host when WPF closes
            if (Application.Current != null)
            {
                Application.Current.Exit += OnApplicationExit;
                _logger.LogDebug("Registered for Application.Exit event [{ServiceId}]", serviceId);
            }

            // Enhanced performance metrics
            LogPerformanceMetrics(serviceStopwatch.ElapsedMilliseconds, serviceId);
                
            // Mark successful startup for warm startup detection
            try
            {
                File.WriteAllText(startupMarkerPath, DateTime.UtcNow.ToString("O"));
                _logger.LogDebug("Startup marker updated for warm startup detection [{ServiceId}]", serviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update startup marker [{ServiceId}]", serviceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WPF application hosted service after {ElapsedMs}ms [{ServiceId}]", 
                serviceStopwatch.ElapsedMilliseconds, serviceId);
            throw;
        }
        finally
        {
            _startupSemaphore.Release();
            serviceStopwatch.Stop();
        }
    }

    /// <summary>
    /// Stops the WPF application by closing the main window gracefully.
    /// This method is called when the host is shutting down.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[HOSTEDWPF] StopAsync called");
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] StopAsync called\n");
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
    /// Logs comprehensive performance metrics for startup analysis.
    /// </summary>
    private void LogPerformanceMetrics(long totalStartupTimeMs, string serviceId)
    {
        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / (1024 * 1024);
            var privateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
            var gcGen0 = GC.CollectionCount(0);
            var gcGen1 = GC.CollectionCount(1);
            var gcGen2 = GC.CollectionCount(2);
            
            _logger.LogInformation("=== STARTUP PERFORMANCE METRICS [{ServiceId}] ===", serviceId);
            _logger.LogInformation("Total Startup Time: {TotalMs}ms", totalStartupTimeMs);
            _logger.LogInformation("Memory - Working Set: {WorkingSetMB}MB, Private: {PrivateMemoryMB}MB", 
                workingSetMB, privateMemoryMB);
            _logger.LogInformation("GC Collections - Gen0: {Gen0}, Gen1: {Gen1}, Gen2: {Gen2}", 
                gcGen0, gcGen1, gcGen2);
            _logger.LogInformation("Process - Threads: {ThreadCount}, Handles: {HandleCount}", 
                process.Threads.Count, process.HandleCount);
            
            // Performance assessment
            var assessment = totalStartupTimeMs switch
            {
                < 500 => "Excellent",
                < 1000 => "Good", 
                < 2000 => "Acceptable",
                < 3000 => "Slow",
                _ => "Very Slow"
            };
            
            _logger.LogInformation("Startup Performance Assessment: {Assessment} [{ServiceId}]", assessment, serviceId);
            _logger.LogInformation("=== END PERFORMANCE METRICS [{ServiceId}] ===", serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log performance metrics [{ServiceId}]", serviceId);
        }
    }

    /// <summary>
    /// Preloads critical resources and pre-JITs important code paths for better startup performance.
    /// </summary>
    private void PreloadCriticalResources(string serviceId = "unknown")
    {
        var preloadStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Preloading critical resources [{ServiceId}]", serviceId);
            
            // Pre-JIT critical WPF types by creating dummy instances
            // This reduces JIT compilation overhead during actual window creation
            Window? dummyWindow = null;
            try
            {
                dummyWindow = new Window
                {
                    Width = 100,  // Use reasonable minimum size instead of 1x1
                    Height = 100,
                    ShowInTaskbar = false,
                    Visibility = Visibility.Hidden,
                    ResizeMode = ResizeMode.NoResize  // Prevent sizing issues
                };

                // Only trigger layout if WPF is fully initialized (avoid crash during early startup)
                if (Application.Current != null && Application.Current.MainWindow == null)
                {
                    try
                    {
                        // Safe layout preloading - only if WPF is ready
                        dummyWindow.Measure(new Size(100, 100));
                        dummyWindow.Arrange(new Rect(0, 0, 100, 100));
                        _logger.LogDebug("Dummy window layout preloading completed successfully [{ServiceId}]", serviceId);
                    }
                    catch (Exception)
                    {
                        _logger.LogDebug("WPF layout system not ready for preloading, skipping Measure/Arrange [{ServiceId}]", serviceId);
                    }
                }
                else
                {
                    _logger.LogDebug("Dummy window created for JIT preloading (layout skipped) [{ServiceId}]", serviceId);
                }

                _logger.LogDebug("Dummy window preloading completed successfully [{ServiceId}]", serviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preload WPF layout system, continuing without preloading [{ServiceId}]", serviceId);
                // Continue execution even if preloading fails
            }
            finally
            {
                // Clean up dummy window safely
                if (dummyWindow != null)
                {
                    try
                    {
                        dummyWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to close dummy window during cleanup [{ServiceId}]", serviceId);
                    }
                    dummyWindow = null;
                }
            }
            
            // Preload Syncfusion components that will be used immediately
            try
            {
                // Force loading of Syncfusion assemblies
                var sfAssembly = typeof(Syncfusion.UI.Xaml.Grid.SfDataGrid);
                var ribbonAssembly = typeof(Syncfusion.Windows.Tools.Controls.Ribbon);
                _logger.LogDebug("Syncfusion assemblies preloaded [{ServiceId}]", serviceId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to preload Syncfusion assemblies [{ServiceId}]", serviceId);
            }
            
            // Force a generation 0 GC to clean up temporary objects
            GC.Collect(0, GCCollectionMode.Optimized);
            
            _logger.LogDebug("Critical resources preloaded in {ElapsedMs}ms [{ServiceId}]", 
                preloadStopwatch.ElapsedMilliseconds, serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preload critical resources after {ElapsedMs}ms [{ServiceId}]", 
                preloadStopwatch.ElapsedMilliseconds, serviceId);
            // Continue execution even if preloading fails
        }
        finally
        {
            preloadStopwatch.Stop();
        }
    }

    /// <summary>
    /// Creates and shows the main window using dependency injection.
    /// </summary>
    private void CreateAndShowMainWindow(string serviceId = "unknown")
    {
        System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] CreateAndShowMainWindow called with serviceId: {serviceId}\n");
        var creationStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("CreateAndShowMainWindow called - starting MainWindow creation [{ServiceId}]", serviceId);
            System.IO.File.AppendAllText("debug-hosted.log", $"[{DateTime.Now:HH:mm:ss.fff}] [HOSTEDWPF] Logger called in CreateAndShowMainWindow\n");
            
            // Preload critical resources for better performance
            PreloadCriticalResources(serviceId);
            
            _logger.LogDebug("Creating MainWindow via lazy initialization [{ServiceId}]", serviceId);
            
            // Create the main window through lazy initialization and dependency injection
            _mainWindow = _lazyMainWindow.Value;
            _logger.LogDebug("MainWindow instance created in {ElapsedMs}ms [{ServiceId}]", 
                creationStopwatch.ElapsedMilliseconds, serviceId);
            
            // Attach splash screen closing logic before showing the window
            var splashScreen = _splashScreen.Value;
            if (splashScreen != null)
            {
                _logger.LogDebug("Attaching splash screen close handler to MainWindow.ContentRendered [{ServiceId}]", serviceId);
                
                _mainWindow.ContentRendered += async (_, __) =>
            {
                var splashCloseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    _logger.LogDebug("MainWindow content rendered, optimizing DataContext and closing splash screen [{ServiceId}]", serviceId);
                    
                    // Ensure DataContext is set after content is rendered for optimal performance
                    if (_mainWindow.DataContext == null)
                    {
                        _logger.LogDebug("DataContext not set, triggering deferred initialization [{ServiceId}]", serviceId);
                        // The MainWindow's OnWindowLoaded should handle DataContext setting
                        // This is just a safety check and optimization trigger
                    }
                    
                    // Handle splash screen closing after UI is fully rendered
                    if (splashScreen != null)
                    {
                        // Reduced delay for faster transition to MainWindow
                        await Task.Delay(50);
                        await splashScreen.FadeOutAndCloseAsync();
                        _logger.LogInformation("Splash screen closed successfully in {ElapsedMs}ms after MainWindow content rendered [{ServiceId}]", 
                            splashCloseStopwatch.ElapsedMilliseconds, serviceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to optimize post-render operations after {ElapsedMs}ms [{ServiceId}]", 
                        splashCloseStopwatch.ElapsedMilliseconds, serviceId);
                    // Fallback: just hide splash screen if it exists
                    if (splashScreen != null)
                    {
                        try
                        {
                            await splashScreen.HideAsync();
                            _logger.LogDebug("Splash screen hidden via fallback method [{ServiceId}]", serviceId);
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogWarning(fallbackEx, "Fallback splash screen hide also failed [{ServiceId}]", serviceId);
                            // Last resort: do nothing, splash will remain
                        }
                    }
                }
            };
            }
            else
            {
                _logger.LogDebug("No splash screen available to close [{ServiceId}]", serviceId);
            }
            
            // Set as the application's main window
            if (Application.Current != null)
            {
                Application.Current.MainWindow = _mainWindow;
                _logger.LogDebug("MainWindow set as Application.MainWindow [{ServiceId}]", serviceId);
                _logger.LogInformation("MainWindow assigned to Application.Current - IsVisible={IsVisible}, WindowState={WindowState} [{ServiceId}]",
                    _mainWindow.IsVisible,
                    _mainWindow.WindowState,
                    serviceId);
            }

            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Show the window
            _mainWindow.Show();
            _logger.LogDebug("MainWindow.Show() completed in {ElapsedMs}ms [{ServiceId}]", 
                showStopwatch.ElapsedMilliseconds, serviceId);

            _logger.LogInformation("Main window displayed - Visibility={Visibility}, Topmost={Topmost}, Bounds={Left},{Top},{Width}x{Height} [{ServiceId}]",
                _mainWindow.Visibility,
                _mainWindow.Topmost,
                _mainWindow.Left,
                _mainWindow.Top,
                _mainWindow.Width,
                _mainWindow.Height,
                serviceId);

            _logger.LogInformation("Main window created and displayed successfully in {TotalElapsedMs}ms [{ServiceId}]",
                creationStopwatch.ElapsedMilliseconds, serviceId);

            _startupProgress.Report(98, "Main window ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create and show main window after {ElapsedMs}ms [{ServiceId}]", 
                creationStopwatch.ElapsedMilliseconds, serviceId);
            throw;
        }
        finally
        {
            creationStopwatch.Stop();
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
            
            // Unregister WPF event handlers on the correct thread
            if (Application.Current != null && Application.Current.Dispatcher != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Exit -= OnApplicationExit;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => 
                        Application.Current.Exit -= OnApplicationExit);
                }
            }
        }
    }
}