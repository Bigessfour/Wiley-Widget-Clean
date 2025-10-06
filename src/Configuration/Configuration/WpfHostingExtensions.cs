using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Services.Hosting;
using WileyWidget.Services.Caching;
using WileyWidget.Services.Threading;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Configuration;
using WileyWidget.Data;
using Serilog;
using Serilog.Debugging;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using WileyWidget.Services.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WileyWidget.Configuration;

/// <summary>
/// Extension methods for configuring WPF applications with the Generic Host pattern.
/// Provides a clean, enterprise-grade setup for WPF applications following Microsoft's
/// recommended hosting patterns.
/// </summary>
public static class WpfHostingExtensions
{
    public static TelemetryConfiguration? TelemetryConfiguration { get; set; }

    /// <summary>
    /// Configures the host application builder for a WPF application with enterprise-grade services.
    /// This method sets up configuration, logging, dependency injection, and WPF-specific services.
    /// </summary>
    /// <param name="builder">The host application builder to configure</param>
    /// <returns>The configured host application builder for method chaining</returns>
    public static IHostApplicationBuilder ConfigureWpfApplication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Configure configuration sources with proper hierarchy
        ConfigureApplicationConfiguration(builder);

        // Configure enterprise logging
        ConfigureApplicationLogging(builder);

    // Configure core application services
    ConfigureCoreServices(builder.Services, builder.Configuration);

    // Configure background hosted services before WPF hosted service registration
    ConfigureHostedServices(builder.Services);

    // Configure WPF-specific services
    ConfigureWpfServices(builder.Services);

    // Configure database services
    builder.Services.AddEnterpriseDatabaseServices(builder.Configuration);

        return builder;
    }

    /// <summary>
    /// Configures the application configuration sources with proper hierarchy and validation.
    /// </summary>
    private static void ConfigureApplicationConfiguration(IHostApplicationBuilder builder)
    {
        // Configuration is automatically set up by the host builder, but we can add additional sources
        builder.Configuration.Sources.Clear();

        var environmentName = builder.Environment.EnvironmentName;
        Log.Information("🔧 Configuring application settings - Environment: {Environment}, IsDevelopment: {IsDevelopment}",
            environmentName, builder.Environment.IsDevelopment());
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine("config", "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine("config", $"appsettings.{environmentName}.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        Log.Debug("🔧 Configuration sources added - Base appsettings, environment-specific, and environment variables");

        // Add user secrets in development
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<App>();
            Log.Debug("🔧 User secrets added for development environment");
        }

        // After base config assembled, attempt to add Azure Key Vault provider if a vault name is configured.
        var tempConfig = builder.Configuration.Build();
        var keyVaultName = tempConfig["Azure:KeyVaultName"]; // expected config key
        if (!string.IsNullOrWhiteSpace(keyVaultName))
        {
            try
            {
                // TODO: Re-implement Azure Key Vault configuration for WPF apps
                // The AddAzureKeyVault method from ASP.NET Core is not available in desktop apps
                // Need to implement custom Key Vault configuration provider
                Log.Warning("Azure Key Vault configuration temporarily disabled for WPF compatibility");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add Azure Key Vault configuration provider for vault {Vault}", keyVaultName);
            }
        }

        Log.Information("✅ Application configuration setup completed");
    }

    /// <summary>
    /// Configures enterprise-grade logging using Serilog with Microsoft's documented pattern.
    /// Uses Serilog integration with HostApplicationBuilder for proper integration.
    /// </summary>
    private static void ConfigureApplicationLogging(IHostApplicationBuilder builder)
    {
        var contentRoot = builder.Environment.ContentRootPath ?? Directory.GetCurrentDirectory();
        var logsDirectory = Path.Combine(contentRoot, "logs");
        try
        {
            Directory.CreateDirectory(logsDirectory);
        }
        catch (Exception directoryEx)
        {
            // Fall back to temp folder if we cannot create the directory
            var tempLogsDirectory = Path.Combine(Path.GetTempPath(), "WileyWidget", "logs");
            Directory.CreateDirectory(tempLogsDirectory);
            logsDirectory = tempLogsDirectory;
            Console.WriteLine($"[Bootstrap] Failed to create logs directory at '{contentRoot}'; using fallback '{logsDirectory}'. Exception: {directoryEx.Message}");
        }

        var selfLogPath = Path.Combine(logsDirectory, "serilog-selflog.txt");
        SelfLog.Enable(message =>
        {
            try
            {
                File.AppendAllText(selfLogPath, message + Environment.NewLine);
            }
            catch
            {
                // Ignore failures writing self-log to avoid recursive errors
            }
        });

    var serilogSection = builder.Configuration.GetSection("Serilog");
    var firstSinkName = builder.Configuration["Serilog:WriteTo:0:Name"];
    Console.WriteLine($"[Bootstrap] Serilog configuration section found: {serilogSection.Exists()}");
    Console.WriteLine($"[Bootstrap] Serilog WriteTo[0] Name from configuration: {firstSinkName}");

    if (!serilogSection.Exists() || string.IsNullOrWhiteSpace(firstSinkName))
    {
        Console.WriteLine("[Bootstrap] Serilog configuration appears missing or invalid. Current configuration view:");

        if (builder.Configuration is IConfigurationRoot configurationRoot)
        {
            Console.WriteLine(configurationRoot.GetDebugView());
        }
        else
        {
            Console.WriteLine(builder.Configuration.Build().GetDebugView());
        }
    }

        // Microsoft documented pattern: Create configured logger and set as global
        var configuredLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console()
            .CreateLogger();

        // Replace the bootstrap logger with the configured logger
        Log.Logger = configuredLogger;

        // Clear providers and add the configured Serilog
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(configuredLogger, dispose: true);

        Log.Information("Serilog logging configured from configuration sources for {Environment} environment", 
            builder.Environment.EnvironmentName);

        // Application Insights (optional) - support ConnectionString or InstrumentationKey
        var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
        var aiInstrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"]; // legacy
        if (!string.IsNullOrWhiteSpace(aiConnectionString) || !string.IsNullOrWhiteSpace(aiInstrumentationKey))
        {
            var isDevelopment = builder.Environment.IsDevelopment();

            // Create + register TelemetryConfiguration manually for non-ASP.NET WPF host
            builder.Services.AddSingleton(sp =>
            {
                var cfg = Microsoft.ApplicationInsights.Extensibility.TelemetryConfiguration.CreateDefault();
                if (!string.IsNullOrWhiteSpace(aiConnectionString))
                    cfg.ConnectionString = aiConnectionString;
                else if (!string.IsNullOrWhiteSpace(aiInstrumentationKey))
                    cfg.ConnectionString = $"InstrumentationKey={aiInstrumentationKey}"; // legacy support

                if (cfg.TelemetryChannel != null && isDevelopment)
                {
                    // Fast flush & no sampling in dev
                    cfg.TelemetryChannel.DeveloperMode = true;
                }
                return cfg;
            });

            // TelemetryClient
            builder.Services.AddSingleton(sp => new TelemetryClient(sp.GetRequiredService<TelemetryConfiguration>()));

            // Initializer + startup hosted service
            builder.Services.AddSingleton<ITelemetryInitializer, WileyWidget.Services.Telemetry.EnvironmentTelemetryInitializer>();
            builder.Services.AddHostedService<WileyWidget.Services.Telemetry.TelemetryStartupService>();

            Log.Information("Application Insights telemetry configured (Environment: {Environment}, DeveloperMode: {DeveloperMode})", 
                builder.Environment.EnvironmentName, isDevelopment);
        }
    }

    /// <summary>
    /// Configures core application services including authentication and configuration options.
    /// </summary>
    private static void ConfigureCoreServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration as singleton
        services.AddSingleton(configuration);

        // Configure strongly-typed configuration options
        services.Configure<DatabaseOptions>(configuration.GetSection("Database"));
        services.Configure<AzureOptions>(configuration.GetSection("Azure"));
        services.Configure<SyncfusionOptions>(configuration.GetSection("Syncfusion"));

        // Register authentication service
        services.AddSingleton<AuthenticationService>();

        // Register Azure services
        services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();

        // Register application metrics service
        services.AddSingleton<ApplicationMetricsService>();

        // Register localization service
        services.AddSingleton<LocalizationService>();

    // Register HttpClient factory for efficient, pooled HTTP usage across the app
    services.AddHttpClient();

        // Register health check services
        services.AddSingleton<WileyWidget.Services.HealthCheckService>();
        services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            .AddApplicationLifecycleHealthCheck();

        // Note: Performance optimization services will be added after namespace compilation is resolved
        // services.AddSingleton<StartupCacheService>();
        // services.AddHostedService<ParallelStartupService>();
    }

    /// <summary>
    /// Configures WPF-specific services including windows and view models.
    /// </summary>
    private static void ConfigureWpfServices(IServiceCollection services)
    {
        // Register main window and view models
    services.AddTransient<MainWindow>();
    services.AddTransient<ViewModels.MainViewModel>(sp => ActivatorUtilities.CreateInstance<ViewModels.MainViewModel>(sp, false));
    services.AddTransient<ViewModels.ProgressViewModel>();
    services.AddTransient<ViewModels.ToolsViewModel>();

        // Register other view models as needed
        // services.AddTransient<OtherViewModel>();

        // Register the WPF application hosted service
        services.AddHostedService<HostedWpfApplication>();
    }

    /// <summary>
    /// Configures background hosted services for enterprise operations.
    /// </summary>
    private static void ConfigureHostedServices(IServiceCollection services)
    {
    services.AddSingleton<BackgroundInitializationService>();
    services.AddHostedService(sp => sp.GetRequiredService<BackgroundInitializationService>());

    NewMethod(services);
    }

    private static void NewMethod(IServiceCollection services)
    {
        // Background services will be added in later phases
        // services.AddHostedService<LicenseManagementService>();
        // services.AddHostedService<AzureIntegrationService>();

        // Run health checks during startup and periodically if desired
        services.AddHostedService<HealthCheckHostedService>();
    }
}