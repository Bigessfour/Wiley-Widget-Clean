using Prism.Ioc;
using Serilog;
using System;
using System.Windows;
using WileyWidget.Startup;

namespace WileyWidget
{
    /// <summary>
    /// WPF Application class using Prism Bootstrapper pattern.
    /// Application initialization is delegated to WileyWidgetBootstrapper.
    /// </summary>
    public class App : Application
    {
        private WileyWidgetBootstrapper _bootstrapper;

        // Static property for backwards compatibility with views
        public static IContainerProvider CurrentContainer { get; private set; }

        // Splash screen instance for bootstrapper access
        public static Window? SplashScreenInstance { get; set; }

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
        public static void SetSplashScreenInstance(Window window) => SplashScreenInstance = window;
        public static object StartupProgress { get; set; }
        public static void UpdateLatestHealthReport(object report) { /* Stub */ }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configure Serilog before bootstrapper initialization
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

            Log.Information("=== WileyWidget Application Starting (Bootstrapper Pattern) ===");

            try
            {
                // Show splash screen before bootstrapper initialization
                var splashScreen = new SplashScreenWindow();
                splashScreen.Show();
                SplashScreenInstance = splashScreen; // Store reference for bootstrapper
                Log.Information("Splash screen displayed before bootstrapper initialization");

                // Create and run the bootstrapper
                _bootstrapper = new WileyWidgetBootstrapper();
                _bootstrapper.Run();

                // Store container reference for backwards compatibility
                CurrentContainer = _bootstrapper.Container;

                Log.Information("Bootstrapper initialization completed successfully");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to initialize application bootstrapper");
                MessageBox.Show(
                    $"Failed to start application: {ex.Message}",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application shutting down");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}