using System;
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
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using WileyWidget.Services.Telemetry;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Azure.Extensions.AspNetCore.Configuration.Secrets;

namespace WileyWidget.Configuration;

/// <summary>
/// Extension methods for configuring WPF applications with the Generic Host pattern.
/// Provides a clean, enterprise-grade setup for WPF applications following Microsoft's
/// recommended hosting patterns.
/// </summary>
public static class WpfHostingExtensions
{
    public static object TelemetryConfiguration { get; private set; }

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

        // Configure WPF-specific services
        ConfigureWpfServices(builder.Services);

        // Configure database services
        builder.Services.AddEnterpriseDatabaseServices(builder.Configuration);

        // Configure background hosted services
        ConfigureHostedServices(builder.Services);

        return builder;
    }

    /// <summary>
    /// Configures the application configuration sources with proper hierarchy and validation.
    /// </summary>
    private static void ConfigureApplicationConfiguration(IHostApplicationBuilder builder)
    {
        // Configuration is automatically set up by the host builder, but we can add additional sources
        builder.Configuration.Sources.Clear();
        
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();

        // Add user secrets in development
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<App>();
        }

        // After base config assembled, attempt to add Azure Key Vault provider if a vault name is configured.
        var tempConfig = builder.Configuration.Build();
        var keyVaultName = tempConfig["Azure:KeyVaultName"]; // expected config key
        if (!string.IsNullOrWhiteSpace(keyVaultName))
        {
            try
            {
                var vaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
                var credential = new DefaultAzureCredential();
                var secretClient = new SecretClient(vaultUri, credential);
                builder.Configuration.AddAzureKeyVault(secretClient, new KeyVaultSecretManager());
                Log.Information("Azure Key Vault configuration provider added for vault {Vault}", keyVaultName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to add Azure Key Vault configuration provider for vault {Vault}", keyVaultName);
            }
        }
    }

    /// <summary>
    /// Configures enterprise-grade logging using Serilog.
    /// </summary>
    private static void ConfigureApplicationLogging(IHostApplicationBuilder builder)
    {
        // Configure Serilog from configuration
        var logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        // Replace the default logging with Serilog
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(logger);

        // Set the global Serilog logger
        Log.Logger = logger;

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
    services.AddTransient<ViewModels.MainViewModel>();
    services.AddTransient<DashboardView>();
    services.AddTransient<DashboardViewModel>();

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
        NewMethod(services);

        // Perform background initialization tasks (database migrations, Azure init)
        services.AddHostedService<BackgroundInitializationService>();
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

/// <summary>
/// Configuration options for database settings.
/// </summary>
public class DatabaseOptions
{
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Configuration options for Azure integration settings.
/// </summary>
public class AzureOptions
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string SqlServer { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
}

/// <summary>
/// Configuration options for Syncfusion licensing.
/// </summary>
public class SyncfusionOptions
{
    public string LicenseKey { get; set; } = string.Empty;
}