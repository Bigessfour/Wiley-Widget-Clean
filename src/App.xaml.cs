using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using Serilog.Events;
using System.Windows;
using Syncfusion.Licensing; // Official Syncfusion licensing namespace
using WileyWidget.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using WileyWidget.Configuration;
using WileyWidget.Models;
using WileyWidget.Data;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using Azure;
using System.Threading;
using WileyWidget.ViewModels;
using System.Windows.Controls; // For Grid, TextBlock, Button
using System.Windows.Controls.Primitives; // For GridLength, etc.
using Microsoft.Extensions.Hosting; // Generic Host
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Media; // For brushes/colors
using System.Windows.Automation; // For AutomationProperties

namespace WileyWidget;

/// <summary>
/// OPTIMIZED APPLICATION STARTUP - Following Microsoft WPF Best Practices
/// Key improvements:
/// - Non-blocking license registration
/// - Immediate splash screen
/// - Background service initialization
/// - Better error handling and fallbacks
/// - Enhanced debug instrumentation for startup analysis
/// </summary>
#pragma warning disable CA1001 // Type 'App' owns disposable field(s) '_splashScreen' but is not disposable
public partial class App : Application
{
    /// <summary>
    /// Static service provider for accessing DI services from anywhere in the app
    /// </summary>
    public static IServiceProvider ServiceProvider { get; private set; }

    /// <summary>
    /// Latest health check report for the application
    /// </summary>
    public static HealthCheckReport LatestHealthReport { get; private set; }

    // private IConfiguration _configuration; REMOVED - handled by DI container
    private SplashScreenWindow _splashScreen;
    // Circuit breakers managed locally since this is app-level functionality
    private Dictionary<string, HealthCheckCircuitBreaker> _circuitBreakers = new();

    /// <summary>
    /// Debug instrumentation for startup analysis
    /// </summary>
    private static readonly bool _enableDebugInstrumentation;

    private static readonly string _debugLogPath;

    private static StreamWriter _debugWriter;
    private static readonly object _debugLock = new object();

    /// <summary>
    /// Static constructor to initialize debug instrumentation
    /// </summary>
    static App()
    {
        // FIRST THING: Output to console to prove we get this far
        Console.WriteLine("[DIAG] App static constructor called - application is loading");
        
        _enableDebugInstrumentation = Environment.GetEnvironmentVariable("WILEY_DEBUG_STARTUP") == "true";
        _debugLogPath = Path.Combine(Path.GetTempPath(), "WileyWidget", "startup-debug.log");
        
        Console.WriteLine("[DIAG] App static constructor completed");
    }

    /// <summary>
    /// Initialize debug instrumentation if enabled
    /// </summary>
    private static void InitializeDebugInstrumentation()
    {
        if (!_enableDebugInstrumentation) return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_debugLogPath));
            _debugWriter = new StreamWriter(_debugLogPath, false) { AutoFlush = true };
            LogDebugEvent("STARTUP_DEBUG", "Debug instrumentation initialized");
            LogDebugEvent("SYSTEM_INFO", $"CLR Version: {Environment.Version}");
            LogDebugEvent("SYSTEM_INFO", $"OS: {Environment.OSVersion}");
            LogDebugEvent("SYSTEM_INFO", $"Process: {Process.GetCurrentProcess().ProcessName} (PID: {Process.GetCurrentProcess().Id})");
            LogDebugEvent("SYSTEM_INFO", $"Architecture: {RuntimeInformation.ProcessArchitecture}");

            // Hook into assembly loading events
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
            {
                LogDebugEvent("ASSEMBLY_LOAD", $"{args.LoadedAssembly.GetName().Name} v{args.LoadedAssembly.GetName().Version} from {args.LoadedAssembly.Location}");
            };

            // Hook into assembly resolve events
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                LogDebugEvent("ASSEMBLY_RESOLVE", $"Attempting to resolve: {args.Name}");
                return null;
            };

        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to initialize debug instrumentation");
        }
    }

    /// <summary>
    /// Log a debug event with timestamp
    /// </summary>
    public static void LogDebugEvent(string category, string message)
    {
        if (!_enableDebugInstrumentation || _debugWriter == null) return;

        lock (_debugLock)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _debugWriter.WriteLine($"{timestamp}|{category}|{message}");
            }
            catch { /* Ignore logging failures */ }
        }
    }

    /// <summary>
    /// Log performance timing for startup phases
    /// </summary>
    public static void LogStartupTiming(string phase, TimeSpan duration, string details = null)
    {
        if (!_enableDebugInstrumentation) return;

        var message = $"{phase}: {duration.TotalMilliseconds:F2}ms";
        if (!string.IsNullOrEmpty(details))
            message += $" | {details}";

        LogDebugEvent("TIMING", message);
        Log.Information("Startup timing - {Phase}: {Duration:F2}ms", phase, duration.TotalMilliseconds);
    }
    private IHost _host; // Generic Host instance
    // private HealthCheckConfiguration _healthCheckConfig; REMOVED - handled by DI container
    // private Dictionary<string, HealthCheckCircuitBreaker> _circuitBreakers; REMOVED - handled by health check services
    // Removed unused private field _latestHealthReport to avoid CS0169 warning (LatestHealthReport static property is used instead)

    /// <summary>
    /// Allows hosted/background services to publish the latest health report in a single place.
    /// </summary>
    public static void UpdateLatestHealthReport(HealthCheckReport report)
    {
        LatestHealthReport = report;
    }

    /// <summary>
    /// Get IConfiguration from DI container - replaces direct _configuration field access
    /// </summary>
    private IConfiguration GetConfiguration()
    {
        return ServiceProvider?.GetService<IConfiguration>() ?? throw new InvalidOperationException("IConfiguration not available in DI container");
    }

    /// <summary>
    /// Get HealthCheckService from DI container - replaces _healthCheckConfig field access  
    /// </summary>
    private WileyWidget.Services.HealthCheckService GetHealthCheckService()
    {
        return ServiceProvider?.GetService<WileyWidget.Services.HealthCheckService>() ?? throw new InvalidOperationException("HealthCheckService not available in DI container");
    }

    /// <summary>
    /// Get HealthCheckConfiguration from DI container - replaces _healthCheckConfig field access
    /// </summary>
    private HealthCheckConfiguration GetHealthCheckConfiguration()
    {
        return ServiceProvider?.GetService<HealthCheckConfiguration>() ?? throw new InvalidOperationException("HealthCheckConfiguration not available in DI container");
    }

    /// <summary>
    /// SAFE CONSTRUCTOR: Following Microsoft WPF best practices
    /// Defer all initialization operations per Microsoft guidance:
    /// "Avoid application configuration... Defer initialization operations until after main window is rendered"
    /// </summary>
    public App()
    {
        // SAFE PATTERN: Only minimal, non-blocking setup in constructor
        // Per Microsoft: "avoid calling overridable methods or setting dependency property values from constructor"
        Console.WriteLine("[DIAG] App constructor called - safe initialization pattern");
        
        // Configuration and license registration moved to OnStartup per Microsoft best practices
        Log.Information("=== Application Constructor Initialized (safe pattern) ===");
    }

    /// <summary>
    /// MICROSOFT-COMPLIANT ONSTARTUP: Proper initialization lifecycle
    /// Following Microsoft guidance: "Consider postponing initialization code until after the main application window is rendered"
    /// "After Run is called and the application is initialized, the application is ready to run. This moment is signified when the Startup event is raised"
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var startupId = Guid.NewGuid().ToString("N")[..8];

        // Initialize debug instrumentation FIRST
        InitializeDebugInstrumentation();
        LogDebugEvent("STARTUP_INIT", $"Startup session: {startupId}");

        try
        {
            Log.Information("=== Application Startup (Microsoft-Compliant) - ID: {StartupId} ===", startupId);
            LogDebugEvent("STARTUP_PHASE", "Beginning application startup");

            // MICROSOFT PATTERN: License registration in OnStartup, configuration handled by WpfHostingExtensions
            // Per Microsoft: "Defer initialization operations until after the main application window is rendered"
            LogDebugEvent("STARTUP_PHASE", "Performing Syncfusion license registration");
            RegisterSyncfusionLicense();
            LogDebugEvent("STARTUP_PHASE", "Syncfusion licensing completed");

            // Prevent WPF from automatically creating MainWindow
            // We'll create it manually after DI is ready
            this.MainWindow = null;

            // 0. Initial setup (10%)
            var phaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
            _splashScreen?.UpdateProgress(10, "Configuring application...");
            LogDebugEvent("STARTUP_PHASE", "Phase 0: Initial setup");
            
            ConfigureGlobalExceptionHandling();
            SettingsService.Instance.Load();
            // Apply dark default early if none persisted (normalization happens in MainWindow).
            if (string.IsNullOrWhiteSpace(SettingsService.Instance.Current.Theme))
                SettingsService.Instance.Current.Theme = "FluentDark";
            // Optional: in automated test scenarios we may want to auto-dismiss the Syncfusion trial dialog so processes exit cleanly.
            if (string.Equals(Environment.GetEnvironmentVariable("WILEYWIDGET_AUTOCLOSE_LICENSE"), "1", StringComparison.OrdinalIgnoreCase))
            {
                TryScheduleLicenseDialogAutoClose();
            }
            
            // (Removed) Redundant async license registration call eliminated; handled once in constructor.
            
            Log.Information("Phase 0 - Initial setup completed in {ElapsedMs}ms [{StartupId}]", 
                phaseStopwatch.ElapsedMilliseconds, startupId);
            LogStartupTiming("Phase 0 - Initial Setup", phaseStopwatch.Elapsed);
            LogDebugEvent("STARTUP_PHASE", $"Phase 0 completed in {phaseStopwatch.ElapsedMilliseconds}ms");

            // 1. Show splash screen IMMEDIATELY (perceived performance) (20%)
            phaseStopwatch.Restart();
            _splashScreen?.UpdateProgress(20, "Initializing user interface...");
            LogDebugEvent("STARTUP_PHASE", "Phase 1: Splash screen initialization");
            try
            {
                ShowSplashScreen();
                Log.Information("Phase 1 - Splash screen displayed in {ElapsedMs}ms [{StartupId}]", 
                    phaseStopwatch.ElapsedMilliseconds, startupId);
                LogStartupTiming("Phase 1 - Splash Screen", phaseStopwatch.Elapsed);
                LogDebugEvent("STARTUP_PHASE", $"Phase 1 completed in {phaseStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to show splash screen in {ElapsedMs}ms, continuing without it [{StartupId}]", 
                    phaseStopwatch.ElapsedMilliseconds, startupId);
                LogDebugEvent("STARTUP_WARNING", $"Splash screen failed: {ex.Message}");
            }

            // 2. Build Generic Host with enterprise configuration (40% - 70%)
            phaseStopwatch.Restart();
            _splashScreen?.UpdateProgress(40, "Configuring services...");
            LogDebugEvent("STARTUP_PHASE", "Phase 2: Host and DI configuration");
            // Essential services configuration moved to WpfHostingExtensions.ConfigureWpfApplication()
            Log.Information("Phase 2a - Configuration moved to WpfHostingExtensions [{StartupId}]", startupId);
            LogDebugEvent("STARTUP_PHASE", "Phase 2a - Using WpfHostingExtensions pattern");

            _splashScreen?.UpdateProgress(60, "Building host...");
            LogDebugEvent("STARTUP_PHASE", "Phase 2b: Host building");
            // Bootstrap logger (console) BEFORE full host so early failures are visible
            var bootstrapLogDir = Path.Combine(Path.GetTempPath(), "WileyWidget", "logs");
            try { Directory.CreateDirectory(bootstrapLogDir); } catch { }
            var bootstrapLogFile = Path.Combine(bootstrapLogDir, "bootstrap.log");
            var bootstrapLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(outputTemplate: "[BOOTSTRAP {Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(bootstrapLogFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 3, shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff}|{Level:u3}|{Message:lj}{NewLine}{Exception}")
                .CreateLogger();
            Serilog.Log.Logger = bootstrapLogger;

            var hostBuilder = Host.CreateApplicationBuilder();
            // Register splash screen in DI container so HostedWpfApplication can access it
            if (_splashScreen != null)
            {
                hostBuilder.Services.AddSingleton(_splashScreen);
            }
            // Delegate DI, logging, config, DB services, hosted services to our extension
            hostBuilder.ConfigureWpfApplication();
            try
            {
                _host = hostBuilder.Build();
            }
            catch (Exception earlyEx)
            {
                Console.Error.WriteLine("EARLY HOST BUILD FAILURE: " + earlyEx.Message);
                Console.Error.WriteLine(earlyEx);
                try { Serilog.Log.Fatal(earlyEx, "Host build failed before full logging initialization"); } catch { }
                ShowFallbackUI(earlyEx);
                Shutdown();
                return;
            }
            Log.Information("Phase 2b - Host built in {ElapsedMs}ms [{StartupId}]", 
                phaseStopwatch.ElapsedMilliseconds, startupId);
            LogDebugEvent("STARTUP_PHASE", $"Phase 2b completed in {phaseStopwatch.ElapsedMilliseconds}ms");

            // Expose ServiceProvider for legacy paths
            ServiceProvider = _host.Services;
            Application.Current.Properties["ServiceProvider"] = ServiceProvider;

            // Essential service initialization moved to HostedWpfApplication service
            Log.Information("Service initialization delegated to HostedWpfApplication service");

            // In development, ensure the SQLite database is created before health checks
            try
            {
                await DatabaseConfiguration.EnsureDevDatabaseIfNeededAsync(ServiceProvider);
            }
            catch (Exception ensureDbEx)
            {
                Log.Warning(ensureDbEx, "Dev database ensure failed; continuing");
            }

            // Start the host (HostedWpfApplication will create and show MainWindow)
            phaseStopwatch.Restart();
            _splashScreen?.UpdateProgress(70, "Starting services...");
            LogDebugEvent("STARTUP_PHASE", "Phase 3: Host startup and MainWindow creation");
            bool hostStartedSuccessfully = false;
            try
            {
                await _host.StartAsync();
                hostStartedSuccessfully = true;
                Log.Information("Phase 3 - Host started successfully in {ElapsedMs}ms [{StartupId}]", 
                    phaseStopwatch.ElapsedMilliseconds, startupId);
                LogStartupTiming("Phase 3 - Host Startup", phaseStopwatch.Elapsed);
                LogDebugEvent("STARTUP_PHASE", $"Phase 3 completed in {phaseStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception hostEx)
            {
                Log.Error(hostEx, "Failed to start application host after {ElapsedMs}ms - MainWindow creation failed [{StartupId}]", 
                    phaseStopwatch.ElapsedMilliseconds, startupId);
                LogDebugEvent("STARTUP_ERROR", $"Host startup failed: {hostEx.Message}");

                // Close splash screen before showing fallback UI
                await CloseSplashScreenOnFailureAsync();

                // Show error dialog and exit gracefully
                ShowFallbackUI(hostEx);
                Shutdown();
                return;
            }

            // Only proceed with WPF initialization if host started successfully
            if (hostStartedSuccessfully)
            {
                // Replace bootstrap logger with fully configured logger is already done inside ConfigureWpfApplication
                phaseStopwatch.Restart();
                // Now call base.OnStartup to complete WPF initialization
                base.OnStartup(e);
                Log.Information("Phase 4 - WPF initialization completed in {ElapsedMs}ms [{StartupId}]", 
                    phaseStopwatch.ElapsedMilliseconds, startupId);

                // Splash screen closing is now handled by HostedWpfApplication when MainWindow ContentRendered fires

                // 5. Complete initialization (100%)
                _splashScreen?.Complete();

                // 6. Background initialization is handled by hosted services now

                Log.Information("=== Application Startup Completed in {TotalElapsedMs}ms - ID: {StartupId} ===", 
                    startupStopwatch.ElapsedMilliseconds, startupId);

                // Record startup metrics
                try
                {
                    var metricsService = ServiceProvider?.GetService<ApplicationMetricsService>();
                    metricsService?.RecordStartup(startupStopwatch.Elapsed.TotalMilliseconds, true);
                }
                catch (Exception metricsEx)
                {
                    Log.Warning(metricsEx, "Failed to record startup metrics");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during application startup after {ElapsedMs}ms [{StartupId}]", 
                startupStopwatch.ElapsedMilliseconds, startupId);

            // Record startup failure metrics
            try
            {
                var metricsService = ServiceProvider?.GetService<ApplicationMetricsService>();
                metricsService?.RecordStartup(startupStopwatch.Elapsed.TotalMilliseconds, false);
            }
            catch (Exception metricsEx)
            {
                Log.Warning(metricsEx, "Failed to record startup failure metrics");
            }

            // Close splash screen before showing fallback UI (only if not already closed)
            if (_splashScreen != null)
            {
                try
                {
                    await _splashScreen.FadeOutAndCloseAsync();
                    _splashScreen = null;
                    Log.Information("Splash screen closed due to startup failure [{StartupId}]", startupId);
                }
                catch (Exception splashEx)
                {
                    Log.Warning(splashEx, "Failed to close splash screen during startup failure [{StartupId}]", startupId);
                    // Fallback to HideSplashScreen
                    HideSplashScreen();
                }
            }

            // Show error dialog and exit gracefully
            ShowFallbackUI(ex);
            Shutdown();
        }
        finally
        {
            startupStopwatch.Stop();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_splashScreen != null)
            {
                try { _splashScreen.Dispose(); } catch { }
                _splashScreen = null;
            }
            if (_host != null)
            {
                // Stop and dispose the host cleanly
                _host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while stopping host on application exit");
        }
        finally
        {
            Log.CloseAndFlush();
        }
        base.OnExit(e);
    }

    // Tracking fields for improved license handling lifecycle
    private static bool _syncfusionLicenseAttempted;
    private static bool _syncfusionLicenseRegistered;
    private static string _syncfusionLicenseValidationMessage;

    /// <summary>
    /// Loads configuration (if not already loaded) and performs a single-pass Syncfusion license resolution + registration.
    /// This replaces the previous dual synchronous + async registration pattern to avoid redundant calls and potential race conditions.
    /// </summary>
    /// <summary>
    /// SIMPLIFIED: Only handles Syncfusion license registration
    /// Configuration loading now handled by WpfHostingExtensions.ConfigureWpfApplication()
    /// </summary>
    private void RegisterSyncfusionLicense()
    {
        // Configuration is now handled by DI container, get license from environment/config
        // Since this is called before DI is set up, use direct environment access

        try
        {
            var licenseKey = GetSyncfusionLicenseKey();
            _syncfusionLicenseAttempted = true;

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                _syncfusionLicenseValidationMessage = "License key not found (env/config/user-secrets).";
                System.Diagnostics.Debug.WriteLine("[Syncfusion] No license key found. Evaluation mode likely.");
                return; // Do not throw – allow app to run in eval for dev.
            }

            if (licenseKey == "${SYNCFUSION_LICENSE_KEY}")
            {
                _syncfusionLicenseValidationMessage = "Placeholder license key detected; replace with a real key.";
                System.Diagnostics.Debug.WriteLine("[Syncfusion] Placeholder key detected; skipping registration.");
                return;
            }

            if (licenseKey.Length < 32)
            {
                _syncfusionLicenseValidationMessage = "License key appears invalid (too short).";
                System.Diagnostics.Debug.WriteLine("[Syncfusion] Key too short; skipping registration.");
                return;
            }

            // Register exactly once. Syncfusion's RegisterLicense is idempotent but we still guard for clarity.
            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            _syncfusionLicenseRegistered = true;
            _syncfusionLicenseValidationMessage = "License registration succeeded.";
            System.Diagnostics.Debug.WriteLine("[Syncfusion] License registered successfully.");
        }
        catch (Exception ex)
        {
            _syncfusionLicenseRegistered = false;
            _syncfusionLicenseValidationMessage = $"Registration exception: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Syncfusion] License registration failed: {ex.Message}");
        }

        // Emit structured log after attempt (Serilog may not yet be fully configured; use both Debug + minimal Console fallback)
        try
        {
            var status = _syncfusionLicenseRegistered ? "Registered" : "NotRegistered";
            var attempted = _syncfusionLicenseAttempted ? "Yes" : "No";
            var msg = _syncfusionLicenseValidationMessage;
            System.Diagnostics.Debug.WriteLine($"[Syncfusion] Status={status} Attempted={attempted} Details={msg}");
            // Use Console early in case logger not wired yet
            Console.WriteLine($"[Syncfusion] Status={status}; Details={msg}");
        }
        catch { /* swallow */ }
    }

    // LoadConfiguration() method REMOVED - handled by WpfHostingExtensions.ConfigureApplicationConfiguration()
    // ConfigureLogging() method REMOVED - handled by WpfHostingExtensions.ConfigureApplicationLogging()

    // ConfigureServices() method REMOVED - handled by WpfHostingExtensions.ConfigureCoreServices() and ConfigureWpfServices()
    // RegisterSyncfusionLicense() stub method REMOVED - functionality in InitializeConfigurationAndRegisterSyncfusionLicense()

    private void ConfigureGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            Services.ErrorReportingService.Instance.ReportError(ex, "AppDomain_Unhandled", showToUser: false, level: LogEventLevel.Fatal);
            ShowCriticalErrorDialog(ex);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            Services.ErrorReportingService.Instance.ReportError(args.Exception, "Dispatcher_Unhandled", showToUser: true);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Services.ErrorReportingService.Instance.ReportError(args.Exception, "Task_Unobserved", showToUser: false);
            args.SetObserved();
        };
    }

    private void ShowCriticalErrorDialog(Exception ex)
    {
        try
        {
            MessageBox.Show(
                $"A critical error occurred and the application must close:\n\n{ex.Message}\n\nPlease restart the application.",
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // If dialog fails, at least log it
            Log.Fatal("Failed to show critical error dialog");
        }
    }

    private void ShowRecoverableErrorDialog(Exception ex)
    {
        try
        {
            var result = MessageBox.Show(
                $"An error occurred:\n\n{ex.Message}\n\nWould you like to continue? (Some features may not work properly)",
                "Application Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.No)
            {
                Log.Information("User chose to exit after error");
                Shutdown();
            }
            else
            {
                Log.Information("User chose to continue after error");
            }
        }
        catch
        {
            Log.Error("Failed to show recoverable error dialog");
        }
    }

    private void ShowSplashScreen()
    {
        var splashStopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Try to use ViewManager if ServiceProvider is available
            if (ServiceProvider?.GetService<IViewManager>() is IViewManager viewManager)
            {
                // Use ViewManager for centralized view management
                Task.Run(async () => await viewManager.ShowSplashScreenAsync(System.Threading.CancellationToken.None));
                Log.Information("Splash screen displayed via ViewManager in {ElapsedMs}ms", splashStopwatch.ElapsedMilliseconds);
            }
            else
            {
                // Fallback to direct instantiation for early startup
                _splashScreen = new SplashScreenWindow();
                _splashScreen.Show();
                Log.Information("Custom splash screen displayed directly in {ElapsedMs}ms", splashStopwatch.ElapsedMilliseconds);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show splash screen after {ElapsedMs}ms, continuing without it", splashStopwatch.ElapsedMilliseconds);
        }
        finally
        {
            splashStopwatch.Stop();
        }
    }

    private void HideSplashScreen()
    {
        if (_splashScreen != null)
        {
            // Use async hide method for smooth fade effect
            Task.Run(async () =>
            {
                await _splashScreen.HideAsync();
                _splashScreen = null;
            });
        }
    }

    private void ValidateConfiguration()
    {
        try
        {
            Log.Information("Validating application configuration...");

            // Check Syncfusion license
            var licenseKey = GetSyncfusionLicenseKey();
            if (string.IsNullOrEmpty(licenseKey))
            {
                Log.Warning("Syncfusion license key not found in configuration. Evaluation dialogs may appear.");
            }
            else
            {
                Log.Information("Syncfusion license key found and validated");
            }

            // Check database connection string
            var config = GetConfiguration();
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                Log.Warning("Database connection string not found. Application may not function properly.");
            }
            else
            {
                Log.Information("Database connection string found");
            }

            // Check Azure AD configuration
            var clientId = config["AzureAd:ClientId"];
            var tenantId = config["AzureAd:TenantId"];
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
            {
                Log.Warning("Azure AD configuration incomplete. Authentication may not work.");
            }
            else
            {
                Log.Information("Azure AD configuration validated");
            }

            Log.Information("Configuration validation completed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Configuration validation failed");
            // Don't throw - allow app to continue with warnings
        }
    }

    // ConfigureEssentialServices() method REMOVED - handled by WpfHostingExtensions
    // InitializeEssentialServices() method REMOVED - handled by HostedWpfApplication service

    private async Task InitializeBackgroundServicesAsync()
    {
        try
        {
            // Initialize database and other services in background
            await Task.Run(() => InitializeDatabaseAsync());
            await Task.Run(() => InitializeAzureServicesAsync());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during background service initialization");
        }
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            // Ensure database is created and migrated
            await WileyWidget.Configuration.DatabaseConfiguration.EnsureDatabaseCreatedAsync(ServiceProvider);
            
            // Validate database schema
            await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(ServiceProvider);
            
            Log.Information("Database initialized successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database initialization failed");
            // Continue without database - app can still run with sample data
        }
    }

    private async Task InitializeAzureServicesAsync()
    {
        try
        {
            // Initialize authentication service
            var authService = ServiceProvider.GetService<AuthenticationService>();
            if (authService != null)
            {
                Log.Information("Azure AD authentication service configured");
            }

            // TODO: Add other Azure services initialization here
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Azure services initialization failed");
        }
    }

    private async Task<HealthCheckReport> PerformHealthChecksAsync()
    {
        var totalStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var report = new HealthCheckReport
        {
            Timestamp = DateTime.UtcNow,
            OverallStatus = HealthStatus.Healthy
        };

        try
        {
            Log.Information("=== ENTERPRISE HEALTH CHECKS STARTED ===");

            // Execute health checks with resilience patterns
            var healthCheckTasks = new List<Task<HealthCheckResult>>();

            // Determine which services are critical (from config) and which are non-critical
            var healthCheckConfig = GetHealthCheckConfiguration();
            var criticalSet = new HashSet<string>(healthCheckConfig.CriticalServices, StringComparer.OrdinalIgnoreCase);

            // Helper local function to enqueue a check respecting skip list and deferral
            void Enqueue(string name, Func<Task<HealthCheckResult>> factory)
            {
                if (IsServiceSkipped(name)) return;
                if (healthCheckConfig.DeferNonCriticalChecks && !criticalSet.Contains(name))
                {
                    // Defer execution by scheduling on thread pool AFTER initial critical checks complete
                    Task.Run(async () =>
                    {
                        var deferredResult = await ExecuteHealthCheckWithResilienceAsync(name, factory());
                        lock(report)
                        {
                            report.Results.Add(deferredResult);
                            // Update overall status if worsened
                            report.OverallStatus = DetermineOverallHealthStatus(report.Results);
                            UpdateLatestHealthReport(report);
                        }
                        Log.Information("Deferred health check completed: {Service} -> {Status}", name, deferredResult.Status);
                    });
                }
                else
                {
                    healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync(name, factory()));
                }
            }

            // Core services (always checked immediately)
            Enqueue("Configuration", CheckConfigurationHealthAsync);
            Enqueue("Database", CheckDatabaseHealthAsync);
            Enqueue("Syncfusion License", CheckSyncfusionLicenseHealthAsync);

            // External services (may be deferred)
            Enqueue("Azure AD", CheckAzureAdHealthAsync);
            Enqueue("Azure Key Vault", CheckAzureKeyVaultHealthAsync);
            Enqueue("QuickBooks", CheckQuickBooksHealthAsync);
            Enqueue("AI Service", CheckAIServiceHealthAsync);
            Enqueue("External Dependencies", CheckExternalDependenciesHealthAsync);
            Enqueue("System Resources", CheckSystemResourcesHealthAsync);

            // Wait for all health checks to complete with overall timeout
            var timeoutTask = Task.Delay(healthCheckConfig.DefaultTimeout);
            var completedTask = await Task.WhenAny(Task.WhenAll(healthCheckTasks), timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Warning("Health checks timed out after {Timeout}s", healthCheckConfig.DefaultTimeout.TotalSeconds);

                // Create timeout results for incomplete tasks
                var timedOutResults = healthCheckTasks
                    .Where(t => !t.IsCompletedSuccessfully)
                    .Select(t => HealthCheckResult.Unhealthy("Unknown Service", "Health check timed out", null, healthCheckConfig.DefaultTimeout));

                report.Results.AddRange(timedOutResults);
            }

            // Collect successful results
            var completedResults = healthCheckTasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToList();
            report.Results.AddRange(completedResults);

            // Handle failed tasks
            var failedTasks = healthCheckTasks.Where(t => t.IsFaulted).ToList();
            foreach (var failedTask in failedTasks)
            {
                var exception = failedTask.Exception?.InnerException ?? failedTask.Exception;
                Log.Error(exception, "Health check task failed");

                // Create failed result
                var failedResult = HealthCheckResult.Unhealthy("Unknown Service", $"Health check failed: {exception?.Message}", exception);
                report.Results.Add(failedResult);
            }

            // Determine overall status
            report.OverallStatus = DetermineOverallHealthStatus(report.Results);

            // Log results
            LogHealthCheckResults(report);

            // Check if application can start
            if (!CanApplicationStart(report) && !healthCheckConfig.ContinueOnFailure)
            {
                throw new InvalidOperationException("Application cannot start due to critical service failures");
            }

            Log.Information("=== ENTERPRISE HEALTH CHECKS COMPLETED ===");
            return report;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during health checks");
            report.OverallStatus = HealthStatus.Unhealthy;
            report.Results.Add(HealthCheckResult.Unhealthy("HealthCheckSystem", "Health check system failed", ex));
            return report;
        }
        finally
        {
            totalStopwatch.Stop();
            report.TotalDuration = totalStopwatch.Elapsed;
            Log.Information("Total health check duration: {Duration}ms", report.TotalDuration.TotalMilliseconds);
        }
    }

    private async Task<HealthCheckResult> CheckDatabaseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Check if database is configured
            var config = GetConfiguration();
            var connectionString = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                return HealthCheckResult.Unhealthy("Database", "Database connection string not configured");
            }

            // Validate database schema and connectivity using the IDbContextFactory to avoid scoped-from-singleton issues
            await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(ServiceProvider);

            // Additional connectivity check using IDbContextFactory if available
            using (var scope = ServiceProvider.CreateScope())
            {
                var provider = scope.ServiceProvider;
                var contextFactory = provider.GetService<Microsoft.EntityFrameworkCore.IDbContextFactory<WileyWidget.Data.AppDbContext>>();
                if (contextFactory != null)
                {
                    await using var dbContext = await contextFactory.CreateDbContextAsync();
                    await dbContext.Database.CanConnectAsync();
                }
                else
                {
                    // Fall back to resolving AppDbContext if factory not available (legacy registration)
                    var dbContext = provider.GetService<WileyWidget.Data.AppDbContext>();
                    if (dbContext != null)
                        await dbContext.Database.CanConnectAsync();
                }
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("Database", "Database connection and schema validation successful", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Warning(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database", $"Database health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckConfigurationHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check required configuration sections
            var configuration = GetConfiguration();
            if (string.IsNullOrEmpty(configuration["AzureAd:ClientId"]))
                issues.Add("Azure AD Client ID not configured");

            if (string.IsNullOrEmpty(configuration.GetConnectionString("DefaultConnection")))
                issues.Add("Database connection string not configured");

            // Consider env var, config, or user secrets for Syncfusion key
            if (string.IsNullOrEmpty(GetSyncfusionLicenseKey()))
                issues.Add("Syncfusion license key not configured");

            // Check environment variables for critical services
            var criticalEnvVars = new[] { "QBO_CLIENT_ID", "QBO_REALM_ID" };
            foreach (var envVar in criticalEnvVars)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.User)))
                {
                    issues.Add($"Environment variable {envVar} not set");
                }
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("Configuration",
                    $"Configuration validation found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Configuration", "All required configuration validated successfully", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Configuration", $"Configuration validation failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckAzureAdHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var authService = ServiceProvider.GetService<AuthenticationService>();
            if (authService == null)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Authentication service not available"));
            }

            // Check configuration
            var configuration = GetConfiguration();
            if (string.IsNullOrEmpty(configuration["AzureAd:ClientId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Client ID not configured"));
            }

            if (string.IsNullOrEmpty(configuration["AzureAd:TenantId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Tenant ID not configured"));
            }

            // Basic connectivity check (without actual authentication)
            // This validates that the configuration is correct for potential authentication
            var clientId = configuration["AzureAd:ClientId"];
            var tenantId = configuration["AzureAd:TenantId"];

            if (!Guid.TryParse(clientId, out _) || !Guid.TryParse(tenantId, out _))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Client ID or Tenant ID is not a valid GUID"));
            }

            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("Azure AD", "Azure AD configuration validated successfully", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", $"Azure AD health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckAzureKeyVaultHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var keyVaultService = ServiceProvider.GetService<IAzureKeyVaultService>();
            if (keyVaultService == null)
            {
                return HealthCheckResult.Unavailable("Azure Key Vault", "Azure Key Vault service not configured");
            }

            var configuration = GetConfiguration();
            var keyVaultUrl = configuration["Azure:KeyVault:Url"];
            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                return HealthCheckResult.Unavailable("Azure Key Vault", "Azure Key Vault URL not configured");
            }

            // Use service level TestConnection with timeout budget (3s)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            bool connected = false;
            try
            {
                connected = await keyVaultService.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Azure Key Vault test connection threw");
            }
            finally
            {
                stopwatch.Stop();
            }

            if (connected)
                return HealthCheckResult.Healthy("Azure Key Vault", "Azure Key Vault reachable", stopwatch.Elapsed);

            return HealthCheckResult.Degraded("Azure Key Vault", "Azure Key Vault unreachable or insufficient permissions", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("Azure Key Vault", $"Azure Key Vault health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckQuickBooksHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Resolve scoped QuickBooks service from a created scope to avoid resolving scoped service from root
            using var scope = ServiceProvider.CreateScope();
            var qbService = scope.ServiceProvider.GetService<IQuickBooksService>();
            if (qbService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("QuickBooks", "QuickBooks service not available"));
            }

            // Check configuration
            var clientId = Environment.GetEnvironmentVariable("QBO_CLIENT_ID", EnvironmentVariableTarget.User);
            var realmId = Environment.GetEnvironmentVariable("QBO_REALM_ID", EnvironmentVariableTarget.User);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(realmId))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("QuickBooks", "QuickBooks Client ID or Realm ID not configured"));
            }

            // Check if token is valid; attempt silent refresh once if invalid
            if (qbService is QuickBooksService concreteService)
            {
                if (!concreteService.HasValidAccessToken())
                {
                    // Silent refresh method not yet implemented; classify as degraded and allow startup
                    stopwatch.Stop();
                    return Task.FromResult(HealthCheckResult.Degraded("QuickBooks", "Access token missing/expired (no silent refresh available)", stopwatch.Elapsed));
                }
            }

            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("QuickBooks", "QuickBooks service configured and token is valid", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("QuickBooks", $"QuickBooks health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckSyncfusionLicenseHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Evaluate status flags produced during early startup
            if (!_syncfusionLicenseAttempted)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("Syncfusion License", "License registration not attempted yet", null, stopwatch.Elapsed));
            }

            if (_syncfusionLicenseRegistered)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Healthy("Syncfusion License", _syncfusionLicenseValidationMessage, stopwatch.Elapsed)); // assuming existing signature matches
            }

            // Not registered – determine severity based on reason
            var message = _syncfusionLicenseValidationMessage ?? "License not registered for unknown reason";

            // Placeholder or missing key -> Unhealthy (action required)
            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", message, null, stopwatch.Elapsed));
            }

            // Format issue -> Unhealthy
            if (message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("short", StringComparison.OrdinalIgnoreCase))
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", message, null, stopwatch.Elapsed));
            }

            // Registration exception -> Degraded (app can continue in eval mode)
            if (message.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Degraded("Syncfusion License", message, stopwatch.Elapsed));
            }

            // Fallback classification
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Degraded("Syncfusion License", message, stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", $"Syncfusion license health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private Task<HealthCheckResult> CheckAIServiceHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Resolve AI service from a scope to respect scoped lifetime
            using var scope = ServiceProvider.CreateScope();
            var aiService = scope.ServiceProvider.GetService<IAIService>();
            if (aiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "AI service not configured"));
            }

            // Check configuration
            // Prefer configuration (Key Vault injected) before falling back to environment
            var configuration = GetConfiguration();
            var openAiKey = configuration["Secrets:OpenAI:ApiKey"] ?? configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var azureAiEndpoint = configuration["Secrets:AzureAI:Endpoint"] ?? configuration["Azure:AI:Endpoint"];    

            if (string.IsNullOrEmpty(openAiKey) && string.IsNullOrEmpty(azureAiEndpoint))
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "Neither OpenAI API key nor Azure AI endpoint configured"));
            }
            // Perform lightweight connectivity probe (2s budget)
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(2));
            var clientFactory = scope.ServiceProvider.GetService<System.Net.Http.IHttpClientFactory>();
            using var httpClient = clientFactory != null ? clientFactory.CreateClient("AIHealthCheck") : new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WileyWidget-AIHealthCheck/1.0");
            System.Net.Http.HttpResponseMessage response = null;
            string target = null;
            bool usedOpenAi = false;

            try
            {
                if (!string.IsNullOrWhiteSpace(azureAiEndpoint))
                {
                    // Normalize endpoint
                    var endpoint = azureAiEndpoint.TrimEnd('/');
                    // Use a metadata style call (models list is provider-specific; we just check reachability)
                    target = endpoint + "/openai/deployments?api-version=2023-05-01"; // harmless list request (may 401)
                    response = httpClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
                else if (!string.IsNullOrWhiteSpace(openAiKey))
                {
                    usedOpenAi = true;
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiKey);
                    target = "https://api.openai.com/v1/models";
                    response = httpClient.GetAsync(target, cts.Token).GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Degraded("AI Service", "Connectivity probe timed out", stopwatch.Elapsed));
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", $"Connectivity error: {ex.Message}", ex, stopwatch.Elapsed));
            }

            if (response == null)
            {
                stopwatch.Stop();
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "Probe did not execute (no endpoint chosen)", null, stopwatch.Elapsed));
            }

            var status = (int)response.StatusCode;
            string detail = $"Probe {(usedOpenAi ? "OpenAI" : "Azure AI")} status {(int)response.StatusCode} {response.ReasonPhrase}";

            HealthCheckResult result;
            if (status == 200)
            {
                result = HealthCheckResult.Healthy("AI Service", detail, stopwatch.Elapsed);
            }
            else if (status == 401 || status == 403)
            {
                // Auth/config issue – user action required
                result = HealthCheckResult.Unhealthy("AI Service", detail, null, stopwatch.Elapsed);
            }
            else if (status == 429 || (status >= 500 && status <= 599))
            {
                // Transient or capacity issue
                result = HealthCheckResult.Degraded("AI Service", detail, stopwatch.Elapsed);
            }
            else
            {
                // Unexpected but reachable
                result = HealthCheckResult.Degraded("AI Service", detail, stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("AI Service", $"AI service health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private async Task<HealthCheckResult> CheckExternalDependenciesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check internet connectivity (basic)
            try
            {
                // Prefer HttpClient from DI to reuse handlers; fall back to ephemeral client if not available
                System.Net.Http.HttpResponseMessage response = null;
                using (var scope = ServiceProvider.CreateScope())
                {
                    var client = scope.ServiceProvider.GetService<System.Net.Http.HttpClient>();
                    if (client != null)
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        response = await client.GetAsync("https://www.microsoft.com");
                    }
                    else
                    {
                        using var tmpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        response = await tmpClient.GetAsync("https://www.microsoft.com");
                    }
                }

                if (response == null || !response.IsSuccessStatusCode)
                {
                    issues.Add("Internet connectivity check failed");
                }
            }
            catch
            {
                issues.Add("Internet connectivity check failed");
            }

            // Check Azure endpoints if configured
            var configuration = GetConfiguration();
            var azureSqlConnection = configuration.GetConnectionString("AzureConnection");
            if (!string.IsNullOrEmpty(azureSqlConnection))
            {
                // Basic Azure SQL connectivity check would go here
                // For now, just validate the connection string format
                if (!azureSqlConnection.Contains("database.windows.net"))
                {
                    issues.Add("Azure SQL connection string format appears invalid");
                }
            }

            stopwatch.Stop();

            if (issues.Any())
            {
                return HealthCheckResult.Degraded("External Dependencies",
                    $"External dependencies check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed);
            }

            return HealthCheckResult.Healthy("External Dependencies", "All external dependencies accessible", stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HealthCheckResult.Unhealthy("External Dependencies", $"External dependencies health check failed: {ex.Message}", ex, stopwatch.Elapsed);
        }
    }

    private Task<HealthCheckResult> CheckSystemResourcesHealthAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var issues = new List<string>();

            // Check available memory
            var memoryInfo = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            var memoryGB = memoryInfo / (1024.0 * 1024.0 * 1024.0);

            if (memoryGB > 4.0) // Arbitrary threshold
            {
                issues.Add($"High memory usage: {memoryGB:F2} GB");
            }

            // Check available disk space
            var driveInfo = new System.IO.DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory));
            var availableGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

            if (availableGB < 1.0) // Less than 1GB free
            {
                issues.Add($"Low disk space: {availableGB:F2} GB available");
            }

            // Check CPU usage (rough estimate)
            if (OperatingSystem.IsWindows())
            {
                var cpuUsageSeconds = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
                Log.Debug("Process CPU time recorded for health check: {CpuSeconds}s", cpuUsageSeconds);
            }
            else
            {
                Log.Debug("Skipping process CPU time check because it isn't supported on this platform.");
            }
            // This is total CPU time, not current usage - would need more complex monitoring

            stopwatch.Stop();

            if (issues.Any())
            {
                return Task.FromResult(HealthCheckResult.Degraded("System Resources",
                    $"System resources check found {issues.Count} issues: {string.Join(", ", issues)}", stopwatch.Elapsed));
            }

            return Task.FromResult(HealthCheckResult.Healthy("System Resources", "System resources within acceptable limits", stopwatch.Elapsed));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Unhealthy("System Resources", $"System resources health check failed: {ex.Message}", ex, stopwatch.Elapsed));
        }
    }

    private HealthStatus DetermineOverallHealthStatus(List<HealthCheckResult> results)
    {
        if (results.Any(r => r.Status == HealthStatus.Unhealthy))
            return HealthStatus.Unhealthy;

        if (results.Any(r => r.Status == HealthStatus.Degraded))
            return HealthStatus.Degraded;

        if (results.Any(r => r.Status == HealthStatus.Unavailable))
            return HealthStatus.Degraded; // Treat unavailable as degraded, not unhealthy

        return HealthStatus.Healthy;
    }

    private void LogHealthCheckResults(HealthCheckReport report)
    {
        Log.Information("Health Check Summary - Overall Status: {Status}, Total Duration: {Duration}ms",
            report.OverallStatus, report.TotalDuration.TotalMilliseconds);

        foreach (var result in report.Results.OrderBy(r => r.ServiceName))
        {
            var logLevel = result.Status switch
            {
                HealthStatus.Healthy => Serilog.Events.LogEventLevel.Information,
                HealthStatus.Degraded => Serilog.Events.LogEventLevel.Warning,
                HealthStatus.Unhealthy => Serilog.Events.LogEventLevel.Error,
                HealthStatus.Unavailable => Serilog.Events.LogEventLevel.Warning,
                _ => Serilog.Events.LogEventLevel.Information
            };

            Log.Write(logLevel, "Health Check - {Service}: {Status} ({Duration}ms) - {Description}",
                result.ServiceName, result.Status, result.Duration.TotalMilliseconds, result.Description);

            if (result.Exception != null)
            {
                Log.Error(result.Exception, "Health check exception for {Service}", result.ServiceName);
            }
        }

        Log.Information("Health Check Statistics - Healthy: {Healthy}, Degraded: {Degraded}, Unhealthy: {Unhealthy}, Unavailable: {Unavailable}",
            report.HealthyCount, report.DegradedCount, report.UnhealthyCount, report.UnavailableCount);
    }

    private bool IsServiceSkipped(string serviceName)
    {
        var healthCheckConfig = GetHealthCheckConfiguration(); 
        return healthCheckConfig.SkipServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HealthCheckResult> ExecuteHealthCheckWithResilienceAsync(string serviceName, Task<HealthCheckResult> healthCheckTask)
    {
        // Get circuit breaker for this service
        if (!_circuitBreakers.TryGetValue(serviceName, out var circuitBreaker))
        {
            var metricsService = ServiceProvider?.GetService<ApplicationMetricsService>();
            circuitBreaker = new HealthCheckCircuitBreaker(
                metricsService: metricsService, 
                serviceName: serviceName);
            _circuitBreakers[serviceName] = circuitBreaker;
        }

        // Execute with circuit breaker and retry logic
        return await ExecuteWithRetryAndCircuitBreakerAsync(
            serviceName,
            async () => await healthCheckTask,
            circuitBreaker);
    }

    private async Task<HealthCheckResult> ExecuteWithRetryAndCircuitBreakerAsync(
        string serviceName,
        Func<Task<HealthCheckResult>> healthCheckFunc,
        HealthCheckCircuitBreaker circuitBreaker)
    {
        var healthCheckConfig = GetHealthCheckConfiguration();

        for (int attempt = 0; attempt <= healthCheckConfig.MaxRetries; attempt++)
        {
            try
            {
                // Use circuit breaker
                return await circuitBreaker.ExecuteAsync(async () =>
                {
                    // Add timeout to individual health check
                    var timeout = GetTimeoutForService(serviceName);
                    using var cts = new CancellationTokenSource(timeout);
                    var task = healthCheckFunc();

                    var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
                    if (completedTask != task)
                    {
                        throw new TimeoutException($"Health check for {serviceName} timed out after {timeout.TotalSeconds}s");
                    }

                    return await task;
                });
            }
            catch (CircuitBreakerOpenException)
            {
                // Circuit breaker is open, fail fast
                return HealthCheckResult.Unavailable(serviceName, "Circuit breaker is open - service temporarily unavailable");
            }
            catch (Exception ex)
            {
                if (attempt < healthCheckConfig.MaxRetries)
                {
                    Log.Warning(ex, "Health check for {Service} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                        serviceName, attempt + 1, healthCheckConfig.MaxRetries + 1, healthCheckConfig.RetryDelay.TotalMilliseconds);

                    // Exponential backoff
                    var delay = healthCheckConfig.RetryDelay * Math.Pow(2, attempt);
                    await Task.Delay(delay);
                    continue;
                }

                return HealthCheckResult.Unhealthy(serviceName,
                    $"Health check failed after {healthCheckConfig.MaxRetries + 1} attempts: {ex.Message}",
                    ex);
            }
        }

        // All retries exhausted
        return HealthCheckResult.Unhealthy(serviceName,
            $"Health check failed after {healthCheckConfig.MaxRetries + 1} attempts.");
    }

    private TimeSpan GetTimeoutForService(string serviceName)
    {
        var healthCheckConfig = GetHealthCheckConfiguration();
        return serviceName switch
        {
            "Database" => healthCheckConfig.DatabaseTimeout,
            "Azure AD" or "Azure Key Vault" or "QuickBooks" or "AI Service" or "External Dependencies" => healthCheckConfig.ExternalServiceTimeout,
            _ => healthCheckConfig.DefaultTimeout
        };
    }

    private bool CanApplicationStart(HealthCheckReport report)
    {
        var healthCheckConfig = GetHealthCheckConfiguration();
        
        // Check critical services
        var criticalServices = report.Results.Where(r => healthCheckConfig.CriticalServices.Contains(r.ServiceName, StringComparer.OrdinalIgnoreCase));
        var criticalFailures = criticalServices.Count(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable);

        if (criticalFailures > 0)
        {
            Log.Error("Application cannot start: {CriticalFailures} critical services are failing", criticalFailures);
            return false;
        }

        // Check overall failure rate
        var totalFailures = report.UnhealthyCount + report.UnavailableCount;
        var failureRate = (double)totalFailures / report.TotalCount;

        var threshold = healthCheckConfig.CriticalFailureRateThreshold <= 0 ? 0.5 : healthCheckConfig.CriticalFailureRateThreshold;
        if (failureRate > threshold)
        {
            Log.Error("Application cannot start: {FailureRate:P0} of services are failing (threshold {Threshold:P0})", failureRate, threshold);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a summary of the current application health status
    /// </summary>
    public static HealthStatusSummary GetHealthStatusSummary()
    {
        var report = LatestHealthReport;
        if (report == null)
        {
            return new HealthStatusSummary
            {
                OverallStatus = HealthStatus.Unavailable,
                StatusDescription = "Health checks have not been performed yet",
                LastChecked = null,
                ServiceCount = 0,
                HealthyCount = 0,
                Issues = new List<string> { "No health check data available" }
            };
        }

        var issues = new List<string>();
        foreach (var result in report.Results.Where(r => r.Status != HealthStatus.Healthy))
        {
            issues.Add($"{result.ServiceName}: {result.Description}");
        }

        return new HealthStatusSummary
        {
            OverallStatus = report.OverallStatus,
            StatusDescription = GetStatusDescription(report.OverallStatus),
            LastChecked = report.Timestamp,
            ServiceCount = report.TotalCount,
            HealthyCount = report.HealthyCount,
            Issues = issues
        };
    }

    private static string GetStatusDescription(HealthStatus status)
    {
        return status switch
        {
            HealthStatus.Healthy => "All services are operating normally",
            HealthStatus.Degraded => "Some services are experiencing issues but the application is functional",
            HealthStatus.Unhealthy => "Critical services are failing - application functionality may be limited",
            HealthStatus.Unavailable => "Health check system is unavailable",
            _ => "Unknown health status"
        };
    }

    private async Task CloseSplashScreenOnFailureAsync()
    {
        if (_splashScreen != null)
        {
            try
            {
                await _splashScreen.FadeOutAndCloseAsync();
                _splashScreen = null;
                Log.Information("Splash screen closed due to startup failure");
            }
            catch (Exception splashEx)
            {
                Log.Warning(splashEx, "Failed to close splash screen during startup failure");
                HideSplashScreen();
            }
        }
    }

    private void ShowFallbackUI(Exception ex)
    {
        try
        {
            // Themed fallback window (FluentDark-like) with copy diagnostics
            var fallbackWindow = new Window
            {
                Title = "Wiley Widget - Startup Error",
                Width = 520,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 34)),
                Foreground = Brushes.White,
                Content = BuildFallbackContent(ex)
            };
            fallbackWindow.Show();
        }
        catch
        {
            // If even fallback UI fails, show message box
            MessageBox.Show($"Application failed to start: {ex.Message}",
                           "Startup Error",
                           MessageBoxButton.OK,
                           MessageBoxImage.Error);
        }
    }

    private UIElement BuildFallbackContent(Exception ex)
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = "Application Startup Failed",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0,0,0,8),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Grid.SetRow(titleText, 0);

        var details = new TextBox
        {
            Text = $"Message: {ex.Message}{Environment.NewLine}Type: {ex.GetType().FullName}{Environment.NewLine}Stack Trace:{Environment.NewLine}{ex.StackTrace}",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = new SolidColorBrush(Color.FromRgb(45,45,50)),
            Foreground = Brushes.Gainsboro,
            BorderBrush = new SolidColorBrush(Color.FromRgb(70,70,78))
        };
        AutomationProperties.SetName(details, "Error details");
        Grid.SetRow(details, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0,8,0,0)
        };

        Button copyButton = new()
        {
            Content = "Copy Details",
            Margin = new Thickness(0,0,8,0),
            Padding = new Thickness(12,4,12,4)
        };
        copyButton.Click += (s, e) =>
        {
            try
            {
                Clipboard.SetText(details.Text);
            }
            catch { }
        };
        AutomationProperties.SetName(copyButton, "Copy error details to clipboard");

        Button closeButton = new()
        {
            Content = "Close",
            Padding = new Thickness(16,4,16,4)
        };
        closeButton.Click += (s, e) => Shutdown();
        AutomationProperties.SetName(closeButton, "Close application");

        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(closeButton);
        Grid.SetRow(buttonPanel, 2);

        grid.Children.Add(titleText);
        grid.Children.Add(details);
        grid.Children.Add(buttonPanel);
        return grid;
    }

    /// <summary>
    /// Resolves the Syncfusion license key from supported sources in priority order:
    /// 1) Environment variable SYNCFUSION_LICENSE_KEY
    /// 2) appsettings (Syncfusion:LicenseKey)
    /// 3) User Secrets (Syncfusion:LicenseKey)
    /// Returns null if not found or if placeholder value is detected.
    /// </summary>
    private string GetSyncfusionLicenseKey()
    {
        try
        {
            // 1) Environment variable (check all scopes: Process, User, Machine)
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
            {
                System.Diagnostics.Debug.WriteLine($"Syncfusion license found in environment variables (scope: Process/User/Machine)");
                return key.Trim();
            }

            // Explicitly check machine scope (where user said it's stored)
            key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
            {
                System.Diagnostics.Debug.WriteLine("Syncfusion license found in machine scope environment variable");
                return key.Trim();
            }

            // 2) Configuration - handled later via DI after host is built
            // License registration happens early, so rely on environment variables
            System.Diagnostics.Debug.WriteLine("Configuration access removed - relying on environment variables for early license registration");

            // 3) User secrets (development)
            try
            {
                var userSecretsConfig = new ConfigurationBuilder()
                    .AddUserSecrets<WileyWidget.App>()
                    .Build();
                key = userSecretsConfig["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                {
                    System.Diagnostics.Debug.WriteLine("Syncfusion license found in user secrets");
                    return key.Trim();
                }
            }
            catch
            {
                // ignore if user secrets unavailable
            }

            System.Diagnostics.Debug.WriteLine("Syncfusion license key not found in any source");
            // 4) Azure Key Vault (final fallback) if service already constructed
            try
            {
                if (ServiceProvider != null)
                {
                    using var scope = ServiceProvider.CreateScope();
                    var kv = scope.ServiceProvider.GetService<IAzureKeyVaultService>();
                    if (kv != null)
                    {
                        var config = GetConfiguration();
                        var secretName = config?["Syncfusion:KeyVaultSecretName"] ?? "Syncfusion-LicenseKey";
                        var kvValue = kv.GetSecretAsync(secretName).GetAwaiter().GetResult();
                        if (!string.IsNullOrWhiteSpace(kvValue) && kvValue != "${SYNCFUSION_LICENSE_KEY}")
                        {
                            System.Diagnostics.Debug.WriteLine("Syncfusion license retrieved from Azure Key Vault");
                            return kvValue.Trim();
                        }
                    }
                }
            }
            catch (Exception kvEx)
            {
                System.Diagnostics.Debug.WriteLine($"Azure Key Vault license retrieval failed: {kvEx.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resolving Syncfusion license key: {ex.Message}");
            // ignore resolution errors and fall through
        }

        return null;
    }

    /// <summary>
    /// Schedules automatic closure of Syncfusion license dialogs after a configurable timeout
    /// </summary>
    private void TryScheduleLicenseDialogAutoClose()
    {
        try
        {
            // Get configurable timeout from environment or use default (30 seconds)
            var timeoutSeconds = Environment.GetEnvironmentVariable("WILEYWIDGET_LICENSE_DIALOG_TIMEOUT_SECONDS");
            if (!int.TryParse(timeoutSeconds, out var timeout) || timeout <= 0)
            {
                timeout = 30; // Default 30 seconds
            }

            Log.Information("Scheduling license dialog auto-close after {TimeoutSeconds} seconds", timeout);

            // Use DispatcherTimer for UI thread safety
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(timeout)
            };

            timer.Tick += (sender, args) =>
            {
                try
                {
                    timer.Stop();
                    CloseLicenseDialogs();
                    Log.Information("License dialog auto-close timer executed");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to auto-close license dialogs");
                }
            };

            timer.Start();
            Log.Debug("License dialog auto-close timer started");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to schedule license dialog auto-close");
        }
    }

    /// <summary>
    /// Attempts to close any open Syncfusion license dialogs
    /// </summary>
    private void CloseLicenseDialogs()
    {
        try
        {
            // Find and close windows with titles containing "Syncfusion" or "License"
            var licenseDialogs = System.Windows.Application.Current.Windows
                .Cast<System.Windows.Window>()
                .Where(w => w.Title.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase) ||
                           w.Title.Contains("License", StringComparison.OrdinalIgnoreCase) ||
                           w.Title.Contains("Evaluation", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var dialog in licenseDialogs)
            {
                try
                {
                    Log.Information("Auto-closing license dialog: {Title}", dialog.Title);
                    dialog.Close();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to close license dialog: {Title}", dialog.Title);
                }
            }

            if (licenseDialogs.Any())
            {
                Log.Information("Auto-closed {Count} license dialogs", licenseDialogs.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error while attempting to close license dialogs");
        }
    }
}

internal interface IViewManager
{
    Task ShowSplashScreenAsync(CancellationToken none);
}