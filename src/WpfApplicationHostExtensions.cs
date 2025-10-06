using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using WileyWidget.Configuration;
using WileyWidget.Services;
using WileyWidget.Services.Hosting;
using WileyWidget.ViewModels;
using WileyWidget.Models;
using System;
using System.Windows;

namespace WileyWidget;

/// <summary>
/// Extension methods for configuring WPF application services in the Generic Host.
/// </summary>
public static class WpfApplicationHostExtensions
{
    /// <summary>
    /// Configures the WPF application services for the Generic Host.
    /// This includes DI container setup, logging, database services, and hosted services.
    /// </summary>
    public static IHostApplicationBuilder ConfigureWpfApplication(this IHostApplicationBuilder builder)
    {
        // Add configuration
        builder.Services.AddSingleton(builder.Configuration);

        // Configure logging with Serilog
        builder.Services.AddLogging(configure => configure.AddSerilog());

        // Add WPF services
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddSingleton<SplashScreenWindow>(sp =>
        {
            if (App.SplashScreenInstance is { } existing)
            {
                return existing;
            }

            SplashScreenWindow? splash = null;

            void CreateSplash()
            {
                var created = ActivatorUtilities.CreateInstance<SplashScreenWindow>(sp);
                splash = created;
                App.SetSplashScreenInstance(created);
            }

            if (Application.Current?.Dispatcher?.CheckAccess() == true)
            {
                CreateSplash();
            }
            else if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(CreateSplash);
            }
            else
            {
                CreateSplash();
            }

            return splash ?? throw new InvalidOperationException("Failed to create SplashScreenWindow on the UI dispatcher");
        });
        builder.Services.AddTransient<AboutWindow>();
    builder.Services.AddScoped<ViewModels.MainViewModel>();
    builder.Services.AddScoped<ViewModels.AboutViewModel>();
    builder.Services.AddScoped<ViewModels.ReportsViewModel>();
    builder.Services.AddScoped<ViewModels.DashboardViewModel>();
    builder.Services.AddScoped<ViewModels.AnalyticsViewModel>();
    builder.Services.AddScoped<ViewModels.EnterpriseViewModel>();
    builder.Services.AddScoped<ViewModels.BudgetViewModel>();
    builder.Services.AddScoped<ViewModels.AIAssistViewModel>();
    builder.Services.AddScoped<ViewModels.SettingsViewModel>();
    builder.Services.AddScoped<ViewModels.ToolsViewModel>();
    builder.Services.AddScoped<ViewModels.ProgressViewModel>();
    builder.Services.AddScoped<ViewModels.MunicipalAccountViewModel>();

        // Add view manager
        builder.Services.AddSingleton<WileyWidget.Services.IViewManager, WileyWidget.Services.ViewManager>();

        // Add authentication service
        builder.Services.AddSingleton<AuthenticationService>();

        // Add database configuration
        builder.Services.AddEnterpriseDatabaseServices(builder.Configuration);

        // Register HttpClient factory for efficient, pooled HTTP usage across the app
        builder.Services.AddHttpClient("Default", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget/1.0");
        });

        // Configure named HttpClient for AI services
        builder.Services.AddHttpClient("AIServices", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60); // AI requests can take longer
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget-AI/1.0");
        });

        // Configure named HttpClient for external APIs
        builder.Services.AddHttpClient("ExternalAPIs", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WileyWidget-API/1.0");
        });

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            .AddApplicationLifecycleHealthCheck();

        // Add custom health check service
        builder.Services.Configure<HealthCheckConfiguration>(builder.Configuration.GetSection("HealthChecks"));
        builder.Services.AddSingleton<HealthCheckConfiguration>();
        builder.Services.AddSingleton<WileyWidget.Services.HealthCheckService>();

        // Add hosted services
    builder.Services.AddSingleton<BackgroundInitializationService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<BackgroundInitializationService>());
    builder.Services.AddHostedService<HealthCheckHostedService>();
    builder.Services.AddHostedService<HostedWpfApplication>();

        // Add other services
        builder.Services.AddSingleton<ApplicationMetricsService>();
        builder.Services.AddSingleton(SettingsService.Instance);
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddSingleton(ErrorReportingService.Instance);

        // Add AI services
        builder.Services.AddSingleton<IAIService>(sp =>
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            var logger = sp.GetService<ILogger<XAIService>>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<XAIService>.Instance;

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger?.LogWarning("XAI_API_KEY not found. Falling back to NullAIService. See https://docs.x.ai/docs/tutorial for setup guidance.");
                return new NullAIService();
            }

            try
            {
                return new XAIService(apiKey, logger);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to initialize XAIService. Falling back to NullAIService");
                return new NullAIService();
            }
        });

    // Add Grok Supercomputer service
    builder.Services.AddScoped<IGrokSupercomputer, GrokSupercomputer>();

    // Add report export service
    builder.Services.AddSingleton<IReportExportService, ReportExportService>();

        // Add QuickBooks service
        builder.Services.AddSingleton<IQuickBooksService, QuickBooksService>();

        // Add Azure Key Vault service
        builder.Services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();

        // Add threading services for UI thread management
        builder.Services.AddSingleton<WileyWidget.Services.Threading.IDispatcherHelper, WileyWidget.Services.Threading.DispatcherHelper>();
        builder.Services.AddTransient<WileyWidget.Services.Threading.IProgressReporter, WileyWidget.Services.Threading.ProgressReporter>();

        // Add Excel processing services
        builder.Services.AddTransient<WileyWidget.Services.Excel.IExcelReaderService, WileyWidget.Services.Excel.ExcelReaderService>();
        builder.Services.AddTransient<WileyWidget.Services.Excel.IMunicipalBudgetParser, WileyWidget.Services.Excel.MunicipalBudgetParser>();
        builder.Services.AddTransient<WileyWidget.Services.Excel.IBudgetImporter, WileyWidget.Services.Excel.ExcelBudgetImporter>();

        // Add memory cache for performance optimization
        builder.Services.AddMemoryCache();

        return builder;
    }
}