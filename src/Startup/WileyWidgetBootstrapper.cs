using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Prism.Unity;
using Prism.Ioc;
using Prism.Modularity;
using Prism;
using Serilog;
using Syncfusion.SfSkinManager;
using WileyWidget.Services;
using WileyWidget.Services.Excel;
using WileyWidget.Services.Threading;
using WileyWidget.Startup.Modules;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Configuration;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;
using WileyWidget.Models;
using Unity;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Bootstrapper for WileyWidget application using Prism with Unity container.
    /// Coordinates initialization, module loading, and shell creation.
    /// </summary>
    public class WileyWidgetBootstrapper : PrismBootstrapper
    {
        private IConfiguration _configuration;

        protected override DependencyObject CreateShell()
        {
            // Create and return the main shell (MainWindow)
            var shell = Container.Resolve<MainWindow>();
            Log.Information("Main window (shell) created successfully");

            return shell;
        }

        protected override void InitializeShell(DependencyObject shell)
        {
            try
            {
                base.InitializeShell(shell);

                if (shell is Window mainWindow)
                {
                    // Apply Syncfusion theme to the main window
                    try
                    {
                        using var theme = new Theme("FluentDark");
                        SfSkinManager.SetTheme(mainWindow, theme);
                        Log.Information("Applied FluentDark theme to main window");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to apply Syncfusion theme - will use default theme");
                    }

                    // Show the main window
                    mainWindow.Show();
                    Log.Information("Main window displayed");

                    // Close and cleanup splash screen after modules are initialized
                    if (App.SplashScreenInstance != null)
                    {
                        App.SplashScreenInstance.Close();
                        App.SplashScreenInstance = null;
                        Log.Information("Splash screen closed after shell initialization and module loading");
                    }

                    // Validate regions after module initialization
                    try
                    {
                        var regionManager = Container.Resolve<IRegionManager>();
                        var mainRegion = regionManager.Regions["MainRegion"];
                        
                        if (mainRegion == null)
                        {
                            Log.Error("MainRegion not found in RegionManager - views will not be displayed");
                        }
                        else
                        {
                            Log.Information("MainRegion found with {ViewCount} views", mainRegion.Views.Count());
                            
                            if (mainRegion.Views.Count() == 0)
                            {
                                Log.Warning("MainRegion has no views registered - dashboard may be empty");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to validate regions after module initialization");
                    }

                    // Navigate to default view
                    try
                    {
                        // Region navigation is now enabled - DashboardView is registered by DashboardModule
                        var regionManager = Container.Resolve<IRegionManager>();
                        regionManager.RequestNavigate("MainRegion", "DashboardView");
                        Log.Information("Successfully navigated to DashboardView in MainRegion");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to navigate to DashboardView - region may not be ready");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize shell");
                OnStartupException(ex);
                throw; // Re-throw to let Prism handle it
            }
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // Build configuration first
            _configuration = BuildConfiguration();
            
            // Register configuration as singleton
            containerRegistry.RegisterInstance<IConfiguration>(_configuration);
            Log.Debug("Registered IConfiguration");

            // Register Microsoft.Extensions.Logging integration with Serilog
            // This is CRITICAL for ViewModels that inject ILogger<T>
#pragma warning disable CA2000 // Logger factory should live for application lifetime, disposed by container
            var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
#pragma warning restore CA2000
            containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);
            
            // Register generic ILogger<T> so any ViewModel can inject ILogger<TViewModel>
            containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));
            Log.Debug("Registered ILoggerFactory and ILogger<T> for Microsoft.Extensions.Logging integration");

            // Register Prism core services (EventAggregator, RegionManager are auto-registered by Prism)
            // but ensure IEventAggregator is available
            Log.Debug("Prism core services (IEventAggregator, IRegionManager) auto-registered");

            // Register Database Context Factory - simplified approach for Prism
            // Instead of bridging to IServiceCollection, register DbContext directly
            // CRITICAL: Use Register (transient) not RegisterSingleton to avoid disposal issues
            containerRegistry.Register<AppDbContext>(provider =>
            {
                var config = provider.Resolve<IConfiguration>();
                var loggerFactory = provider.Resolve<ILoggerFactory>();
                var connectionString = config.GetConnectionString("DefaultConnection");
                
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                
                if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("Data Source=:memory:"))
                {
                    // Use SQLite in-memory for testing
                    optionsBuilder.UseSqlite("Data Source=:memory:");
                    Log.Warning("Using SQLite in-memory database (no connection string configured)");
                }
                else if (connectionString.Contains(".db") || connectionString.Contains("Data Source="))
                {
                    optionsBuilder.UseSqlite(connectionString);
                    Log.Information("Using SQLite database");
                }
                else
                {
                    optionsBuilder.UseSqlServer(connectionString);
                    Log.Information("Using SQL Server database");
                }
                
                optionsBuilder.UseLoggerFactory(loggerFactory);
                optionsBuilder.EnableSensitiveDataLogging(true);
                optionsBuilder.EnableDetailedErrors(true);
                
                return new AppDbContext(optionsBuilder.Options);
            });
            
            // Register DbContextFactory using a simple wrapper
            containerRegistry.Register<IDbContextFactory<AppDbContext>>(provider =>
            {
                return new PrismDbContextFactory(provider);
            });
            
            Log.Debug("Registered database services with DbContext factory pattern (transient lifetime)");

            // Register core infrastructure services
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, LocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            containerRegistry.RegisterSingleton<ThemeManager>();
            containerRegistry.RegisterSingleton<IThemeManager>(provider => provider.Resolve<ThemeManager>());
            containerRegistry.RegisterSingleton<IDispatcherHelper, DispatcherHelper>();
            
            // Register data repositories - Transient to work with DbContextFactory
            // Each repository resolves a fresh AppDbContext via the factory
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
            
            // Register QuickBooks service (optional - module will handle gracefully if missing)
            containerRegistry.RegisterSingleton<IQuickBooksService, QuickBooksService>();
            
            // Register Excel services
            containerRegistry.RegisterSingleton<IExcelReaderService, ExcelReaderService>();
            
            // Register StartupPerformanceMonitor for telemetry and performance tracking
            containerRegistry.RegisterSingleton<WileyWidget.Diagnostics.StartupPerformanceMonitor>();
            
            // Register Prism DialogService for modal dialogs without owner issues
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            
            Log.Debug("Registered core services and repositories");

            // Register ViewModels - these are registered as transient by default in Prism
            // Remove duplicate registrations - modules will handle their own ViewModels
            containerRegistry.RegisterSingleton<MainViewModel>();
            
            Log.Debug("Registered ViewModels");

            // Register MainWindow as singleton
            containerRegistry.RegisterSingleton<MainWindow>();

            // Views are now registered by their respective modules
            Log.Debug("Main window registered");
        }

        protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
        {
            base.ConfigureRegionAdapterMappings(regionAdapterMappings);

            Log.Debug("Configured region adapter mappings");
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            try
            {
                base.ConfigureModuleCatalog(moduleCatalog);

                // Add modules with proper dependencies and initialization order using ModuleInfo
                // These modules replace the previous IStartupTask pattern

                // Core infrastructure modules (no dependencies)
                var diagnosticsModule = new ModuleInfo
                {
                    ModuleName = typeof(DiagnosticsModule).Name,
                    ModuleType = typeof(DiagnosticsModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable
                };
                moduleCatalog.AddModule(diagnosticsModule);

                var syncfusionModule = new ModuleInfo
                {
                    ModuleName = typeof(SyncfusionModule).Name,
                    ModuleType = typeof(SyncfusionModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable
                };
                moduleCatalog.AddModule(syncfusionModule);

                var settingsModule = new ModuleInfo
                {
                    ModuleName = typeof(SettingsModule).Name,
                    ModuleType = typeof(SettingsModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable
                };
                moduleCatalog.AddModule(settingsModule);

                // Service modules (depend on settings)
                var quickBooksModule = new ModuleInfo
                {
                    ModuleName = typeof(QuickBooksModule).Name,
                    ModuleType = typeof(QuickBooksModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable,
                    DependsOn = { typeof(SettingsModule).Name }
                };
                moduleCatalog.AddModule(quickBooksModule);

                // View modules (depend on infrastructure)
                var dashboardModule = new ModuleInfo
                {
                    ModuleName = typeof(DashboardModule).Name,
                    ModuleType = typeof(DashboardModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable,
                    DependsOn = { typeof(SettingsModule).Name, typeof(SyncfusionModule).Name }
                };
                moduleCatalog.AddModule(dashboardModule);

                var municipalAccountModule = new ModuleInfo
                {
                    ModuleName = typeof(MunicipalAccountModule).Name,
                    ModuleType = typeof(MunicipalAccountModule).AssemblyQualifiedName,
                    InitializationMode = InitializationMode.WhenAvailable,
                    DependsOn = { typeof(SettingsModule).Name, typeof(SyncfusionModule).Name }
                };
                moduleCatalog.AddModule(municipalAccountModule);

                Log.Information("Configured module catalog with 6 modules and explicit dependency relationships");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure module catalog");
                OnStartupException(ex);
                throw; // Re-throw to let Prism handle it
            }
        }

        protected override IModuleCatalog CreateModuleCatalog()
        {
            // Use default module catalog
            var catalog = base.CreateModuleCatalog();
            Log.Debug("Created module catalog");
            return catalog;
        }

        /// <summary>
        /// Handles startup exceptions with proper logging and error reporting.
        /// Specifically handles TargetInvocationException and other critical startup failures.
        /// </summary>
        protected virtual void OnStartupException(Exception ex)
        {
            // Log the exception with full details
            Log.Fatal(ex, "Critical startup exception occurred during bootstrapper initialization");

            // Handle specific exception types
            if (ex is System.Reflection.TargetInvocationException targetEx)
            {
                Log.Fatal(targetEx, "TargetInvocationException during startup - likely module loading failure");
                
                // Try to get the inner exception for more details
                if (targetEx.InnerException != null)
                {
                    Log.Fatal(targetEx.InnerException, "Inner exception details");
                }
            }
            else if (ex is System.IO.FileNotFoundException fileEx)
            {
                Log.Fatal(fileEx, "File not found during startup - check module assemblies");
            }
            else if (ex is System.TypeLoadException typeEx)
            {
                Log.Fatal(typeEx, "Type loading exception during startup - check module dependencies");
            }
            else if (ex is System.MissingMethodException methodEx)
            {
                Log.Fatal(methodEx, "Missing method exception during startup - check module compatibility");
            }

            // Additional context logging
            Log.Fatal("Application startup failed. Container state: {ContainerState}", 
                Container != null ? "Initialized" : "Null");

            // Don't rethrow - let the application handle shutdown gracefully
        }

        /// <summary>
        /// Build configuration from appsettings.json, environment variables, and user secrets.
        /// </summary>
        private IConfiguration BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<App>(optional: true);

            var config = builder.Build();
            Log.Debug("Configuration built successfully");
            return config;
        }
    }

    /// <summary>
    /// Prism-compatible DbContextFactory that creates fresh AppDbContext instances.
    /// Works with Prism's DI container to provide proper EF Core lifetime management.
    /// </summary>
    internal sealed class PrismDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly IContainerProvider _containerProvider;

        public PrismDbContextFactory(IContainerProvider containerProvider)
        {
            _containerProvider = containerProvider ?? throw new ArgumentNullException(nameof(containerProvider));
        }

        /// <summary>
        /// Creates a new AppDbContext instance from the Prism container.
        /// Each call creates a fresh context to avoid disposal issues.
        /// </summary>
        public AppDbContext CreateDbContext()
        {
            return _containerProvider.Resolve<AppDbContext>();
        }

        /// <summary>
        /// Async version of CreateDbContext for compatibility.
        /// </summary>
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateDbContext());
        }
    }
}
