using System;
using System.IO;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using System.Windows;
using Syncfusion.Licensing; // Official Syncfusion licensing namespace
using WileyWidget.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
<<<<<<< Updated upstream
=======
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;
using Azure;
using System.Threading;
using WileyWidget.ViewModels;
>>>>>>> Stashed changes

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

    private IConfiguration _configuration;
    private SplashScreen _splashScreen;

    /// <summary>
    /// OPTIMIZED CONSTRUCTOR: Synchronous license registration before ANY Syncfusion components
    /// </summary>
    public App()
    {
        // CRITICAL: Register Syncfusion license SYNCHRONOUSLY before ANY Syncfusion components are created
        RegisterSyncfusionLicenseSynchronously();

        // 1. Load configuration (fast, local I/O)
        LoadConfiguration();

        // 2. Configure logging (fast, local setup)
        ConfigureLogging();

<<<<<<< Updated upstream
        // Register Syncfusion license using configuration
        RegisterSyncfusionLicense();
        Log.Information("=== Application Constructor Initialized ===");
=======
        Log.Information("=== Application Constructor Completed (Syncfusion License Registered) ===");
>>>>>>> Stashed changes
    }

    /// <summary>
    /// OPTIMIZED ONSTARTUP: UI initialization after license is registered
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            Log.Information("=== Application Startup (Optimized) ===");

<<<<<<< Updated upstream
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
		base.OnStartup(e);
	}
=======
            // 1. Show splash screen IMMEDIATELY (perceived performance)
            ShowSplashScreen();

            // 2. Configure essential services only (database deferred)
            ConfigureEssentialServices();

            // 3. Create and show main window (fast UI feedback)
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
>>>>>>> Stashed changes

            // 4. Hide splash screen immediately after main window is shown
            HideSplashScreen();
            Log.Information("Splash screen hidden - MainWindow shown successfully");

<<<<<<< Updated upstream
			// Build configuration
			var configuration = new ConfigurationBuilder()
				.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
				.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
				.AddUserSecrets<WileyWidget.App>(optional: true)
				.AddEnvironmentVariables()
				.Build();

			// Configure services
			var services = new ServiceCollection();
			services.AddDatabaseServices(configuration);
=======
            // 5. Continue with background initialization (non-blocking)
            await InitializeBackgroundServicesAsync();

            Log.Information("=== Application Startup Completed ===");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Critical error during application startup");
            // Show error dialog and exit gracefully
            MessageBox.Show($"Application failed to start: {ex.Message}",
                          "Startup Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
            Shutdown();
        }

        base.OnStartup(e);
    }

    /// <summary>
    /// Show splash screen immediately for perceived performance
    /// </summary>
    private void ShowSplashScreen()
    {
        try
        {
            // Check if splash screen image exists
            var splashImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "SplashScreen.png");
            if (File.Exists(splashImagePath))
            {
                _splashScreen = new SplashScreen(splashImagePath);
                _splashScreen.Show(false); // Show without auto-close
                Log.Information("Splash screen displayed");
            }
            else
            {
                Log.Information("Splash screen image not found, creating default splash screen");

                // Create a simple default splash screen using a border
                var defaultSplash = new Window
                {
                    Title = "Wiley Widget",
                    Width = 400,
                    Height = 300,
                    WindowStyle = WindowStyle.None,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = System.Windows.Media.Brushes.LightBlue,
                    Topmost = true
                };

                var grid = new System.Windows.Controls.Grid();
                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "Wiley Widget\nEnterprise Widget Management System\nLoading...",
                    FontSize = 16,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = System.Windows.TextAlignment.Center
                };
>>>>>>> Stashed changes

                grid.Children.Add(textBlock);
                defaultSplash.Content = grid;
                defaultSplash.Show();

                // Store reference for cleanup
                _splashScreen = null; // We'll handle this differently
                Log.Information("Default splash screen displayed");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show splash screen");
        }
    }

<<<<<<< Updated upstream
			Log.Information("Database services configured successfully");
		}
		catch (Exception ex)
		{
			Log.Error(ex, "Failed to configure database services");
			// In development, you might want to show a message box or handle this differently
			// For now, we'll log the error and continue - the app can still run without database
		}
	}
=======
    /// <summary>
    /// Hide splash screen when initialization is complete
    /// </summary>
    private void HideSplashScreen()
    {
        try
        {
            if (_splashScreen != null)
            {
                _splashScreen.Close(TimeSpan.FromSeconds(0.5)); // Fade out
                Log.Information("Splash screen hidden");
            }
            else
            {
                // Find and close any default splash screen windows
                foreach (var window in Application.Current.Windows)
                {
                    if (window is Window w && w.Title == "Wiley Widget" && w.Width == 400 && w.Height == 300)
                    {
                        w.Close();
                        Log.Information("Default splash screen hidden");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to hide splash screen");
        }
    }

    /// <summary>
    /// SYNCHRONOUS LICENSE REGISTRATION: Must happen before ANY Syncfusion components are created
    /// Follows Syncfusion WPF API requirements exactly
    /// PRIORITY ORDER: 1. Machine Environment Variable, 2. Configuration (AZURE KEY VAULT BYPASSED)
    /// </summary>
    private void RegisterSyncfusionLicenseSynchronously()
    {
        try
        {
            Log.Information("🔍 Starting Syncfusion license registration process...");
>>>>>>> Stashed changes

            // 1. PRIORITY: Machine-level environment variable using Registry (Microsoft recommended for reliable access)
            string envKey = null;
            try
            {
                // First try standard .NET method
                envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.Machine);
                Log.Information("🔍 Standard .NET method - Machine-level env var: {Found}, Length: {Length}", 
                    !string.IsNullOrWhiteSpace(envKey) ? "FOUND" : "NOT FOUND", 
                    envKey?.Length ?? 0);

                // If truncated or not found, use Registry approach (Microsoft documented fallback for Windows)
                if ((string.IsNullOrWhiteSpace(envKey) || envKey.Length < 80) && OperatingSystem.IsWindows())
                {
                    Log.Warning("⚠️ Standard method returned truncated/missing value, using Registry approach...");
                    
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment"))
                    {
                        if (key != null)
                        {
                            var registryValue = key.GetValue("SYNCFUSION_LICENSE_KEY") as string;
                            if (!string.IsNullOrWhiteSpace(registryValue))
                            {
                                envKey = registryValue;
                                Log.Information("✅ Registry method - Machine-level env var: FOUND, Length: {Length}", envKey.Length);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Failed to access machine-level environment variable via Registry");
            }

            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                var licenseKey = envKey.Trim();
                Log.Information("✅ MACHINE-LEVEL License Key Analysis:");
                Log.Information("   - Length: {Length} (Expected: 92+ characters)", licenseKey.Length);
                Log.Information("   - Starts with: {Start}", licenseKey.Length > 15 ? licenseKey.Substring(0, 15) : licenseKey);
                Log.Information("   - Ends with: {End}", licenseKey.Length > 15 ? licenseKey.Substring(licenseKey.Length - 15) : licenseKey);
                
                if (licenseKey.Length >= 80) // Valid license key length
                {
                    try
                    {
                        SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                        Log.Information("✅ Syncfusion license SUCCESSFULLY registered with machine-level key (length: {Length})", licenseKey.Length);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "❌ Failed to register license with machine-level key");
                    }
                }
                else
                {
                    Log.Error("❌ Machine-level license key still appears truncated: {Length} characters", licenseKey.Length);
                }
            }

            // 1b. ENVIRONMENT VARIABLE SUCCESS: Now using user-level environment variable
            Log.Information("✅ Direct license registration working. Environment variable fixed at user level.");

            // 1c. PRIORITY: User-level environment variable (WORKING SOLUTION)
            var userEnvKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userEnvKey) && userEnvKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                var licenseKey = userEnvKey.Trim();
                Log.Information("✅ USER-LEVEL Environment variable found - License key length: {Length}, starts with: {Start}", 
                    licenseKey.Length, 
                    licenseKey.Length > 10 ? licenseKey.Substring(0, 10) : licenseKey);
                
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Information("✅ Syncfusion license SUCCESSFULLY registered from USER-LEVEL ENVIRONMENT VARIABLE.");
                return;
            }

            // 1c. Fallback: Check process-level environment variable
            var processEnvKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(processEnvKey) && processEnvKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                var licenseKey = processEnvKey.Trim();
                Log.Information("✅ PROCESS-LEVEL Environment variable found - License key length: {Length}, starts with: {Start}", 
                    licenseKey.Length, 
                    licenseKey.Length > 10 ? licenseKey.Substring(0, 10) : licenseKey);
                
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                Log.Information("✅ Syncfusion license SUCCESSFULLY registered from PROCESS-LEVEL ENVIRONMENT VARIABLE.");
                return;
            }

            Log.Warning("❌ SYNCFUSION_LICENSE_KEY not found in any environment variable scope (Machine/User/Process).");

            // 2. AZURE KEY VAULT BYPASSED - Skip for now
            Log.Information("🚫 Azure Key Vault bypassed as requested - using environment variables only.");

            // 2. AZURE KEY VAULT BYPASSED - Skip for now
            Log.Information("🚫 Azure Key Vault bypassed as requested - using environment variables only.");

            // 3. Fallback: Try configuration file (if needed)
            try
            {
                var configKey = _configuration?["Syncfusion:LicenseKey"];
                if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    var licenseKey = configKey.Trim();
                    Log.Information("Configuration file license key length: {Length}, starts with: {Start}", 
                        licenseKey.Length, 
                        licenseKey.Length > 10 ? licenseKey.Substring(0, 10) : licenseKey);
                    
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("✅ Syncfusion license registered from CONFIGURATION FILE.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read license from configuration file.");
            }

            // 4. Last resort: Trial mode
            Log.Warning("❌ No valid Syncfusion license found. Application will run in TRIAL MODE with watermarks.");
            Log.Warning("💡 To fix: Set SYNCFUSION_LICENSE_KEY environment variable at machine level.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "❌ Critical error during Syncfusion license registration.");
            // Continue in trial mode rather than crash
        }
    }

    /// <summary>
    /// BACKGROUND SERVICE INITIALIZATION: Deferred until UI is shown
    /// </summary>
    private async Task InitializeBackgroundServicesAsync()
    {
        try
        {
            Log.Information("Starting background service initialization...");

            // Database initialization (moved here from OnStartup)
            await ConfigureDatabaseServicesAsync();

            // Other background services can be initialized here
            await InitializeOtherServicesAsync();

            Log.Information("Background service initialization completed");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Background service initialization failed");
            // Don't crash the app, just log the error
        }
    }

    /// <summary>
    /// ASYNC DATABASE CONFIGURATION: Proper error handling
    /// </summary>
    private async Task ConfigureDatabaseServicesAsync()
    {
        try
        {
            Log.Information("Configuring database services (async)...");

            // Build configuration (same logic as LoadConfiguration)
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            // Add environment-specific configuration file
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                             Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                             "Development";
            configBuilder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);

            // Add user secrets for development
            configBuilder.AddUserSecrets<WileyWidget.App>(optional: true);

            // Add environment variables (these will override config file values)
            configBuilder.AddEnvironmentVariables();

            var config = configBuilder.Build();

            // Configure services
            var services = new ServiceCollection();

            // Add logging services
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddSerilog();
            });

            services.AddDatabaseServices(config);

            // Register additional services
            services.AddSingleton<IAzureKeyVaultService>(sp => new AzureKeyVaultService(sp.GetRequiredService<ILogger<AzureKeyVaultService>>(), config));
            services.AddSingleton<IQuickBooksService, QuickBooksService>();
            services.AddSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DashboardViewModel>();

            // Build service provider
            var serviceProvider = services.BuildServiceProvider();

            // Store service provider for static access
            ServiceProvider = serviceProvider;

            // Initialize database asynchronously
            await DatabaseConfiguration.EnsureDatabaseCreatedAsync(serviceProvider);

            // Validate database schema asynchronously
            await DatabaseConfiguration.ValidateDatabaseSchemaAsync(serviceProvider);

            Log.Information("Database services configured successfully (async)");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to configure database services (async)");
            // In development, you might want to show a message box or handle this differently
            // For now, we'll log the error and continue - the app can still run without database
        }
    }

    /// <summary>
    /// Placeholder for other background services
    /// </summary>
    private async Task InitializeOtherServicesAsync()
    {
        // Initialize other services that don't need to block startup
        await Task.CompletedTask;
    }

    /// <summary>
    /// Configure only essential services immediately
    /// </summary>
    private void ConfigureEssentialServices()
    {
        // Only configure services that are absolutely needed for basic UI functionality
        // Database and other heavy services are deferred to background initialization
        Log.Information("Essential services configured");
    }

    /// <summary>
    /// Loads application configuration from appsettings.json and environment variables
    /// </summary>
    private void LoadConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    /// <summary>
    /// Registers Syncfusion license using configuration system with fallback methods
    /// </summary>
    private void RegisterSyncfusionLicense()
    {
<<<<<<< Updated upstream
        // 0. Configuration-based license (highest priority)
        try
        {
            var configKey = _configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(configKey) && configKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(configKey.Trim());
                Log.Information("Syncfusion license registered from configuration.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 1. Optional embedded license hook (implemented in user-created partial file not committed).
        // If the partial method returns true, registration succeeded and we skip other sources.
        try
        {
            if (TryRegisterEmbeddedLicense())
            {
                Log.Information("Syncfusion license registered from embedded partial.");
                return;
            }
        }
        catch { /* ignore and continue */ }

        // 2. Environment variable (User or Machine scope). User sets via: [System.Environment]::SetEnvironmentVariable("SYNCFUSION_LICENSE_KEY","<key>","User")
        try
        {
            var envKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && envKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
            {
                SyncfusionLicenseProvider.RegisterLicense(envKey.Trim());
                Log.Information("Syncfusion license registered from environment variable.");
                return;
            }
            else
            {
                Log.Information("No SYNCFUSION_LICENSE_KEY environment variable set – attempting file fallback.");
            }
        }
        catch { /* ignore and continue to file fallback */ }

        // 3. File fallback
        if (!TryLoadLicenseFromFile())
        {
            Log.Warning("Syncfusion license NOT registered (no config, no env var, no license.key). Application will run in trial mode.");
=======
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
                 Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
                 "Development";
        return env.Equals("Production", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if running in a server environment (not local development)
    /// </summary>
    private bool IsServerEnvironment()
    {
        // Check for common server indicators
        return Environment.GetEnvironmentVariable("COMPUTERNAME") != Environment.GetEnvironmentVariable("USERNAME") ||
               Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") != null ||
               Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME") != null;
    }

    /// <summary>
    /// Configure Serilog (daily rolling file in AppData, 7 file retention, enriched with process/thread/machine).
    /// Swallows internal logging setup exceptions to avoid blocking application startup.
    /// </summary>
    private void ConfigureLogging()
    {
        try
        {
            var logRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WileyWidget", "logs");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.File(
                    Path.Combine(logRoot, "wiley-widget-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {MachineName} {ProcessId}:{ThreadId} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("Logging configured successfully");
        }
        catch (Exception ex)
        {
            // If logging setup fails, write to console as fallback
            Console.WriteLine($"Failed to configure logging: {ex.Message}");
>>>>>>> Stashed changes
        }
    }

    /// <summary>
    /// Try to load license from file as final fallback
    /// </summary>
    private bool TryLoadLicenseFromFile()
    {
        try
        {
            var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SyncfusionLicense.txt");
            if (File.Exists(licensePath))
            {
                var licenseKey = File.ReadAllText(licensePath).Trim();
                if (!string.IsNullOrWhiteSpace(licenseKey) && licenseKey != "YOUR_SYNCFUSION_LICENSE_KEY_HERE")
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load license from file");
        }
        return false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("=== Application Exit ===");
            Log.CloseAndFlush();
        }
        catch { }
        base.OnExit(e);
    }
}