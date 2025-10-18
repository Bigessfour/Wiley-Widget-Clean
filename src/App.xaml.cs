using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Reflection;
using Prism.Unity;
using Prism.Modularity;
using Prism.Container.Unity;
using Unity;
using Unity.Resolution;
using Syncfusion.SfSkinManager;
using Syncfusion.Licensing;
using WileyWidget.Views;
using WileyWidget.Startup.Modules;
using WileyWidget.Services;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Serilog.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using DotNetEnv;
using WileyWidget.Regions;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using Azure.Identity;
using System.Text.RegularExpressions;
using Prism.Events;
using WileyWidget.ViewModels.Messages;
// using Microsoft.ApplicationInsights;
// using Microsoft.ApplicationInsights.Extensibility;
using Serilog.Events;

namespace WileyWidget
{
    public class App : PrismApplication
    {
        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        public static object StartupProgress { get; set; }
        public static void UpdateLatestHealthReport(object report) { /* Stub */ }

        protected override void OnStartup(StartupEventArgs e)
        {
            // Set up global exception handling before anything else
            SetupGlobalExceptionHandling();

            ConfigureLogging();
            Trace.WriteLine("[App] ConfigureLogging completed");

            base.OnStartup(e);

            Log.Information("Application startup completed");
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

        protected override Window CreateShell()
        {
            // Syncfusion setup (from Syncfusion WPF docs: Register license before any controls load)
            try
            {
                var cfg = BuildConfiguration();
                var licenseKey = cfg["Syncfusion:LicenseKey"] ?? cfg["Syncfusion:License"] ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                if (!string.IsNullOrWhiteSpace(licenseKey) && !licenseKey.Contains("YOUR_SYNCFUSION_LICENSE_KEY_HERE"))
                {
                    SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                    Log.Information("Syncfusion license registered from configuration (masked: {Mask})", licenseKey.Length > 8 ? licenseKey.Substring(0, 8) + "..." : "(masked)");
                }
                else
                {
                    Log.Warning("Syncfusion license key not configured. Set Syncfusion:LicenseKey in appsettings.json or SYNCFUSION_LICENSE_KEY environment variable to suppress runtime license dialogs.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to register Syncfusion license during startup - continuing without license registration (this may show license dialogs on first use)");
            }

            try
            {
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
            // Initialize global theme using ThemeManager for proper fallback support
            // This ensures FluentDark as default with FluentLight as fallback
            var themeManager = Container.Resolve<IThemeManager>();
            try
            {
                // Apply the current theme (from settings) or default to FluentDark
                var currentTheme = themeManager.CurrentTheme;
                if (string.IsNullOrEmpty(currentTheme) || !ThemeManager.AvailableThemes.Contains(currentTheme))
                {
                    currentTheme = "FluentDark";
                }
                themeManager.ApplyTheme(currentTheme);
                Log.Information("Global theme initialized: {Theme}", currentTheme);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize global theme, falling back to FluentLight");
                try
                {
                    themeManager.ApplyTheme("FluentLight");
                }
                catch
                {
                    // Last resort - set directly if ThemeManager fails
#pragma warning disable CA2000
                    SfSkinManager.ApplicationTheme = new Theme("FluentLight");
#pragma warning restore CA2000
                }
            }

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

            // Register database services
            RegisterDatabaseServices(containerRegistry, configuration);

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, EncryptedLocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            containerRegistry.RegisterSingleton<ThemeManager>();
            containerRegistry.RegisterSingleton<IThemeManager>(provider => provider.Resolve<ThemeManager>());
            containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());

            // Register IServiceProvider via Unity adapter so framework services can request it per Prism guidance
            var unityContainer = containerRegistry.GetContainer();
            var serviceProviderAdapter = new UnityServiceProviderAdapter(unityContainer);
            containerRegistry.RegisterInstance<IServiceProvider>(serviceProviderAdapter);
            EnableUnityDiagnostics(unityContainer);
            Log.Information("✓ Registered core infrastructure services (Syncfusion, Settings, Theme, Dispatcher, IServiceProvider adapter)");

            // Initialize production secrets (synchronous for reliability)
            try
            {
                var secretVault = Container.Resolve<ISecretVaultService>();
                secretVault.MigrateSecretsFromEnvironmentAsync().GetAwaiter().GetResult();
                Log.Information("✓ Environment secrets migrated to local vault");

                // Only populate production secrets if we're in production environment
                var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
                if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                {
                    secretVault.PopulateProductionSecretsAsync().GetAwaiter().GetResult();
                    Log.Information("✓ Production secrets initialized");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize production secrets");
            }
            
            // Register Microsoft.Extensions.Caching.Memory infrastructure
            var services = new ServiceCollection();
            services.AddMemoryCache();
            var serviceProvider = services.BuildServiceProvider();
            var memoryCache = serviceProvider.GetRequiredService<IMemoryCache>();
            containerRegistry.RegisterInstance<IMemoryCache>(memoryCache);
            Log.Information("✓ Registered IMemoryCache for in-memory caching infrastructure");
            
            // Register data repositories required during startup validation to prevent Unity resolution failures
            containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
            containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();
            containerRegistry.Register<IAuditRepository, WileyWidget.Data.AuditRepository>();
            Log.Information("✓ Registered core data repositories for startup validation (Enterprise, Budget, Audit)");
            
            // Register Microsoft.Extensions.DependencyInjection compatibility BEFORE business services
            // IServiceScopeFactory is required by WhatIfScenarioEngine but Unity doesn't provide it natively
            // Note: Removed UnityServiceScopeFactory - modules handle their own service scopes
            Log.Information("✓ Microsoft.Extensions.DependencyInjection compatibility handled by modules");
            
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
            
            // Register Module Health Service
            containerRegistry.RegisterSingleton<IModuleHealthService, ModuleHealthService>();
            Log.Information("✓ Registered IModuleHealthService as singleton");
            
            // Register Prism DialogService
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            Log.Information("✓ Registered Prism IDialogService as singleton");

            // Register Prism Dialogs
            containerRegistry.RegisterDialog<Views.ConfirmationDialogView, ViewModels.ConfirmationDialogViewModel>("ConfirmationDialog");
            containerRegistry.RegisterDialog<Views.NotificationDialogView, ViewModels.NotificationDialogViewModel>("NotificationDialog");
            containerRegistry.RegisterDialog<Views.WarningDialogView, ViewModels.WarningDialogViewModel>("WarningDialog");
            containerRegistry.RegisterDialog<Views.ErrorDialogView, ViewModels.ErrorDialogViewModel>("ErrorDialog");
            containerRegistry.RegisterDialog<Views.SettingsDialogView, ViewModels.SettingsDialogViewModel>("SettingsDialog");
            Log.Information("✓ Registered Prism Dialogs (Confirmation, Notification, Warning, Error, Settings)");

            // Register Navigation Service with Journal support
            containerRegistry.RegisterSingleton<INavigationService, NavigationService>();
            Log.Information("✓ Registered INavigationService with journal support");

            // Register Composite Command Service
            containerRegistry.RegisterSingleton<ICompositeCommandService, CompositeCommandService>();
            Log.Information("✓ Registered ICompositeCommandService for coordinating multiple commands");

            // Register Interaction Request Service
            containerRegistry.RegisterSingleton<IInteractionRequestService, InteractionRequestService>();
            Log.Information("✓ Registered IInteractionRequestService for ViewModel-View communication");

            // Register Scoped Region Service
            containerRegistry.RegisterSingleton<IScopedRegionService, ScopedRegionService>();
            Log.Information("✓ Registered IScopedRegionService for isolated navigation contexts");
            
            // Register Prism Error Handler for centralized error handling
            containerRegistry.RegisterSingleton<IPrismErrorHandler, PrismErrorHandler>();
            Log.Information("✓ Registered IPrismErrorHandler for centralized error handling and logging");
            
            // Register ViewModels (module-specific ViewModels are now registered in their respective modules)
            containerRegistry.RegisterSingleton<MainViewModel>(provider => new MainViewModel(
                provider.Resolve<IRegionManager>(),
                provider.Resolve<IDialogService>(),
                provider.Resolve<IDispatcherHelper>(),
                provider.Resolve<ILogger<MainViewModel>>(),
                provider.Resolve<IEnterpriseRepository>(),
                provider.Resolve<IExcelReaderService>(),
                provider.Resolve<IReportExportService>(),
                provider.Resolve<IBudgetRepository>(),
                provider.Resolve<IAIService>()));

            // Register additional ViewModels for Prism ViewModelLocator (infrastructure-only)
            containerRegistry.Register<SplashScreenWindowViewModel>();
            containerRegistry.Register<AboutViewModel>();
            containerRegistry.Register<ExcelImportViewModel>();
            containerRegistry.Register<ProgressViewModel>();
            
            // Register Region Adapters
            containerRegistry.Register<WileyWidget.Regions.DockingManagerRegionAdapter>();

            // Navigation registrations are now handled by individual modules

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
                ("IModuleHealthService", typeof(IModuleHealthService)),
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

        [Conditional("DEBUG")]
        private static void EnableUnityDiagnostics(IUnityContainer unityContainer)
        {
            if (unityContainer == null)
            {
                throw new ArgumentNullException(nameof(unityContainer));
            }

            unityContainer.AddExtension(new UnityDebugExtension());

            if (!Trace.Listeners.OfType<ConsoleTraceListener>().Any(listener => listener.Name == "UnityConsole"))
            {
                Trace.Listeners.Add(new ConsoleTraceListener { Name = "UnityConsole" });
            }

            Trace.WriteLine("[Unity] Debug diagnostics initialized");
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
        /// Registers database services using Microsoft.Extensions.DependencyInjection pattern
        /// </summary>
        /// <param name="containerRegistry">The Unity container registry for DI registration</param>
        /// <param name="configuration">Application configuration for database settings</param>
        private void RegisterDatabaseServices(IContainerRegistry containerRegistry, IConfiguration configuration)
        {
            Log.Information("=== Registering Database Services ===");

            try
            {
                // Unity-only approach: build DbContextOptions<AppDbContext> from configuration
                // and register a small Unity-friendly factory implementation that takes
                // DbContextOptions<AppDbContext> in its constructor. This removes the
                // cross-container ServiceCollection usage and keeps DI with Unity only.

                var connectionString = configuration.GetConnectionString("DefaultConnection")
                                       ?? "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                // Configure SQL Server with reasonable defaults (migrations assembly, retries, timeout)
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("WileyWidget.Data");
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });

                optionsBuilder.EnableDetailedErrors();
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

                var options = optionsBuilder.Options;

                // Register the options instance with Unity
                containerRegistry.RegisterInstance<DbContextOptions<AppDbContext>>(options);

                // Register a Unity-friendly factory implementation that Unity can construct
                // because it takes DbContextOptions<AppDbContext> in its ctor
                containerRegistry.RegisterSingleton<IDbContextFactory<AppDbContext>, WileyWidget.Data.UnityAppDbContextFactory>();

                // Register AppDbContext using the factory
                containerRegistry.Register<AppDbContext>(provider => provider.Resolve<IDbContextFactory<AppDbContext>>().CreateDbContext());

                Log.Information("✓ Registered database services (AppDbContext, IDbContextFactory via Unity)");
                Log.Information("  - Provider: SQL Server with connection pooling");
                Log.Information("  - Features: Unity-only registration, repositories, audit logging");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register database services");
                throw new InvalidOperationException("Failed to configure database services. Check connection string and database availability.", ex);
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
                // 0. Register Application Insights Telemetry (Singleton)
                // Production telemetry for AI service monitoring and performance tracking
                // NOTE: Commented out until Azure/Application Insights is configured
                /*
                var config = Container.Resolve<IConfiguration>();
                var instrumentationKey = config["ApplicationInsights:InstrumentationKey"];
                if (!string.IsNullOrEmpty(instrumentationKey))
                {
                    var telemetryConfiguration = new Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration();
                    telemetryConfiguration.ConnectionString = config["ApplicationInsights:ConnectionString"] ?? $"InstrumentationKey={instrumentationKey}";
                    
                    var telemetryClient = new Microsoft.ApplicationInsights.TelemetryClient(telemetryConfiguration);
                    containerRegistry.RegisterInstance(telemetryClient);
                    Log.Information("✓ Registered Application Insights TelemetryClient (Singleton)");
                    Log.Information("  - Production telemetry for AI service monitoring");
                    Log.Information("  - Features: Request tracking, dependency monitoring, custom events, metrics");
                    Log.Information("  - Configuration: InstrumentationKey, ConnectionString from appsettings.json");
                }
                else
                {
                    Log.Warning("Application Insights not configured - set ApplicationInsights:InstrumentationKey in appsettings.json for production telemetry");
                }
                */
                
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
                
                // 2. Register IAIService -> XAIService (Singleton) - Enhanced with context service and logging
                // xAI service implementation for AI-powered insights and analysis with Grok integration
                containerRegistry.RegisterSingleton<IAIService, XAIService>();
                Log.Information("✓ Registered IAIService -> XAIService (Singleton) [Enhanced]");
                Log.Information("  - xAI/Grok integration for AI-powered insights");
                Log.Information("  - Features: Insights, data analysis, area review, mock data generation");
                Log.Information("  - Dependencies: IHttpClientFactory, IConfiguration, ILogger<XAIService>, IWileyWidgetContextService, IAILoggingService, IMemoryCache");
                Log.Information("  - Configuration: XAI:ApiKey, XAI:BaseUrl, XAI:Model, XAI:TimeoutSeconds");
                
                // 3. Register IGrokSupercomputer -> GrokSupercomputer (Singleton)
                // AI-powered municipal utility analytics and compliance reporting engine
                containerRegistry.RegisterSingleton<IGrokSupercomputer, GrokSupercomputer>();
                Log.Information("✓ Registered IGrokSupercomputer -> GrokSupercomputer (Singleton)");
                Log.Information("  - AI-powered municipal utility analytics engine");
                Log.Information("  - Capabilities: Enterprise data fetching, report calculations, budget analysis, compliance reporting, AI data analysis");
                Log.Information("  - Dependencies: ILogger<GrokSupercomputer>, IEnterpriseRepository, IBudgetRepository, IAuditRepository, IAILoggingService, IAIService");
                
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
                    apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ?? string.Empty;
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        Log.Information("XAI:ApiKey pulled from environment variable XAI_API_KEY");
                    }
                }
                Log.Information("XAI:ApiKey resolved to: {ApiKeyMasked} (length: {Length})", 
                    string.IsNullOrEmpty(apiKey) ? "null/empty" : $"{apiKey.Substring(0, Math.Min(10, apiKey.Length))}...", 
                    apiKey?.Length ?? 0);
                
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

            // Standard WPF control region adapters are registered by base.ConfigureRegionAdapterMappings()
            // including TabControl, ContentControl, ItemsControl, and Selector

            // Custom adapter for Syncfusion controls (e.g., DockingManager)
            regionAdapterMappings.RegisterMapping(typeof(Syncfusion.Windows.Tools.Controls.DockingManager), Container.Resolve<WileyWidget.Regions.DockingManagerRegionAdapter>());

            // Register region behaviors
            var regionBehaviorFactory = Container.Resolve<IRegionBehaviorFactory>();

            // Navigation logging behavior for all regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.NavigationLoggingBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.NavigationLoggingBehavior));

            // Auto-save behavior for data entry regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.AutoSaveBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.AutoSaveBehavior));

            // Navigation history behavior for main content regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.NavigationHistoryBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.NavigationHistoryBehavior));

            // Auto-activate behavior for single-view regions
            regionBehaviorFactory.AddIfMissing(WileyWidget.Regions.AutoActivateBehavior.BehaviorKey,
                typeof(WileyWidget.Regions.AutoActivateBehavior));

            Log.Information("✓ Registered Prism region behaviors: NavigationLogging, AutoSave, NavigationHistory, AutoActivate");
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            Log.Information("=== Configuring Prism Module Catalog with Auto-Discovery and Initialization Modes ===");

            // Auto-discover modules using reflection
            // Find all types in the current assembly that implement IModule and have [Module] attribute
            var moduleTypes = typeof(App).Assembly
                .GetTypes()
                .Where(t => typeof(IModule).IsAssignableFrom(t) &&
                           t.GetCustomAttributes(typeof(ModuleAttribute), false).Any() &&
                           !t.IsAbstract &&
                           t.IsClass)
                .ToList();

            Log.Information("Found {Count} modules with [Module] attribute", moduleTypes.Count);

            // Get initialization mode from configuration
            var configuration = BuildConfiguration();
            var defaultInitMode = configuration.GetValue("Prism:DefaultModuleInitializationMode", "WhenAvailable");

            foreach (var moduleType in moduleTypes)
            {
                var moduleAttribute = (ModuleAttribute)moduleType.GetCustomAttributes(typeof(ModuleAttribute), false).First();
                var moduleName = moduleAttribute.ModuleName;

                // Determine initialization mode for this module
                var initMode = GetModuleInitializationMode(moduleName, defaultInitMode);

                // Add module with specified initialization mode
                moduleCatalog.AddModule(moduleType, initMode);

                Log.Information("Registered module: {ModuleName} ({TypeName}) with initialization mode: {InitMode}",
                    moduleName, moduleType.Name, initMode);
            }

            Log.Information("✓ Auto-discovery completed for {Count} modules with configurable initialization modes", moduleTypes.Count);
        }

        protected override void InitializeModules()
        {
            Log.Information("=== Initializing Prism Modules ===");

            base.InitializeModules();

            // Get the module health service for validation
            var moduleHealthService = Container.Resolve<IModuleHealthService>();

            // Validate module initialization and region availability
            ValidateModuleInitialization(moduleHealthService);

            // Initialize global error handling for Prism navigation and general errors
            InitializeGlobalErrorHandling();

            Log.Information("✓ Module initialization completed successfully");
        }

        /// <summary>
        /// Initializes global error handling for Prism applications.
        /// Sets up EventAggregator subscriptions for centralized error handling and logging.
        /// </summary>
        private void InitializeGlobalErrorHandling()
        {
            Log.Information("=== Initializing Global Error Handling ===");

            try
            {
                // Resolve the error handler service
                var errorHandler = Container.Resolve<IPrismErrorHandler>();
                var eventAggregator = Container.Resolve<Prism.Events.IEventAggregator>();

                // Subscribe to navigation error events for global handling
                eventAggregator.GetEvent<NavigationErrorEvent>().Subscribe(
                    errorEvent =>
                    {
                        Log.Error("Global navigation error handler: Region '{RegionName}' failed to navigate to '{TargetView}': {ErrorMessage}",
                            errorEvent.RegionName, errorEvent.TargetView, errorEvent.ErrorMessage);
                        
                        // Additional global error handling logic can be added here
                        // For example: showing error dialogs, sending telemetry, etc.
                    },
                    ThreadOption.UIThread); // Handle on UI thread for dialog display

                // Subscribe to general error events for global handling
                eventAggregator.GetEvent<GeneralErrorEvent>().Subscribe(
                    errorEvent =>
                    {
                        var logLevel = errorEvent.IsHandled ? LogEventLevel.Warning : LogEventLevel.Error;
                        Log.Write(logLevel, errorEvent.Error, "Global error handler: {Source}.{Operation} - {ErrorMessage}",
                            errorEvent.Source, errorEvent.Operation, errorEvent.ErrorMessage);

                        // Additional global error handling logic can be added here
                        // For example: error reporting, user notifications, etc.
                    },
                    ThreadOption.UIThread);

                // Register global navigation handlers in the error handler
                errorHandler.RegisterGlobalNavigationHandlers();

                Log.Information("✓ Global error handling initialized with EventAggregator subscriptions");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize global error handling");
                // Don't throw - allow application to continue with degraded error handling
            }
        }

        /// <summary>
        /// Determines the initialization mode for a specific module.
        /// </summary>
        private InitializationMode GetModuleInitializationMode(string moduleName, string defaultMode)
        {
            // Define module-specific initialization modes
            var moduleInitModes = new Dictionary<string, InitializationMode>
            {
                // Core infrastructure module - load immediately (consolidated from Diagnostics, Syncfusion, Settings)
                ["CoreModule"] = InitializationMode.WhenAvailable,
                ["QuickBooksModule"] = InitializationMode.OnDemand,

                // Feature modules - load on demand to improve startup performance
                ["DashboardModule"] = InitializationMode.OnDemand,
                ["EnterpriseModule"] = InitializationMode.OnDemand,
                ["BudgetModule"] = InitializationMode.OnDemand,
                ["MunicipalAccountModule"] = InitializationMode.OnDemand,
                ["UtilityCustomerModule"] = InitializationMode.OnDemand,
                ["ReportsModule"] = InitializationMode.OnDemand,
                ["AIAssistModule"] = InitializationMode.OnDemand,

                // Panel modules - load when their dependencies are loaded
                ["PanelModule"] = InitializationMode.WhenAvailable,
                ["ToolsModule"] = InitializationMode.OnDemand
            };

            return moduleInitModes.TryGetValue(moduleName, out var mode) ? mode : Enum.Parse<InitializationMode>(defaultMode);
        }

        /// <summary>
        /// Validates that all modules are properly initialized and their regions are available.
        /// Provides comprehensive diagnostics for module and region health.
        /// </summary>
        /// <param name="moduleHealthService">The module health service for status checking</param>
        private void ValidateModuleInitialization(IModuleHealthService moduleHealthService)
        {
            Log.Information("=== Validating Module Initialization and Region Availability ===");

            var regionManager = Container.Resolve<IRegionManager>();
            var validationResults = new List<string>();

            // Define expected regions for each module
            var moduleRegionMap = new Dictionary<string, string[]>
            {
                ["CoreModule"] = new[] { "SettingsRegion" }, // Core module handles settings
                ["DashboardModule"] = new[] { "MainRegion" },
                ["EnterpriseModule"] = new[] { "EnterpriseRegion" },
                ["BudgetModule"] = new[] { "BudgetRegion", "AnalyticsRegion" },
                ["MunicipalAccountModule"] = new[] { "MunicipalAccountRegion" },
                ["UtilityCustomerModule"] = new[] { "UtilityCustomerRegion" },
                ["ReportsModule"] = new[] { "ReportsRegion" },
                ["AIAssistModule"] = new[] { "AIAssistRegion" },
                ["PanelModule"] = new[] { "LeftPanelRegion", "RightPanelRegion", "BottomPanelRegion" },
                ["ToolsModule"] = new[] { "BottomPanelRegion" }
            };

            foreach (var moduleStatus in moduleHealthService.GetAllModuleStatuses())
            {
                var moduleName = moduleStatus.ModuleName;
                var status = moduleStatus.Status;

                if (status == ModuleHealthStatus.Healthy)
                {
                    // Check if expected regions are available for healthy modules
                    if (moduleRegionMap.TryGetValue(moduleName, out var expectedRegions))
                    {
                        var missingRegions = expectedRegions.Where(region => !regionManager.Regions.ContainsRegionWithName(region)).ToList();
                        if (missingRegions.Any())
                        {
                            validationResults.Add($"WARNING: Module '{moduleName}' healthy but missing regions: {string.Join(", ", missingRegions)}");
                        }
                        else
                        {
                            validationResults.Add($"✓ Module '{moduleName}' validation passed - all regions available");
                        }
                    }
                    else
                    {
                        validationResults.Add($"✓ Module '{moduleName}' validation passed - no regions to validate");
                    }
                }
                else if (status == ModuleHealthStatus.Failed)
                {
                    validationResults.Add($"✗ Module '{moduleName}' failed initialization: {moduleStatus.ErrorMessage}");
                }
                else
                {
                    validationResults.Add($"? Module '{moduleName}' status: {status}");
                }
            }

            // Log validation results
            foreach (var result in validationResults)
            {
                if (result.StartsWith("✓"))
                    Log.Information(result);
                else if (result.StartsWith("✗"))
                    Log.Error(result);
                else if (result.StartsWith("WARNING"))
                    Log.Warning(result);
                else
                    Log.Information(result);
            }

            var healthyModules = moduleHealthService.GetAllModuleStatuses().Count(m => m.Status == ModuleHealthStatus.Healthy);
            var totalModules = moduleHealthService.GetAllModuleStatuses().Count();

            Log.Information("=== Module Validation Complete ===");
            Log.Information("Modules Healthy: {Healthy}/{Total}", healthyModules, totalModules);

            if (healthyModules == totalModules)
            {
                Log.Information("✓ All modules validated successfully - application ready");
            }
            else
            {
                Log.Warning("⚠ Some modules failed validation - application may have reduced functionality");
            }
        }

        protected override void OnInitialized()
        {
            try
            {
                base.OnInitialized();

                Log.Information("Application initialization completed successfully");
                Log.Information("All services registered and container ready for use");

                Application.Current.MainWindow?.Show();

                // Log module health report after initialization
                try
                {
                    var moduleHealthService = Container.Resolve<IModuleHealthService>();
                    moduleHealthService.LogHealthReport();

                    // Validate module initialization and region availability
                    ValidateModuleInitialization(moduleHealthService);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to generate module health report during application initialization");
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
                throw;
            }
        }

        /// <summary>
        /// Registers a module with the module catalog and tracks it with the health service.
        /// </summary>
        /// <param name="moduleCatalog">The module catalog to add the module to</param>
        /// <param name="healthService">The health service to track the module</param>
        /// <param name="moduleName">The name of the module for tracking</param>
        /// <param name="registerAction">The action to perform the module registration</param>
        private void RegisterModuleWithHealthTracking(IModuleCatalog moduleCatalog, IModuleHealthService healthService, string moduleName, Action registerAction)
        {
            try
            {
                healthService.RegisterModule(moduleName);
                registerAction();
                Log.Debug("Successfully registered module '{ModuleName}' with catalog and health tracking", moduleName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to register module '{ModuleName}' with catalog", moduleName);
                healthService.MarkModuleInitialized(moduleName, false, ex.Message);
                throw;
            }
        }

        protected override IContainerExtension CreateContainerExtension()
        {
#pragma warning disable CA2000 // Call System.IDisposable.Dispose on object created by 'new UnityContainer()' before all references to it are out of scope
            var container = new UnityContainer();
#pragma warning restore CA2000
            container.AddExtension(new Diagnostic());
            return new UnityContainerExtension(container);
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
            // Load .env file if it exists
            DotNetEnv.Env.Load();

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var configurationRoot = builder.Build();
            ResolveConfigurationPlaceholders(configurationRoot);

            return configurationRoot;
        }

        private static void ResolveConfigurationPlaceholders(IConfigurationRoot configurationRoot)
        {
            if (configurationRoot == null)
            {
                return;
            }

            var values = configurationRoot.AsEnumerable().ToList();
            foreach (var entry in values)
            {
                if (string.IsNullOrWhiteSpace(entry.Value) || entry.Value.IndexOf("${", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var replaced = PlaceholderRegex.Replace(entry.Value, match =>
                {
                    var variableName = match.Groups["name"].Value;
                    if (string.IsNullOrWhiteSpace(variableName))
                    {
                        return match.Value;
                    }

                    var resolved = Environment.GetEnvironmentVariable(variableName);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        resolved = configurationRoot[variableName];
                    }

                    return string.IsNullOrEmpty(resolved) ? match.Value : resolved;
                });

                if (!string.Equals(replaced, entry.Value, StringComparison.Ordinal))
                {
                    configurationRoot[entry.Key] = replaced;
                }
            }
        }

        private static readonly Regex PlaceholderRegex = new("\\$\\{(?<name>[A-Za-z0-9_]+)\\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Ensure application shutdown is robust. Some third-party libraries (Syncfusion) may attempt to show
        /// dialogs during shutdown which can throw if the UI thread or owner windows are disposed. We catch
        /// and swallow known shutdown-time exceptions to avoid the process terminating with an unhelpful crash.
        /// </summary>
        /// <param name="e">Exit event args</param>
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutdown initiated");

            try
            {
                // Best-effort: re-register Syncfusion license in case some syncfusion component needs it during shutdown
                try
                {
                    var cfg = BuildConfiguration();
                    var licenseKey = cfg["Syncfusion:LicenseKey"] ?? Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
                    if (!string.IsNullOrWhiteSpace(licenseKey))
                    {
                        try
                        {
                            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
                            Log.Debug("Re-registered Syncfusion license during shutdown (masked)");
                        }
                        catch (Exception rex)
                        {
                            Log.Warning(rex, "Re-registering Syncfusion license during shutdown failed - continuing with shutdown");
                        }
                    }
                }
                catch (Exception) { /* ignore configuration read errors during shutdown */ }

                base.OnExit(e);
                Log.Information("Application shutdown completed successfully");
            }
            catch (Exception ex)
            {
                // Special handling: Syncfusion LicenseMessage.ShowDialog can throw during shutdown
                var msg = ex.ToString();
                if (msg.Contains("LicenseMessage", StringComparison.OrdinalIgnoreCase) || msg.Contains("Syncfusion", StringComparison.OrdinalIgnoreCase))
                {
                    // Log and swallow to prevent fatal shutdown crash
                    Log.Warning(ex, "Non-fatal Syncfusion-related exception occurred during OnExit; swallowing to allow graceful exit");
                }
                else
                {
                    // Unknown exception - log as fatal but do not rethrow to avoid ungraceful termination
                    Log.Fatal(ex, "Unhandled exception during application shutdown");
                }
            }
            finally
            {
                // Ensure logger flush
                try { Log.CloseAndFlush(); } catch { }
            }
        }

        /// <summary>
        /// Gets the active service provider from the current application instance.
        /// </summary>
        /// <returns>The IServiceProvider from the DI container</returns>
        public static IServiceProvider GetActiveServiceProvider()
        {
            var app = Application.Current as App;
            if (app == null)
            {
                throw new InvalidOperationException("Application is not initialized or not of type App");
            }
            return app.Container as IServiceProvider ?? throw new InvalidOperationException("DI container is not available");
        }

        /// <summary>
        /// Gets or sets the splash screen window instance.
        /// </summary>
        public static SplashScreenWindow? SplashScreenInstance { get; private set; }

        /// <summary>
        /// Sets the splash screen window instance.
        /// </summary>
        /// <param name="splashScreen">The splash screen window to set</param>
        public static void SetSplashScreenInstance(SplashScreenWindow splashScreen)
        {
            SplashScreenInstance = splashScreen;
        }
    }
}