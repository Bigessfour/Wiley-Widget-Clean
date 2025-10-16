using System.Windows;
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

        // Helper method for views that need service resolution
        public static IServiceProvider GetActiveServiceProvider()
        {
            lock (_containerLock)
            {
                if (CurrentContainer == null)
                {
                    Log.Error("Application container not initialized");
                    throw new InvalidOperationException("Application container not initialized");
                }
                
                Log.Debug("Returning active service provider");
                // Prism's container implements IServiceProvider
                return CurrentContainer as IServiceProvider ?? 
                       throw new InvalidOperationException("Container does not implement IServiceProvider");
            }
        }

        // Stub methods for backwards compatibility (will be removed during full refactor)
        private static IServiceProvider _serviceProvider;
        public static IServiceProvider ServiceProvider
        {
            get
            {
                lock (_initLock)
                {
                    if (_serviceProvider == null)
                    {
                        _serviceProvider = GetActiveServiceProvider();
                        Log.Information("ServiceProvider initialized lazily with thread-safety");
                    }
                }
                return _serviceProvider;
            }
            set
            {
                lock (_initLock)
                {
                    _serviceProvider = value;
                    Log.Information("ServiceProvider set explicitly with thread-safety");
                }
            }
        }
        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        public static void SetSplashScreenInstance(Window window) => SplashScreenInstance = window;
        public static object StartupProgress { get; set; }
        public static void UpdateLatestHealthReport(object report) { /* Stub */ }

        protected override void OnStartup(StartupEventArgs e)
        {
            ConfigureLogging();

            // Syncfusion setup (from Syncfusion WPF docs: Register license before any controls load)
            SyncfusionLicenseProvider.RegisterLicense("YOUR_SYNCFUSION_LICENSE_KEY_HERE");  // Replace with your actual key

            // Initialize ServiceProvider early to prevent "Application container not initialized" errors
            InitializeInternal();

            base.OnStartup(e);

            // Ensure ServiceProvider is set after bootstrapper initialization
            ServiceProvider = CurrentContainer as IServiceProvider;
            Log.Information("ServiceProvider initialized early in OnStartup after base initialization");
        }

        private void InitializeInternal()
        {
            // Early initialization placeholder - ensures ServiceProvider is accessed before potential issues
            // The lazy initialization in ServiceProvider property handles the actual setup
            Log.Debug("InitializeInternal called for early container access");
        }

        protected override Window CreateShell()
        {
            // CRITICAL: Set CurrentContainer BEFORE resolving shell to ensure views can access it
            CurrentContainer = Container;
            
            return Container.Resolve<MainWindow>();
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
            // Build configuration first
            var configuration = BuildConfiguration();
            
            // Register configuration as singleton
            containerRegistry.RegisterInstance<IConfiguration>(configuration);

            // Register Microsoft.Extensions.Logging integration with Serilog
#pragma warning disable CA2000
            var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, LocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            containerRegistry.RegisterSingleton<ThemeManager>();
            containerRegistry.RegisterSingleton<IThemeManager>(provider => provider.Resolve<ThemeManager>());
            containerRegistry.RegisterSingleton<IDispatcherHelper>(provider => new DispatcherHelper());
            
            // Register data repositories
            containerRegistry.Register<IEnterpriseRepository, WileyWidget.Data.EnterpriseRepository>();
            containerRegistry.Register<IUtilityCustomerRepository, WileyWidget.Data.UtilityCustomerRepository>();
            containerRegistry.Register<IMunicipalAccountRepository, WileyWidget.Data.MunicipalAccountRepository>();
            containerRegistry.Register<IUnitOfWork, WileyWidget.Data.UnitOfWork>();
            
            // Register business services
            containerRegistry.RegisterSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();
            containerRegistry.RegisterSingleton<FiscalYearSettings>();
            containerRegistry.RegisterSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
            
            // Register AI services
            containerRegistry.RegisterSingleton<IAIService, XAIService>();
            
            // Register QuickBooks service
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
            
            // Register Excel services
            containerRegistry.RegisterSingleton<IExcelReaderService, ExcelReaderService>();
            
            // Register report export service
            containerRegistry.RegisterSingleton<IReportExportService, ReportExportService>();
            
            // Register budget repository
            containerRegistry.Register<IBudgetRepository, WileyWidget.Data.BudgetRepository>();
            
            // Register audit repository
            containerRegistry.Register<IAuditRepository, WileyWidget.Data.AuditRepository>();
            
            // Register StartupPerformanceMonitor
            containerRegistry.RegisterSingleton<WileyWidget.Diagnostics.StartupPerformanceMonitor>();
            
            // Register Prism DialogService
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            
            // Register ViewModels
            containerRegistry.RegisterSingleton<MainViewModel>(provider => new MainViewModel(provider.Resolve<IRegionManager>(), provider.Resolve<IDispatcherHelper>(), provider.Resolve<ILogger<MainViewModel>>(), provider.Resolve<IEnterpriseRepository>(), provider.Resolve<IExcelReaderService>(), provider.Resolve<IReportExportService>(), provider.Resolve<IBudgetRepository>()));
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
            CurrentContainer = containerRegistry as IContainerProvider;
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
            base.OnInitialized();

            // CurrentContainer is already set in CreateShell()
            // This ensures it's available during shell construction

            Application.Current.MainWindow?.Show();

            if (SplashScreenInstance != null)
            {
                SplashScreenInstance.Close();
                SplashScreenInstance = null;
            }

            try
            {
                // var regionManager = Container.Resolve<IRegionManager>();
                // regionManager.RequestNavigate("MainRegion", "DashboardView");
                // Log.Information("Navigated to DashboardView during application initialization");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to navigate to DashboardView during startup");
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