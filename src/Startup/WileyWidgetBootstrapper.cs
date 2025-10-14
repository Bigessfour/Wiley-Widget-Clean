using System;
using System.Windows;
using Prism.DryIoc;
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
using WileyWidget.Configuration;

namespace WileyWidget.Startup
{
    /// <summary>
    /// Bootstrapper for WileyWidget application using Prism with DryIoc container.
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

            // Register services using existing patterns from WpfHostingExtensions
            // Note: We need to bridge between Prism's IContainerRegistry and IServiceCollection
            // For now, register key services directly
            
            containerRegistry.RegisterSingleton<ISyncfusionLicenseService, SyncfusionLicenseService>();
            containerRegistry.RegisterSingleton<SyncfusionLicenseState>();
            containerRegistry.RegisterSingleton<ISecretVaultService, LocalSecretVaultService>();
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<ISettingsService>(provider => provider.Resolve<SettingsService>());
            containerRegistry.RegisterSingleton<ThemeManager>();
            containerRegistry.RegisterSingleton<IThemeManager>(provider => provider.Resolve<ThemeManager>());
            
            // Register StartupPerformanceMonitor for telemetry and performance tracking
            containerRegistry.RegisterSingleton<WileyWidget.Diagnostics.StartupPerformanceMonitor>();
            
            // Register Prism DialogService for modal dialogs without owner issues
            containerRegistry.RegisterSingleton<Prism.Dialogs.IDialogService, Prism.Dialogs.DialogService>();
            
            Log.Debug("Registered core services");

            // Register ViewModels
            containerRegistry.RegisterSingleton<MainViewModel>();
            containerRegistry.Register<DashboardViewModel>();
            containerRegistry.Register<MunicipalAccountViewModel>();
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
}
