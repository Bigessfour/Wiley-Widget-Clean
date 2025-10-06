using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;
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
public class HostedWpfApplication(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostLifetime,
    ILogger<HostedWpfApplication> logger,
    IViewManager viewManager,
    BackgroundInitializationService backgroundInitializationService) : IHostedService, IDisposable
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IHostApplicationLifetime _hostLifetime = hostLifetime ?? throw new ArgumentNullException(nameof(hostLifetime));
    private readonly ILogger<HostedWpfApplication> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IViewManager _viewManager = viewManager ?? throw new ArgumentNullException(nameof(viewManager));
    private MainWindow? _mainWindow;
    private readonly SemaphoreSlim _startupSemaphore = new(1, 1);
    private readonly TaskCompletionSource<bool> _applicationExitTcs = new();
    // ✅ FIX: Remove Lazy initialization that creates UI on wrong thread
    // UI creation will be done explicitly on STA thread using Dispatcher.Invoke
#pragma warning disable CA2213 // Disposable fields should be disposed - managed by DI container
    private readonly BackgroundInitializationService _backgroundInitializationService = backgroundInitializationService ?? throw new ArgumentNullException(nameof(backgroundInitializationService));

#pragma warning restore CA2213

    /// <summary>
    /// Starts the WPF application by creating and showing the main window.
    /// This method is called by the host after all other hosted services have started.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var serviceStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var serviceId = Guid.NewGuid().ToString("N")[..8];

        // Detect cold vs warm startup for performance tracking
        var startupMarkerPath = Path.Combine(Path.GetTempPath(), "wiley_widget_startup_marker.tmp");
        var isColdStartup = !File.Exists(startupMarkerPath) ||
                           (File.Exists(startupMarkerPath) &&
                            (DateTime.UtcNow - File.GetLastWriteTimeUtc(startupMarkerPath)).TotalMinutes > 30);

        _logger.LogInformation("=== STARTING WPF APPLICATION HOSTED SERVICE ===");
        _logger.LogInformation("Service ID: {ServiceId}, Startup Type: {StartupType}, Thread: {ThreadId}",
            serviceId, isColdStartup ? "Cold" : "Warm", Environment.CurrentManagedThreadId);
        _logger.LogInformation("Application.Current: {AppCurrent}, Dispatcher: {Dispatcher}",
            Application.Current != null, Application.Current?.Dispatcher != null);

        await _startupSemaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Acquired startup semaphore, beginning initialization - ID: {ServiceId}", serviceId);

            // Wait for background initialization to complete (with timeout)
            var backgroundInitTask = _backgroundInitializationService.InitializationCompleted;
            if (!backgroundInitTask.IsCompleted)
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                var completedTask = await Task.WhenAny(backgroundInitTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _logger.LogWarning("Background initialization timed out after 30 seconds - continuing startup ({ServiceId})", serviceId);
                }
                else
                {
                    await ObserveBackgroundInitializationCompletionAsync(backgroundInitTask, serviceId, cancellationToken);
                }
            }
            else
            {
                await ObserveBackgroundInitializationCompletionAsync(backgroundInitTask, serviceId, cancellationToken);
            }

            var windowCreationStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Track dependency injection performance
            var diStopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Beginning view management phase - ID: {ServiceId}", serviceId);

            // Splash screen is already shown in OnStartup - proceed directly to MainWindow creation

            // Preload resources (move this to a separate method or service if needed)
            PreloadCriticalResources(serviceId);

            // ✅ MICROSOFT FIX: Use Dispatcher.Invoke for cross-thread UI access
            // Reference: https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcher.invoke
            // Application.Current is guaranteed to be available in a running WPF application

            MainWindow? mainWindow = null;
            
            // Safety check: Application.Current should never be null in a running WPF application
            if (Application.Current == null)
            {
                throw new InvalidOperationException("WPF Application.Current is null - WPF application may not be properly initialized");
            }

            // Check if MainWindow was already created by WPF via StartupUri
            // This check must be done on the UI thread
            MainWindow? existingMainWindow = null;
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                existingMainWindow = Application.Current.MainWindow as MainWindow;
            });

            if (existingMainWindow != null)
            {
                _logger.LogInformation("MainWindow already exists (created via StartupUri) - ID: {ServiceId}", serviceId);
                mainWindow = existingMainWindow;
            }
            else
            {
                // MainWindow doesn't exist yet, create it on the UI thread
                _logger.LogInformation("Creating MainWindow on UI thread - ID: {ServiceId}", serviceId);
                mainWindow = await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var windowTask = _viewManager.ShowMainWindowAsync(cancellationToken);
                    // Don't await here - just start the task and let it run
                    _ = windowTask.ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully)
                        {
                            _logger.LogInformation("MainWindow creation task completed successfully - ID: {ServiceId}", serviceId);
                        }
                        else if (t.IsFaulted)
                        {
                            _logger.LogError(t.Exception, "MainWindow creation task failed - ID: {ServiceId}", serviceId);
                        }
                    }, TaskScheduler.Default);

                    return Application.Current.MainWindow as MainWindow;
                });
                _logger.LogInformation("MainWindow creation initiated on UI thread - ID: {ServiceId}", serviceId);
            }
            _mainWindow = mainWindow;
                
                // VERBOSE POST-STARTUP DEBUG LOGGING
                if (mainWindow != null)
                {
                    // ✅ FIX: Access MainWindow properties on UI thread
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _logger.LogInformation("✓ MainWindow created successfully - Type: {WindowType}, Title: {Title} - ID: {ServiceId}",
                            mainWindow.GetType().Name, mainWindow.Title, serviceId);
                        _logger.LogDebug("MainWindow initial state - Visibility: {Visibility}, WindowState: {WindowState}, Size: {Width}x{Height} - ID: {ServiceId}",
                            mainWindow.Visibility, mainWindow.WindowState, mainWindow.Width, mainWindow.Height, serviceId);
                    });
                }
                else
                {
                    _logger.LogWarning("⚠ MainWindow creation returned null - UI may not be properly initialized - ID: {ServiceId}", serviceId);
                }
            
            // ✅ CRITICAL FIX: Handle splash screen closing asynchronously WITHOUT blocking StartAsync
            // This must be done on a background thread to not block the hosted service startup
            _ = Task.Run(async () =>
            {
                try
                {
                    if (mainWindow != null && Application.Current?.Dispatcher != null)
                    {
                        // Wait for MainWindow to be fully rendered
                        var contentRenderedTcs = new TaskCompletionSource<bool>();
                        var loadedTcs = new TaskCompletionSource<bool>();

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // Use ContentRendered for better timing - fires after UI is fully painted
                            mainWindow.ContentRendered += (_, _) =>
                            {
                                _logger.LogInformation("� MainWindow ContentRendered - UI fully painted and ready - ID: {ServiceId}", serviceId);
                                contentRenderedTcs.TrySetResult(true);
                            };

                            // Fallback: also listen to Loaded as backup
                            mainWindow.Loaded += (_, _) =>
                            {
                                _logger.LogInformation("📦 MainWindow Loaded event fired - ID: {ServiceId}", serviceId);
                                loadedTcs.TrySetResult(true);
                            };
                        });

                        // Wait for either ContentRendered or Loaded (with timeout)
                        var renderTimeout = Task.Delay(TimeSpan.FromSeconds(10));
                        var renderTask = await Task.WhenAny(contentRenderedTcs.Task, loadedTcs.Task, renderTimeout);

                        if (renderTask == renderTimeout)
                        {
                            _logger.LogWarning("MainWindow render events timed out, proceeding with splash screen close - ID: {ServiceId}", serviceId);
                        }

                        // Close splash screen
                        await _viewManager.CloseSplashScreenAsync(CancellationToken.None);
                        _logger.LogInformation("✨ Splash screen closed after MainWindow render - ID: {ServiceId}", serviceId);
                    }
                    else
                    {
                        // Fallback: close splash screen after delay if MainWindow is null
                        await Task.Delay(2000);
                        await _viewManager.CloseSplashScreenAsync(CancellationToken.None);
                        _logger.LogWarning("Closed splash screen with fallback delay (MainWindow was null) - ID: {ServiceId}", serviceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in splash screen handling - ID: {ServiceId}", serviceId);
                }
            }, CancellationToken.None);

            windowCreationStopwatch.Stop();
            _logger.LogInformation("View management and window creation completed in {WindowCreationMs}ms - ID: {ServiceId}",
                windowCreationStopwatch.ElapsedMilliseconds, serviceId);

            // ✅ STA THREAD FIX: Register for application shutdown to stop the host when WPF closes
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (Application.Current != null)
                    {
                        Application.Current.Exit += OnApplicationExit;
                        _logger.LogDebug("Registered for Application.Exit event - ID: {ServiceId}", serviceId);
                    }
                    else
                    {
                        _logger.LogWarning("Application.Current is null inside dispatcher, cannot register for Exit event - ID: {ServiceId}", serviceId);
                    }
                });
            }
            else
            {
                _logger.LogWarning("Application.Current or Dispatcher is null, cannot register for Exit event - ID: {ServiceId}", serviceId);
            }

            _logger.LogInformation("=== WPF APPLICATION HOSTED SERVICE STARTED SUCCESSFULLY ===");
            _logger.LogInformation("Total startup time: {TotalElapsedMs}ms - ID: {ServiceId}",
                serviceStopwatch.ElapsedMilliseconds, serviceId);
                
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
            
            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            var errorReportingService = _serviceProvider.GetService<ErrorReportingService>();
            errorReportingService?.ReportError(
                ex, 
                "HostedService_Start", 
                showToUser: true, 
                level: LogEventLevel.Fatal, 
                correlationId: correlationId);
            
            throw;
        }
        finally
        {
            _startupSemaphore.Release();
            serviceStopwatch.Stop();
        }

        // Wait for the WPF application to exit before completing the hosted service
        _logger.LogInformation("Waiting for WPF application to exit - ID: {ServiceId}", serviceId);
        try
        {
            await _applicationExitTcs.Task.WaitAsync(cancellationToken);
            _logger.LogInformation("WPF application exited, hosted service completing - ID: {ServiceId}", serviceId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Hosted service cancellation requested, completing - ID: {ServiceId}", serviceId);
            _applicationExitTcs.TrySetResult(true);
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

        // Complete the application exit task in case it wasn't completed by OnApplicationExit
        _applicationExitTcs.TrySetResult(true);
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

    private async Task ObserveBackgroundInitializationCompletionAsync(Task backgroundInitTask, string serviceId, CancellationToken cancellationToken)
    {
        try
        {
            await backgroundInitTask.WaitAsync(cancellationToken);
            _logger.LogInformation("Background initialization completed successfully - ID: {ServiceId}", serviceId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Background initialization canceled - ID: {ServiceId}", serviceId);
        }
        catch (Exception ex)
        {
            HandleBackgroundInitializationFailure(ex, serviceId);
        }
    }

    private void MonitorBackgroundInitialization(Task backgroundInitTask, string serviceId, CancellationToken hostCancellationToken)
    {
        backgroundInitTask.ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                if (hostCancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Background initialization canceled due to host shutdown - ID: {ServiceId}", serviceId);
                }
                else
                {
                    _logger.LogWarning("Background initialization canceled unexpectedly - ID: {ServiceId}", serviceId);
                }
                return;
            }

            if (task.IsFaulted)
            {
                var exception = task.Exception?.GetBaseException() ?? new Exception("Background initialization failed with unknown error");
                HandleBackgroundInitializationFailure(exception, serviceId);
                return;
            }

            _logger.LogInformation("Background initialization completed after UI startup - ID: {ServiceId}", serviceId);
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private void HandleBackgroundInitializationFailure(Exception exception, string serviceId)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        var errorReportingService = _serviceProvider.GetService<ErrorReportingService>();
        errorReportingService?.ReportError(
            exception,
            "BackgroundInitialization",
            showToUser: true,
            level: LogEventLevel.Error,
            correlationId: correlationId);

        _logger.LogWarning(exception, "Proceeding with WPF startup despite background initialization issues (CorrelationId: {CorrelationId}, ServiceId: {ServiceId})",
            correlationId, serviceId);
    }

    /// <summary>
    /// CRITICAL STARTUP COMPONENT: Preloads critical resources and pre-JITs important code paths
    /// 
    /// PURPOSE: Reduce first-use latency by warming up commonly used controls and assemblies
    /// 
    /// SUCCESS CRITERIA:
    /// - Syncfusion assemblies loaded without errors
    /// - Resource dictionaries validated
    /// - Verification confirms successful preload
    /// - Timing metrics show reasonable preload duration (<200ms target)
    /// </summary>
    private void PreloadCriticalResources(string serviceId = "unknown")
    {
        var preloadStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var resourcesLoaded = 0;
        var failedResources = new List<string>();
        
        try
        {
            _logger.LogInformation("🚀 [PRELOAD] Starting critical resource preloading - ID: {ServiceId}", serviceId);

            // ✅ Warm up Syncfusion assemblies to avoid first-use delays
            try
            {
                _logger.LogDebug("🔄 Preloading Syncfusion.UI.Xaml.Grid assembly - ID: {ServiceId}", serviceId);
                _ = typeof(Syncfusion.UI.Xaml.Grid.SfDataGrid);
                resourcesLoaded++;
                _logger.LogDebug("   ✓ SfDataGrid type loaded successfully");
                
                _logger.LogDebug("🔄 Preloading Syncfusion.Windows.Tools.Controls assembly - ID: {ServiceId}", serviceId);
                _ = typeof(Syncfusion.Windows.Tools.Controls.Ribbon);
                resourcesLoaded++;
                _logger.LogDebug("   ✓ Ribbon type loaded successfully");
            }
            catch (Exception syncEx)
            {
                failedResources.Add("Syncfusion assemblies");
                _logger.LogWarning(syncEx, "   ⚠️ Failed to preload Syncfusion assemblies - ID: {ServiceId}", serviceId);
            }

            // ✅ Prime merged dictionaries or theme resources if available
            try
            {
                if (Application.Current?.Resources is ResourceDictionary resources)
                {
                    var mergedCount = resources.MergedDictionaries?.Count ?? 0;
                    _logger.LogDebug("🔄 Resource dictionary validation - {MergedCount} merged dictionaries found - ID: {ServiceId}", 
                        mergedCount, serviceId);
                    
                    if (mergedCount > 0)
                    {
                        resourcesLoaded++;
                        _logger.LogDebug("   ✓ Resource dictionaries available and accessible");
                    }
                    else
                    {
                        _logger.LogWarning("   ⚠️ No merged dictionaries found in Application.Resources");
                    }
                }
                else
                {
                    _logger.LogWarning("   ⚠️ Application.Current.Resources not available");
                }
            }
            catch (Exception resEx)
            {
                failedResources.Add("Resource dictionaries");
                _logger.LogWarning(resEx, "   ⚠️ Failed to validate resource dictionaries - ID: {ServiceId}", serviceId);
            }

            preloadStopwatch.Stop();

            // ✅ VERIFICATION: Report preload results
            if (failedResources.Count == 0)
            {
                _logger.LogInformation("✅ [PRELOAD SUCCESS] All critical resources preloaded in {ElapsedMs}ms - ID: {ServiceId}", 
                    preloadStopwatch.ElapsedMilliseconds, serviceId);
                _logger.LogInformation("   ➜ {Count} resources/assemblies preloaded for reduced first-use latency", resourcesLoaded);
                _logger.LogInformation("   ➜ Syncfusion controls ready for instantiation");
            }
            else
            {
                _logger.LogWarning("⚠️ [PRELOAD PARTIAL] Resource preloading completed with warnings in {ElapsedMs}ms - ID: {ServiceId}", 
                    preloadStopwatch.ElapsedMilliseconds, serviceId);
                _logger.LogWarning("   ➜ Successfully preloaded: {Success} resources", resourcesLoaded);
                _logger.LogWarning("   ➜ Failed to preload: {Failed} resources ({FailedList})", 
                    failedResources.Count, string.Join(", ", failedResources));
            }

            // Performance warning for slow preloads
            if (preloadStopwatch.ElapsedMilliseconds > 200)
            {
                _logger.LogWarning("⚠️ [PERFORMANCE] Resource preloading exceeded target duration - ID: {ServiceId}", serviceId);
                _logger.LogWarning("   ➜ Target: <200ms | Actual: {ElapsedMs}ms", preloadStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "❌ [PRELOAD FAILED] Critical resource preloading failed after {ElapsedMs}ms - ID: {ServiceId}",
                preloadStopwatch.ElapsedMilliseconds, serviceId);
            _logger.LogWarning("   ➜ Application will continue but may experience increased first-use latency");
            // Non-fatal: Continue execution even if preloading fails
        }
        finally
        {
            preloadStopwatch.Stop();
        }
    }

    /// <summary>
    /// Creates and shows the main window using dependency injection.
    /// </summary>
    private async Task CreateAndShowMainWindow(string serviceId = "unknown")
    {
        var creationStopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogInformation("=== CREATING AND SHOWING MAIN WINDOW ===");
        _logger.LogInformation("Service ID: {ServiceId}, Thread: {ThreadId}, UI Thread: {IsUIThread}",
            serviceId, Environment.CurrentManagedThreadId, Application.Current?.Dispatcher.CheckAccess() ?? false);

        try
        {
            // Preload critical resources for better performance
            _logger.LogDebug("Preloading critical resources - ID: {ServiceId}", serviceId);
            PreloadCriticalResources(serviceId);

            _logger.LogInformation("Creating MainWindow via Dispatcher.Invoke on STA thread - ID: {ServiceId}", serviceId);

            // ✅ FIX: Create MainWindow on STA thread using Dispatcher.Invoke
            MainWindow? mainWindow = null;
            SplashScreenWindow? splashScreen = null;
            
            if (Application.Current?.Dispatcher != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    _mainWindow = mainWindow;
                    splashScreen = _serviceProvider.GetService<SplashScreenWindow>();
                });
                
                _logger.LogInformation("MainWindow instance created: {WindowType}, Title: '{Title}' - ID: {ServiceId}",
                    mainWindow?.GetType().Name, mainWindow?.Title, serviceId);
                _logger.LogDebug("MainWindow instance created in {ElapsedMs}ms - ID: {ServiceId}",
                    creationStopwatch.ElapsedMilliseconds, serviceId);
            }
            else
            {
                _logger.LogError("Application.Current or Dispatcher is null - cannot create MainWindow");
                throw new InvalidOperationException("WPF Application not properly initialized");
            }
            if (splashScreen != null && mainWindow != null)
            {
                _logger.LogInformation("Attaching splash screen close handler to MainWindow.ContentRendered - ID: {ServiceId}", serviceId);

                mainWindow.ContentRendered += async (_, __) =>
            {
                var splashCloseStopwatch = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation("=== MAIN WINDOW CONTENT RENDERED EVENT FIRED ===");
                _logger.LogInformation("MainWindow content rendered, preparing to close splash screen - ID: {ServiceId}", serviceId);
                try
                {
                    // Ensure DataContext is set after content is rendered for optimal performance
                    if (mainWindow?.DataContext == null)
                    {
                        _logger.LogWarning("DataContext not set on MainWindow after content rendered - ID: {ServiceId}", serviceId);
                    }
                    else
                    {
                        _logger.LogInformation("DataContext is set: {DataContextType} - ID: {ServiceId}",
                            mainWindow.DataContext.GetType().Name, serviceId);
                    }

                    // Handle splash screen closing after UI is fully rendered
                    if (splashScreen != null)
                    {
                        _logger.LogInformation("Closing splash screen after 50ms delay - ID: {ServiceId}", serviceId);
                        // Reduced delay for faster transition to MainWindow
                        await Task.Delay(50);
                        await splashScreen.FadeOutAndCloseAsync();
                        _logger.LogInformation("=== SPLASH SCREEN CLOSED SUCCESSFULLY ===");
                        _logger.LogInformation("Splash screen closed in {ElapsedMs}ms after MainWindow content rendered - ID: {ServiceId}",
                            splashCloseStopwatch.ElapsedMilliseconds, serviceId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to close splash screen after content rendered - ID: {ServiceId}", serviceId);
                    // Fallback: just hide splash screen if it exists
                    if (splashScreen != null)
                    {
                        try
                        {
                            _logger.LogWarning("Attempting fallback splash screen hide - ID: {ServiceId}", serviceId);
                            await splashScreen.HideAsync();
                            _logger.LogInformation("Splash screen hidden via fallback method - ID: {ServiceId}", serviceId);
                        }
                        catch (Exception fallbackEx)
                        {
                            _logger.LogError(fallbackEx, "Fallback splash screen hide also failed - ID: {ServiceId}", serviceId);
                            // Last resort: do nothing, splash will remain
                        }
                    }
                }
            };
            }
            else
            {
                _logger.LogWarning("No splash screen available to close - ID: {ServiceId}", serviceId);
            }

            // Set as the application's main window
            if (Application.Current != null && mainWindow != null)
            {
                _logger.LogInformation("Setting MainWindow as Application.MainWindow - ID: {ServiceId}", serviceId);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Application.Current.MainWindow = mainWindow;
                });
                _logger.LogDebug("MainWindow set as Application.MainWindow - ID: {ServiceId}", serviceId);
            }
            else
            {
                _logger.LogError("Application.Current is null, cannot set MainWindow - ID: {ServiceId}", serviceId);
            }

            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("=== CALLING MainWindow.Show() ===");
            // Show the window
            if (mainWindow != null)
            {
                mainWindow.Show();
                _logger.LogInformation("MainWindow.Show() completed in {ElapsedMs}ms - ID: {ServiceId}",
                    showStopwatch.ElapsedMilliseconds, serviceId);
                _logger.LogInformation("MainWindow visibility: {Visibility}, IsVisible: {IsVisible} - ID: {ServiceId}",
                    mainWindow.Visibility, mainWindow.IsVisible, serviceId);
            }
            else
            {
                _logger.LogError("MainWindow is null, cannot show window - ID: {ServiceId}", serviceId);
            }

            _logger.LogInformation("=== MAIN WINDOW CREATED AND DISPLAYED SUCCESSFULLY ===");
            _logger.LogInformation("Total MainWindow creation time: {TotalElapsedMs}ms - ID: {ServiceId}",
                creationStopwatch.ElapsedMilliseconds, serviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== FAILED TO CREATE/SHOW MAIN WINDOW ===");
            _logger.LogError(ex, "MainWindow creation failed after {ElapsedMs}ms - ID: {ServiceId}",
                creationStopwatch.ElapsedMilliseconds, serviceId);

            // Use error reporting service for structured error handling and UI feedback
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            var errorReportingService = _serviceProvider.GetService<ErrorReportingService>();
            errorReportingService?.ReportError(
                ex,
                "MainWindow_Creation",
                showToUser: true,
                level: LogEventLevel.Fatal,
                correlationId: correlationId);

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

        // Complete the application exit task so StartAsync can complete
        _applicationExitTcs.TrySetResult(true);

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