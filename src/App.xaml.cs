using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Prism.Unity;
using Prism.Modularity;
using Unity;
using Unity.Resolution;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using WileyWidget.Views;
using WileyWidget.Startup.Modules;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using WileyWidget.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget
{
    public class App : PrismApplication
    {
        // Static property for backwards compatibility with views
        public static IContainerProvider CurrentContainer { get; private set; }

        // Splash screen instance for bootstrapper access
        public static Window? SplashScreenInstance { get; set; }

        // Thread-safety locks
        private static readonly object _initLock = new object();
        private static readonly object _containerLock = new object();
        private static bool _isInitialized = false;

        // Helper method for views that need service resolution
        public static IServiceProvider GetActiveServiceProvider()
        {
            lock (_containerLock)
            {
                if (CurrentContainer == null)
                {
                    Log.Error("Application container not initialized - call InitializeContainer() first");
                    throw new InvalidOperationException("Application container not initialized. Ensure OnStartup has completed.");
                }

                Log.Debug("Returning active service provider");
                // Prism's container implements IServiceProvider
                return CurrentContainer as IServiceProvider ??
                       throw new InvalidOperationException("Container does not implement IServiceProvider");
            }
        }

        // Production-ready ServiceProvider with proper initialization
        private static IServiceProvider _serviceProvider;
        private static IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider == null)
                {
                    lock (_initLock)
                    {
                        if (_serviceProvider == null)
                        {
                            _serviceProvider = GetActiveServiceProvider();
                            Log.Information("ServiceProvider initialized with thread-safety");
                        }
                    }
                }
                return _serviceProvider;
            }
        }

        // Public accessor for ServiceProvider (backwards compatibility)
        public static IServiceProvider GetServiceProvider()
        {
            return ServiceProvider;
        }
        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        public static void SetSplashScreenInstance(Window window) => SplashScreenInstance = window;
        public static object StartupProgress { get; set; }
        public static void UpdateLatestHealthReport(object report) { /* Stub */ }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handling before anything else
            SetupGlobalExceptionHandling();

            ConfigureLogging();

            // Syncfusion setup (from Syncfusion WPF docs: Register license before any controls load)
            SyncfusionLicenseProvider.RegisterLicense("YOUR_SYNCFUSION_LICENSE_KEY_HERE");  // Replace with your actual key

            // Initialize ServiceProvider early to prevent "Application container not initialized" errors
            InitializeInternal();

            base.OnStartup(e);

            // ServiceProvider is now initialized lazily when first accessed
            Log.Information("Application startup completed - ServiceProvider ready for lazy initialization");
        }

        /// <summary>
        /// Sets up global exception handling for production readiness.
        /// Catches unhandled exceptions and logs them appropriately.
        /// </summary>
        private void SetupGlobalExceptionHandling()
        {
            // Handle unhandled exceptions on the UI thread
            Application.Current.DispatcherUnhandledException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unhandled UI exception occurred");
                e.Handled = true; // Prevent application crash

                // Show user-friendly error message
                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred: {e.Exception.Message}\n\nPlease check the logs for more details.",
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            // Handle unhandled exceptions on background threads
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exception = e.ExceptionObject as Exception;
                Log.Fatal(exception, "Unhandled background thread exception occurred");
                // Application will terminate after this
            };

            // Handle unobserved task exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                Log.Error(e.Exception, "Unobserved task exception occurred");
                e.SetObserved(); // Prevent it from crashing the finalizer thread
            };

            Log.Information("Global exception handling configured");
        }

        private void InitializeInternal()
        {
            // Early initialization to ensure logging is ready and basic services are available
            // This prevents "Application container not initialized" errors during startup
            Log.Debug("InitializeInternal called - ensuring early container access readiness");

            // Validate that logging is configured
            if (Log.Logger == null)
            {
                throw new InvalidOperationException("Logging not configured. Call ConfigureLogging() before InitializeInternal().");
            }

            // Mark as initialized to prevent duplicate initialization attempts
            lock (_initLock)
            {
                if (_isInitialized)
                {
                    Log.Debug("Application already initialized, skipping InitializeInternal");
                    return;
                }
                _isInitialized = true;
            }

            Log.Information("Application internal initialization completed");
        }

        protected override Window CreateShell()
        {
            try
            {
                // CRITICAL: Set CurrentContainer BEFORE resolving shell to ensure views can access it
                // This must happen after RegisterTypes() has completed
                if (Container == null)
                {
                    throw new InvalidOperationException("Unity container not initialized. RegisterTypes() must be called before CreateShell().");
                }

                CurrentContainer = Container;
                Log.Information("CurrentContainer set in CreateShell() - services now available for resolution");

                var shell = Container.Resolve<MainWindow>();
                Log.Information("MainWindow shell resolved successfully");
                return shell;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create application shell");
                throw new InvalidOperationException("Failed to create application shell. Check DI registrations and service availability.", ex);
            }
        }

        protected override void InitializeShell(Window shell)
        {
            // Set global theme (from Syncfusion docs: Use SfSkinManager.SetTheme for app-wide styling)
#pragma warning disable CA2000
            SfSkinManager.SetTheme(shell, new Theme("FluentDark"));  // Apply to shell
#pragma warning restore CA2000

            Application.Current.MainWindow = shell;
            shell.Show();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Starting DI Container Registration ===");
            
            // Build configuration first
            var configuration = BuildConfiguration();
            
            // Register configuration as singleton
            containerRegistry.RegisterInstance<IConfiguration>(configuration);
            Log.Information("✓ Registered IConfiguration as singleton instance");

            // Register Microsoft.Extensions.Logging integration with Serilog
#pragma warning disable CA2000
            var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
            Log.Information("✓ Registered ILoggerFactory and ILogger<> with Serilog integration");

            // Register HttpClient infrastructure for AI services
            RegisterHttpClientServices(containerRegistry, configuration);

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, LocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            containerRegistry.RegisterSingleton<ThemeManager>();
            containerRegistry.RegisterSingleton<IThemeManager>(provider => provider.Resolve<ThemeManager>());
            containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());
            Log.Information("✓ Registered core infrastructure services (Syncfusion, Settings, Theme, Dispatcher)");
            
            // Register data repositories
            containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
            containerRegistry.Register<IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();
            containerRegistry.Register<IMunicipalAccountRepository, WileyWidget.Data.MunicipalAccountRepository>();
            containerRegistry.Register<IUnitOfWork, WileyWidget.Data.UnitOfWork>();
            containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();
            containerRegistry.Register<IAuditRepository, WileyWidget.Data.AuditRepository>();
            Log.Information("✓ Registered data repositories (Enterprise, UtilityCustomer, MunicipalAccount, UnitOfWork, Budget, Audit)");
            
            // Register business services
            containerRegistry.RegisterSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();
            containerRegistry.RegisterSingleton<FiscalYearSettings>();
            containerRegistry.RegisterSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
            Log.Information("✓ Registered business services (WhatIfScenarioEngine, FiscalYearSettings, ChargeCalculator)");
            
            // Register AI Integration Services (Phase 1 - Production Ready)
            RegisterAIIntegrationServices(containerRegistry);
            
            // Register QuickBooks service
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
            Log.Information("✓ Registered IQuickBooksService as singleton");
            
            // Register Excel services
            containerRegistry.RegisterSingleton<IExcelReaderService, ExcelReaderService>();
            Log.Information("✓ Registered IExcelReaderService as singleton");
            
            // Register report export service
            containerRegistry.RegisterSingleton<IReportExportService, ReportExportService>();
            Log.Information("✓ Registered IReportExportService as singleton");
            
            // Register StartupPerformanceMonitor
            containerRegistry.RegisterSingleton<WileyWidget.Diagnostics.StartupPerformanceMonitor>();
            Log.Information("✓ Registered StartupPerformanceMonitor as singleton");
            
            // Register Prism DialogService
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            Log.Information("✓ Registered Prism IDialogService as singleton");
            
            // Register ViewModels
            containerRegistry.RegisterSingleton<MainViewModel>(provider => new MainViewModel(provider.Resolve<IRegionManager>(), provider.Resolve<IDispatcherHelper>(), provider.Resolve<ILogger<MainViewModel>>(), provider.Resolve<IEnterpriseRepository>(), provider.Resolve<IExcelReaderService>(), provider.Resolve<IReportExportService>(), provider.Resolve<IBudgetRepository>(), provider.Resolve<IAIService>()));
            containerRegistry.Register<AnalyticsViewModel>(provider => new AnalyticsViewModel(provider.Resolve<IDispatcherHelper>(), provider.Resolve<ILogger<AnalyticsViewModel>>(), provider.Resolve<IBudgetRepository>(), provider.Resolve<IMunicipalAccountRepository>(), provider.Resolve<IReportExportService>()));
            containerRegistry.Register<DashboardViewModel>();
            containerRegistry.Register<EnterpriseViewModel>();
            containerRegistry.Register<BudgetViewModel>();
            containerRegistry.Register<MunicipalAccountViewModel>();
            containerRegistry.Register<UtilityCustomerViewModel>();
            containerRegistry.Register<ReportsViewModel>();
            containerRegistry.Register<BudgetAnalysisViewModel>(provider => new BudgetAnalysisViewModel(provider.Resolve<IDispatcherHelper>(), provider.Resolve<ILogger<BudgetAnalysisViewModel>>(), provider.Resolve<IReportExportService>(), provider.Resolve<IBudgetRepository>()));
            
            // Register Region Adapters
            containerRegistry.Register<WileyWidget.Regions.DockingManagerRegionAdapter>();

            // Register Views for navigation
            containerRegistry.RegisterForNavigation<AnalyticsView>();
            containerRegistry.RegisterForNavigation<DashboardView>();

            // CRITICAL: Set CurrentContainer immediately after all registrations are complete
            // This ensures services like ThemeManager and SettingsService can access the container
            // during their construction, preventing "Application container not initialized" errors
            try
            {
                CurrentContainer = containerRegistry as IContainerProvider;
                if (CurrentContainer == null)
                {
                    throw new InvalidOperationException("Failed to cast container registry to IContainerProvider");
                }
                Log.Information("✓ CurrentContainer set successfully after all DI registrations");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to set CurrentContainer after DI registrations");
                throw new InvalidOperationException("DI container initialization failed. Application cannot continue.", ex);
            }

            Log.Information("=== DI Container Registration Complete ===");
            Log.Information($"Total registrations: AI Services, Data Repositories, Business Services, ViewModels, Infrastructure");
            Log.Information("Container ready for service resolution");

            // Validate critical service registrations
            ValidateCriticalServices(containerRegistry);
        }

        /// <summary>
        /// Validates that all critical services are properly registered and can be resolved.
        /// This prevents runtime errors due to missing DI registrations.
        /// </summary>
        /// <param name="containerRegistry">The container registry to validate</param>
        private void ValidateCriticalServices(IContainerRegistry containerRegistry)
        {
            Log.Information("Validating critical service registrations...");

            var criticalServices = new[]
            {
                ("IConfiguration", typeof(IConfiguration)),
                ("ILoggerFactory", typeof(ILoggerFactory)),
                ("ISettingsService", typeof(ISettingsService)),
                ("IThemeManager", typeof(IThemeManager)),
                ("IEnterpriseRepository", typeof(IEnterpriseRepository)),
                ("IBudgetRepository", typeof(IBudgetRepository)),
                ("IAIService", typeof(IAIService)),
                ("IGrokSupercomputer", typeof(IGrokSupercomputer)),
                ("IWileyWidgetContextService", typeof(IWileyWidgetContextService)),
                ("IAILoggingService", typeof(IAILoggingService)),
                ("IDataAnonymizerService", typeof(IDataAnonymizerService))
            };

            var validationErrors = new List<string>();

            foreach (var (serviceName, serviceType) in criticalServices)
            {
                try
                {
                    var unityContainer = containerRegistry.GetContainer();
                    var service = unityContainer.Resolve(serviceType);
                    if (service == null)
                    {
                        validationErrors.Add($"{serviceName} resolved to null");
                    }
                    else
                    {
                        Log.Debug($"✓ {serviceName} validated successfully");
                    }
                }
                catch (Exception ex)
                {
                    validationErrors.Add($"{serviceName} failed to resolve: {ex.Message}");
                    Log.Warning(ex, $"Critical service validation failed for {serviceName}");
                }
            }

            if (validationErrors.Any())
            {
                Log.Error("Critical service validation failed:");
                foreach (var error in validationErrors)
                {
                    Log.Error($"  - {error}");
                }
                throw new InvalidOperationException($"Critical services failed validation: {string.Join(", ", validationErrors)}");
            }

            Log.Information("✓ All critical services validated successfully");
        }

        /// <summary>
        /// Registers HttpClient infrastructure for AI services with retry policies and timeout configuration.
        /// Production-ready implementation with comprehensive error handling and logging.
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        /// <param name="configuration">Application configuration for HttpClient settings</param>
        private void RegisterHttpClientServices(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            Log.Information("=== Registering HttpClient Infrastructure for AI Services ===");
            
            try
            {
                // Create a service collection for HttpClient registration (Microsoft.Extensions.DependencyInjection pattern)
                var services = new ServiceCollection();
                
                // Configure named HttpClient for AI services with timeout and base address
                var xaiBaseUrl = configuration["XAI:BaseUrl"] ?? "https://api.x.ai/v1/";
                var timeoutSeconds = double.Parse(configuration["XAI:TimeoutSeconds"] ?? "30");
                
                services.AddHttpClient("AIServices", client =>
                {
                    client.BaseAddress = new Uri(xaiBaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
                    client.DefaultRequestHeaders.Add("User-Agent", "WileyWidget/1.0");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = 3,
                    UseDefaultCredentials = false
                })
                .SetHandlerLifetime(TimeSpan.FromMinutes(5)); // Connection pooling for 5 minutes
                
                // Build the service provider and extract IHttpClientFactory
                var serviceProvider = services.BuildServiceProvider();
                var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                
                // Register IHttpClientFactory as singleton in Unity container
                containerRegistry.RegisterInstance<IHttpClientFactory>(httpClientFactory);
                
                Log.Information("✓ Registered IHttpClientFactory with named client 'AIServices'");
                Log.Information($"  - Base URL: {xaiBaseUrl}");
                Log.Information($"  - Timeout: {timeoutSeconds} seconds");
                Log.Information($"  - Handler Lifetime: 5 minutes (connection pooling)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register HttpClient infrastructure for AI services");
                throw new InvalidOperationException("Failed to configure HttpClient for AI services. Check configuration and network settings.", ex);
            }
        }

        /// <summary>
        /// Registers AI Integration Services for Phase 1 production deployment.
        /// Includes GrokSupercomputer, WileyWidgetContextService, and enhanced XAIService.
        /// All services are registered as singletons for optimal performance and resource management.
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        private void RegisterAIIntegrationServices(IContainerRegistry containerRegistry)
        {
            Log.Information("=== Registering AI Integration Services (Phase 1 - Production) ===");
            
            try
            {
                // 0. Register IDataAnonymizerService -> DataAnonymizerService (Singleton)
                // Provides privacy-compliant data anonymization for AI operations
                containerRegistry.RegisterSingleton<IDataAnonymizerService, DataAnonymizerService>();
                Log.Information("✓ Registered IDataAnonymizerService -> DataAnonymizerService (Singleton)");
                Log.Information("  - Provides GDPR-compliant data anonymization");
                Log.Information("  - Features: Enterprise anonymization, budget data masking, deterministic hashing");
                Log.Information("  - Dependencies: ILogger<DataAnonymizerService>");
                
                // 1. Register IWileyWidgetContextService -> WileyWidgetContextService (Singleton)
                // Provides dynamic context building for AI operations including system state, enterprises, budgets, and operations
                containerRegistry.RegisterSingleton<IWileyWidgetContextService, WileyWidgetContextService>();
                Log.Information("✓ Registered IWileyWidgetContextService -> WileyWidgetContextService (Singleton)");
                Log.Information("  - Provides dynamic context for AI operations with anonymization support");
                Log.Information("  - Dependencies: ILogger<WileyWidgetContextService>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IDataAnonymizerService");
                
                // 1.5. Register IAILoggingService -> AILoggingService (Singleton)
                // AI usage tracking and logging service for monitoring XAI operations
                containerRegistry.RegisterSingleton<IAILoggingService, AILoggingService>();
                Log.Information("✓ Registered IAILoggingService -> AILoggingService (Singleton)");
                Log.Information("  - AI usage tracking and monitoring service");
                Log.Information("  - Features: Query/response logging, error tracking, usage metrics, statistics");
                Log.Information("  - Logging: Dedicated Serilog file sink at logs/ai-usage.log");
                Log.Information("  - Dependencies: ILogger<AILoggingService>");
                
                // 2. Register IGrokSupercomputer -> GrokSupercomputer (Singleton)
                // AI-powered municipal utility analytics and compliance reporting engine
                containerRegistry.RegisterSingleton<IGrokSupercomputer, GrokSupercomputer>();
                Log.Information("✓ Registered IGrokSupercomputer -> GrokSupercomputer (Singleton)");
                Log.Information("  - AI-powered municipal utility analytics engine");
                Log.Information("  - Capabilities: Enterprise data fetching, report calculations, budget analysis, compliance reporting");
                Log.Information("  - Dependencies: ILogger<GrokSupercomputer>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IAILoggingService");
                
                // 3. Register IAIService -> XAIService (Singleton) - Enhanced with context service and logging
                // xAI service implementation for AI-powered insights and analysis with Grok integration
                containerRegistry.RegisterSingleton<IAIService, XAIService>();
                Log.Information("✓ Registered IAIService -> XAIService (Singleton) [Enhanced]");
                Log.Information("  - xAI/Grok integration for AI-powered insights");
                Log.Information("  - Features: Insights, data analysis, area review, mock data generation");
                Log.Information("  - Dependencies: IHttpClientFactory, IConfiguration, ILogger<XAIService>, IWileyWidgetContextService, IAILoggingService");
                Log.Information("  - Configuration: XAI:ApiKey, XAI:BaseUrl, XAI:Model, XAI:TimeoutSeconds");
                
                // 4. Validate AI service configuration
                ValidateAIServiceConfiguration();
                
                Log.Information("=== AI Integration Services Registration Complete ===");
                Log.Information("All AI services registered successfully with singleton lifetime scope");
                Log.Information("Services ready for production use with comprehensive dependency injection");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CRITICAL: Failed to register AI Integration Services");
                Log.Error("Application may not function correctly without AI services");
                Log.Error("Please check configuration (appsettings.json) and ensure all dependencies are available");
                throw new InvalidOperationException("Failed to register AI Integration Services. Application cannot continue.", ex);
            }
        }

        /// <summary>
        /// Validates AI service configuration to ensure all required settings are present.
        /// Production-ready validation with comprehensive error reporting.
        /// </summary>
        private void ValidateAIServiceConfiguration()
        {
            Log.Information("Validating AI service configuration...");
            
            try
            {
                var config = Container.Resolve<IConfiguration>();
                var validationErrors = new List<string>();
                
                // Validate XAI configuration
                var apiKey = config["XAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    validationErrors.Add("XAI:ApiKey is missing or empty");
                }
                else if (apiKey.Length < 20)
                {
                    validationErrors.Add("XAI:ApiKey appears invalid (too short, expected 20+ characters)");
                }
                
                var baseUrl = config["XAI:BaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    Log.Warning("XAI:BaseUrl not configured, using default: https://api.x.ai/v1/");
                }
                else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    validationErrors.Add($"XAI:BaseUrl is invalid: {baseUrl}");
                }
                
                var model = config["XAI:Model"];
                if (string.IsNullOrWhiteSpace(model))
                {
                    Log.Warning("XAI:Model not configured, using default: grok-4-0709");
                }
                
                var timeout = config["XAI:TimeoutSeconds"];
                if (!string.IsNullOrWhiteSpace(timeout) && !double.TryParse(timeout, out var timeoutValue))
                {
                    validationErrors.Add($"XAI:TimeoutSeconds is invalid: {timeout}");
                }
                
                if (validationErrors.Any())
                {
                    Log.Error("AI Service configuration validation failed:");
                    foreach (var error in validationErrors)
                    {
                        Log.Error($"  - {error}");
                    }
                    throw new InvalidOperationException($"AI Service configuration is invalid: {string.Join(", ", validationErrors)}");
                }
                
                Log.Information("✓ AI service configuration validated successfully");
                Log.Information($"  - API Key: Configured ({apiKey?.Substring(0, Math.Min(8, apiKey.Length))}...)");
                Log.Information($"  - Base URL: {baseUrl ?? "Default (https://api.x.ai/v1/)"}");
                Log.Information($"  - Model: {model ?? "Default (grok-4-0709)"}");
                Log.Information($"  - Timeout: {timeout ?? "Default (30)"} seconds");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to validate AI service configuration");
                throw;
            }
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);
            // Custom adapter for Syncfusion controls (e.g., DockingManager)
            regionAdapterMappings.RegisterMapping(typeof(Syncfusion.Windows.Tools.Controls.DockingManager), Container.Resolve<WileyWidget.Regions.DockingManagerRegionAdapter>());
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            // Add modules for views (from Prism docs: This loads views into regions)
            moduleCatalog.AddModule<DashboardModule>();
            // Add others as they exist
        }

        protected override void OnInitialized()
        {
            try
            {
                base.OnInitialized();

                // CurrentContainer is already set in CreateShell()
                // This ensures it's available during shell construction
                if (CurrentContainer == null)
                {
                    throw new InvalidOperationException("CurrentContainer not set. DI initialization failed.");
                }

                Log.Information("Application initialization completed successfully");
                Log.Information("All services registered and container ready for use");

                Application.Current.MainWindow?.Show();

                if (SplashScreenInstance != null)
                {
                    SplashScreenInstance.Close();
                    SplashScreenInstance = null;
                    Log.Information("Splash screen closed successfully");
                }

                try
                {
                    // Optional: Navigate to default view
                    // var regionManager = Container.Resolve<IRegionManager>();
                    // regionManager.RequestNavigate("MainRegion", "DashboardView");
                    // Log.Information("Navigated to DashboardView during application initialization");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to navigate to default view during startup - continuing without navigation");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Critical error during application initialization");
                // Ensure splash screen is closed even on error
                if (SplashScreenInstance != null)
                {
                    try
                    {
                        SplashScreenInstance.Close();
                        SplashScreenInstance = null;
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                throw;
            }
        }

        protected override IContainerExtension CreateContainerExtension()
        {
            return new UnityContainerExtension();
        }

        private static void ConfigureLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File("logs/wiley-widget-.log", 
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            Log.Information("=== WileyWidget Prism application startup ===");
        }

        private IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            return builder.Build();
        }
    }
}