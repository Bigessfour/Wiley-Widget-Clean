using Prism.Ioc;
using Serilog;
using Syncfusion.SfSkinManager;
using System;
using System.Windows;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using Prism.DryIoc;

namespace WileyWidget
{
    public class App : Prism.DryIoc.PrismApplication
    {
        private Window _splashScreen;

        // Static property for backwards compatibility with views
        public static IContainerProvider CurrentContainer { get; private set; }

        // Helper method for views that need service resolution
        public static IServiceProvider GetActiveServiceProvider()
        {
            if (CurrentContainer == null)
                throw new InvalidOperationException("Application container not initialized");
            
            // Prism's container implements IServiceProvider
            return CurrentContainer as IServiceProvider ?? 
                   throw new InvalidOperationException("Container does not implement IServiceProvider");
        }

        // Stub methods for backwards compatibility (will be removed during full refactor)
        public static IServiceProvider ServiceProvider => GetActiveServiceProvider();
        public static void LogDebugEvent(string category, string message) => Log.Debug("[{Category}] {Message}", category, message);
        public static void LogStartupTiming(string message, TimeSpan elapsed) => Log.Debug("{Message} completed in {Ms}ms", message, elapsed.TotalMilliseconds);
        public static Window SplashScreenInstance { get; set; }
        public static void SetSplashScreenInstance(Window window) => SplashScreenInstance = window;
        public static object StartupProgress { get; set; }
        public static void UpdateLatestHealthReport(object report) { /* Stub */ }

        protected override Window CreateShell()
        {
            _splashScreen = new SplashScreenWindow();
            _splashScreen.Show();
            Log.Information("Splash screen shown");
            
            // Store container reference for static access
            CurrentContainer = Container;
            
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<SettingsService>();
            containerRegistry.RegisterSingleton<MainViewModel>();
            containerRegistry.Register<DashboardViewModel>();
            containerRegistry.Register<MunicipalAccountViewModel>();
            containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>();
            containerRegistry.RegisterForNavigation<MunicipalAccountView, MunicipalAccountViewModel>();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            
            // Apply theme to main window after it's created
            var mainWindow = MainWindow;
            if (mainWindow != null)
            {
                SfSkinManager.SetTheme(mainWindow, new Syncfusion.SfSkinManager.Theme("FluentDark"));
                Log.Information("Applied FluentDark theme");
            }

            var regionManager = Container.Resolve<IRegionManager>();
            regionManager.RequestNavigate("MainRegion", "DashboardView");
            Log.Information("Navigated to DashboardView");

            _splashScreen.Close();
            _splashScreen = null;
            Log.Information("Splash screen closed");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File("logs/wiley-widget-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}