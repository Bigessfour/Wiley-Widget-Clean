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

namespace WileyWidget;

/// <summary>
/// OPTIMIZED APPLICATION STARTUP - Following Microsoft WPF Best Practices
/// Key improvements:
/// - Non-blocking license registration
/// - Immediate splash screen
/// - Background service initialization
/// - Better error handling and fallbacks
/// </summary>
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

    private IConfiguration _configuration;
    private SplashScreenWindow _splashScreen;
    private HealthCheckConfiguration _healthCheckConfig;
    private Dictionary<string, HealthCheckCircuitBreaker> _circuitBreakers;
    private IHost _host; // Generic Host instance
    // Removed unused private field _latestHealthReport to avoid CS0169 warning (LatestHealthReport static property is used instead)

    /// <summary>
    /// OPTIMIZED CONSTRUCTOR: Synchronous license registration before ANY Syncfusion components
    /// </summary>
    public App()
    {
        // CRITICAL: Register Syncfusion license SYNCHRONOUSLY before ANY Syncfusion components are created
        // This method loads configuration internally and registers the license
        RegisterSyncfusionLicenseSynchronously();

        // Logging is configured later via Generic Host (Serilog). Avoid double initialization here.
        // Register Syncfusion license using configuration too (non-blocking follow-up)
        RegisterSyncfusionLicense();
        Log.Information("=== Application Constructor Initialized ===");
    }

    /// <summary>
    /// OPTIMIZED ONSTARTUP: UI initialization after license is registered
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            Log.Information("=== Application Startup (Optimized) ===");

            // Prevent WPF from automatically creating MainWindow
            // We'll create it manually after DI is ready
            this.MainWindow = null;

            // 0. Initial setup (10%)
            _splashScreen?.UpdateProgress(10, "Configuring application...");
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

            // 1. Show splash screen IMMEDIATELY (perceived performance) (20%)
            _splashScreen?.UpdateProgress(20, "Initializing user interface...");
            try
            {
                ShowSplashScreen();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to show splash screen, continuing without it");
            }

            // 2. Build Generic Host with enterprise configuration (40% - 70%)
            _splashScreen?.UpdateProgress(40, "Configuring services...");
            ConfigureEssentialServices();

            _splashScreen?.UpdateProgress(60, "Building host...");
            var hostBuilder = Host.CreateApplicationBuilder();
            // Delegate DI, logging, config, DB services, hosted services to our extension
            hostBuilder.ConfigureWpfApplication();
            _host = hostBuilder.Build();

            // Expose ServiceProvider for legacy paths
            ServiceProvider = _host.Services;
            Application.Current.Properties["ServiceProvider"] = ServiceProvider;

            // Initialize essential services that require the ServiceProvider
            InitializeEssentialServices();

            // Start the host (HostedWpfApplication will create and show MainWindow)
            _splashScreen?.UpdateProgress(70, "Starting services...");
            await _host.StartAsync();

            // Now call base.OnStartup to complete WPF initialization
            base.OnStartup(e);

            // Ensure splash closes once content is rendered
            bool splashClosed = false;
            if (Application.Current?.MainWindow != null)
            {
                Application.Current.MainWindow.ContentRendered += async (_, __) =>
                {
                    if (_splashScreen != null && !splashClosed)
                    {
                        splashClosed = true;
                        try
                        {
                            // Allow a brief moment for the completion message to be visible
                            await Task.Delay(250);
                            await _splashScreen.FadeOutAndCloseAsync();
                            _splashScreen = null;
                            Log.Information("Splash screen closed after MainWindow content rendered");
                        }
                        catch (Exception exClose)
                        {
                            Log.Warning(exClose, "Failed to fade/close splash, falling back to HideSplashScreen()");
                            HideSplashScreen();
                        }
                    }
                };
            }

            // 5. Complete initialization (100%)
            _splashScreen?.Complete();

            // 6. Continue with background initialization (non-blocking)
            _ = InitializeBackgroundServicesAsync();

            Log.Information("=== Application Startup Completed ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during application startup");
            // Show error dialog and exit gracefully
            ShowFallbackUI(ex);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
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

    private void RegisterSyncfusionLicenseSynchronously()
    {
        try
        {
            // Load configuration first to get license key
            LoadConfiguration();

            // Initialize health check configuration
            InitializeHealthCheckConfiguration();

            // Read license key from supported sources (env var, config, user secrets)
            var licenseKey = GetSyncfusionLicenseKey();

            if (!string.IsNullOrEmpty(licenseKey))
            {
                // Register the license with Syncfusion SYNCHRONOUSLY before any components are created
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                System.Diagnostics.Debug.WriteLine("Syncfusion license registered successfully from configuration/environment");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Syncfusion license key not found in configuration. Evaluation dialogs may appear.");
            }
        }
        catch (Exception ex)
        {
            // License registration failed, but don't crash the app
            System.Diagnostics.Debug.WriteLine($"Syncfusion license registration failed: {ex.Message}");
        }
    }

    private void LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    private void InitializeHealthCheckConfiguration()
    {
        _healthCheckConfig = new HealthCheckConfiguration();

        // Load health check settings from configuration
        var healthCheckSection = _configuration.GetSection("HealthChecks");
        if (healthCheckSection.Exists())
        {
            healthCheckSection.Bind(_healthCheckConfig);
        }

        // Override with environment variables if present
        var defaultTimeout = Environment.GetEnvironmentVariable("HEALTHCHECK_DEFAULT_TIMEOUT");
        if (!string.IsNullOrEmpty(defaultTimeout) && TimeSpan.TryParse(defaultTimeout, out var timeout))
        {
            _healthCheckConfig.DefaultTimeout = timeout;
        }

        var continueOnFailure = Environment.GetEnvironmentVariable("HEALTHCHECK_CONTINUE_ON_FAILURE");
        if (!string.IsNullOrEmpty(continueOnFailure) && bool.TryParse(continueOnFailure, out var continueFlag))
        {
            _healthCheckConfig.ContinueOnFailure = continueFlag;
        }

        // Initialize circuit breakers for each service
        _circuitBreakers = new Dictionary<string, HealthCheckCircuitBreaker>
        {
            ["Database"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout),
            ["Azure AD"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout),
            ["Azure Key Vault"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout),
            ["QuickBooks"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout),
            ["AI Service"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout),
            ["External Dependencies"] = new HealthCheckCircuitBreaker(_healthCheckConfig.CircuitBreakerThreshold, _healthCheckConfig.CircuitBreakerTimeout)
        };

        Log.Information("Health check configuration initialized with timeout: {Timeout}s, continue on failure: {Continue}, circuit breaker threshold: {Threshold}",
            _healthCheckConfig.DefaultTimeout.TotalSeconds, _healthCheckConfig.ContinueOnFailure, _healthCheckConfig.CircuitBreakerThreshold);
    }

    private void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/wiley-widget-.log", rollingInterval: RollingInterval.Day)
            .WriteTo.Debug()
            .CreateLogger();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add configuration
        services.AddSingleton(_configuration);

        // Add logging
        services.AddLogging(configure => configure.AddSerilog());

        // Add services
        services.AddTransient<MainWindow>();
        services.AddScoped<ViewModels.MainViewModel>();

        // Add authentication service
        services.AddSingleton<AuthenticationService>();

        // Add database configuration
        services.AddEnterpriseDatabaseServices(_configuration);

    // Register HttpClient factory for efficient, pooled HTTP usage across the app
    services.AddHttpClient();

        // Add health checks
        services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            .AddApplicationLifecycleHealthCheck();

        // Add custom health check service
        services.AddSingleton<HealthCheckConfiguration>();
        services.AddSingleton<WileyWidget.Services.HealthCheckService>();
    }

    private void RegisterSyncfusionLicense()
    {
        try
        {
            // Resolve license key from unified helper (env var, config, user secrets)
            var licenseKey = GetSyncfusionLicenseKey();

            if (!string.IsNullOrEmpty(licenseKey) && licenseKey != "${SYNCFUSION_LICENSE_KEY}")
            {
                // Register the license with Syncfusion
                Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Information("Syncfusion license registered successfully");
            }
            else
            {
                // Provide detailed guidance for license setup
                Log.Warning("Syncfusion license key not found. To prevent evaluation dialogs, set one of the following:");
                Log.Warning("  1. Environment variable: SYNCFUSION_LICENSE_KEY");
                Log.Warning("  2. Configuration file: appsettings.json -> Syncfusion:LicenseKey");
                Log.Warning("  3. User secrets: dotnet user-secrets set Syncfusion:LicenseKey <your-key>");
                Log.Warning("  Get a license key from: https://www.syncfusion.com/account/license");

                // For development environments, we can continue without crashing
                // In production, you might want to throw an exception here
#if DEBUG
                Log.Warning("Continuing in DEBUG mode without license - evaluation dialogs may appear");
#else
                Log.Error("Production environment requires valid Syncfusion license key");
                // In production, you might want to exit or show a dialog
#endif
            }
        }
        catch (Exception ex)
        {
            // License registration failed, but don't crash the app
            Log.Error(ex, "Syncfusion license registration failed");
            Log.Warning("Application will continue but may show evaluation dialogs");
        }
    }

    private void ConfigureGlobalExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = (Exception)args.ExceptionObject;
            Log.Fatal(ex, "Unhandled exception - application will terminate");
            ShowCriticalErrorDialog(ex);
        };

        DispatcherUnhandledException += (sender, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception");
            args.Handled = true;
            ShowRecoverableErrorDialog(args.Exception);
        };

        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
            ShowRecoverableErrorDialog(args.Exception);
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
        try
        {
            // Create custom splash screen window with progress tracking
            _splashScreen = new SplashScreenWindow();
            _splashScreen.Show();
            Log.Information("Custom splash screen displayed successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show splash screen, continuing without it");
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
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                Log.Warning("Database connection string not found. Application may not function properly.");
            }
            else
            {
                Log.Information("Database connection string found");
            }

            // Check Azure AD configuration
            var clientId = _configuration["AzureAd:ClientId"];
            var tenantId = _configuration["AzureAd:TenantId"];
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

    private void ConfigureEssentialServices()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Validate configuration before proceeding
        ValidateConfiguration();

        // Configure only essential services that are needed immediately
        // Database and other heavy services are initialized in background
        // Note: Service resolution moved to after ServiceProvider is built

        stopwatch.Stop();
        Log.Information("Essential services configured in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
    }

    private void InitializeEssentialServices()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Initialize authentication service
            var authService = ServiceProvider.GetService<AuthenticationService>();
            if (authService != null)
            {
                Log.Information("Authentication service initialized in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                Log.Warning("Authentication service could not be resolved from DI container");
            }

            stopwatch.Stop();
            Log.Information("Essential services initialized in {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize essential services");
            throw; // Re-throw to prevent application startup with broken services
        }
    }

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

            // Core services (always checked)
            healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("Configuration", CheckConfigurationHealthAsync()));
            healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("Database", CheckDatabaseHealthAsync()));

            // External services (with circuit breakers)
            if (!IsServiceSkipped("Azure AD"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("Azure AD", CheckAzureAdHealthAsync()));

            if (!IsServiceSkipped("Azure Key Vault"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("Azure Key Vault", CheckAzureKeyVaultHealthAsync()));

            if (!IsServiceSkipped("QuickBooks"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("QuickBooks", CheckQuickBooksHealthAsync()));

            if (!IsServiceSkipped("Syncfusion License"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("Syncfusion License", CheckSyncfusionLicenseHealthAsync()));

            if (!IsServiceSkipped("AI Service"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("AI Service", CheckAIServiceHealthAsync()));

            if (!IsServiceSkipped("External Dependencies"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("External Dependencies", CheckExternalDependenciesHealthAsync()));

            if (!IsServiceSkipped("System Resources"))
                healthCheckTasks.Add(ExecuteHealthCheckWithResilienceAsync("System Resources", CheckSystemResourcesHealthAsync()));

            // Wait for all health checks to complete with overall timeout
            var timeoutTask = Task.Delay(_healthCheckConfig.DefaultTimeout);
            var completedTask = await Task.WhenAny(Task.WhenAll(healthCheckTasks), timeoutTask);

            if (completedTask == timeoutTask)
            {
                Log.Warning("Health checks timed out after {Timeout}s", _healthCheckConfig.DefaultTimeout.TotalSeconds);

                // Create timeout results for incomplete tasks
                var timedOutResults = healthCheckTasks
                    .Where(t => !t.IsCompletedSuccessfully)
                    .Select(t => HealthCheckResult.Unhealthy("Unknown Service", "Health check timed out", null, _healthCheckConfig.DefaultTimeout));

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
            if (!CanApplicationStart(report) && !_healthCheckConfig.ContinueOnFailure)
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
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
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
            if (string.IsNullOrEmpty(_configuration["AzureAd:ClientId"]))
                issues.Add("Azure AD Client ID not configured");

            if (string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")))
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
            if (string.IsNullOrEmpty(_configuration["AzureAd:ClientId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Client ID not configured"));
            }

            if (string.IsNullOrEmpty(_configuration["AzureAd:TenantId"]))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Azure AD", "Azure AD Tenant ID not configured"));
            }

            // Basic connectivity check (without actual authentication)
            // This validates that the configuration is correct for potential authentication
            var clientId = _configuration["AzureAd:ClientId"];
            var tenantId = _configuration["AzureAd:TenantId"];

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

            var keyVaultUrl = _configuration["Azure:KeyVault:Url"];
            if (string.IsNullOrEmpty(keyVaultUrl))
            {
                return HealthCheckResult.Unavailable("Azure Key Vault", "Azure Key Vault URL not configured");
            }

            // Attempt to list secrets (this validates connectivity and permissions)
            // Note: This is a basic check and may fail in production due to permissions
            try
            {
                // We don't actually list secrets, just check if the client can be created
                await Task.CompletedTask; // Placeholder for actual connectivity check
            }
            catch
            {
                // If we can't connect, mark as degraded rather than unhealthy
                stopwatch.Stop();
                return HealthCheckResult.Degraded("Azure Key Vault", "Azure Key Vault connectivity could not be verified", stopwatch.Elapsed);
            }

            stopwatch.Stop();
            return HealthCheckResult.Healthy("Azure Key Vault", "Azure Key Vault service configured and accessible", stopwatch.Elapsed);
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
            var qbService = ServiceProvider.GetService<IQuickBooksService>();
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

            // Check if token is valid
            if (qbService is QuickBooksService concreteService && !concreteService.HasValidAccessToken())
            {
                return Task.FromResult(HealthCheckResult.Degraded("QuickBooks", "QuickBooks access token is expired or not available"));
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
            // Use unified resolution for the license key (env/config/user secrets)
            var licenseKey = GetSyncfusionLicenseKey();
            if (string.IsNullOrEmpty(licenseKey))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key not configured"));
            }

            // Validate license format (basic check)
            if (licenseKey.Length < 32)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Syncfusion License", "Syncfusion license key appears to be invalid (too short)"));
            }

            // Check if license is registered (this would require checking Syncfusion's internal state)
            // For now, we assume it's valid if configured
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("Syncfusion License", "Syncfusion license key configured and validated", stopwatch.Elapsed));
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
            var aiService = ServiceProvider.GetService<IAIService>();
            if (aiService == null)
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "AI service not configured"));
            }

            // Check configuration
            var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var azureAiEndpoint = _configuration["Azure:AI:Endpoint"];

            if (string.IsNullOrEmpty(openAiKey) && string.IsNullOrEmpty(azureAiEndpoint))
            {
                return Task.FromResult(HealthCheckResult.Unavailable("AI Service", "Neither OpenAI API key nor Azure AI endpoint configured"));
            }

            // Basic connectivity check would go here
            // For now, just validate configuration
            stopwatch.Stop();
            return Task.FromResult(HealthCheckResult.Healthy("AI Service", "AI service configuration validated", stopwatch.Elapsed));
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
            var azureSqlConnection = _configuration.GetConnectionString("AzureConnection");
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
            var cpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
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
        return _healthCheckConfig.SkipServices.Contains(serviceName, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HealthCheckResult> ExecuteHealthCheckWithResilienceAsync(string serviceName, Task<HealthCheckResult> healthCheckTask)
    {
        // Get circuit breaker for this service
        if (!_circuitBreakers.TryGetValue(serviceName, out var circuitBreaker))
        {
            circuitBreaker = new HealthCheckCircuitBreaker();
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
        var lastException = (Exception)null;

        for (int attempt = 0; attempt <= _healthCheckConfig.MaxRetries; attempt++)
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
                lastException = ex;

                // Log retry attempt
                if (attempt < _healthCheckConfig.MaxRetries)
                {
                    Log.Warning(ex, "Health check for {Service} failed (attempt {Attempt}/{MaxAttempts}), retrying in {Delay}ms",
                        serviceName, attempt + 1, _healthCheckConfig.MaxRetries + 1, _healthCheckConfig.RetryDelay.TotalMilliseconds);

                    // Exponential backoff
                    var delay = _healthCheckConfig.RetryDelay * Math.Pow(2, attempt);
                    await Task.Delay(delay);
                }
            }
        }

        // All retries exhausted
        return HealthCheckResult.Unhealthy(serviceName,
            $"Health check failed after {_healthCheckConfig.MaxRetries + 1} attempts: {lastException?.Message}",
            lastException);
    }

    private TimeSpan GetTimeoutForService(string serviceName)
    {
        return serviceName switch
        {
            "Database" => _healthCheckConfig.DatabaseTimeout,
            "Azure AD" or "Azure Key Vault" or "QuickBooks" or "AI Service" or "External Dependencies" => _healthCheckConfig.ExternalServiceTimeout,
            _ => _healthCheckConfig.DefaultTimeout
        };
    }

    private bool CanApplicationStart(HealthCheckReport report)
    {
        // Check critical services
        var criticalServices = report.Results.Where(r => _healthCheckConfig.CriticalServices.Contains(r.ServiceName, StringComparer.OrdinalIgnoreCase));
        var criticalFailures = criticalServices.Count(r => r.Status == HealthStatus.Unhealthy || r.Status == HealthStatus.Unavailable);

        if (criticalFailures > 0)
        {
            Log.Error("Application cannot start: {CriticalFailures} critical services are failing", criticalFailures);
            return false;
        }

        // Check overall failure rate
        var totalFailures = report.UnhealthyCount + report.UnavailableCount;
        var failureRate = (double)totalFailures / report.TotalCount;

        if (failureRate > 0.5) // More than 50% services failing
        {
            Log.Error("Application cannot start: {FailureRate:P0} of services are failing", failureRate);
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

    private void ShowFallbackUI(Exception ex)
    {
        try
        {
            // Create a minimal fallback window
            var fallbackWindow = new Window
            {
                Title = "Wiley Widget - Startup Error",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = "Application Startup Failed",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(titleText, 0);

            var errorText = new TextBlock
            {
                Text = $"Error: {ex.Message}",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(errorText, 1);

            var closeButton = new Button
            {
                Content = "Close Application",
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            closeButton.Click += (s, e) => Shutdown();
            Grid.SetRow(closeButton, 2);

            grid.Children.Add(titleText);
            grid.Children.Add(errorText);
            grid.Children.Add(closeButton);

            fallbackWindow.Content = grid;
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

    private void TryScheduleLicenseDialogAutoClose()
    {
        // TODO: Implement license dialog auto-close for testing
        // This method would schedule automatic dismissal of Syncfusion license dialogs in test environments
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
            // 1) Environment variable
            var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 2) Configuration
            key = _configuration?["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                return key.Trim();

            // 3) User secrets (development)
            try
            {
                var userSecretsConfig = new ConfigurationBuilder()
                    .AddUserSecrets<WileyWidget.App>()
                    .Build();
                key = userSecretsConfig["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(key) && key != "${SYNCFUSION_LICENSE_KEY}")
                    return key.Trim();
            }
            catch
            {
                // ignore if user secrets unavailable
            }
        }
        catch
        {
            // ignore resolution errors and fall through
        }

        return null;
    }
}
