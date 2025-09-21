using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Services.Hosting;
using WileyWidget.ViewModels;
using WileyWidget.Services;
using WileyWidget.Configuration;
using WileyWidget.Data;
using Serilog;

namespace WileyWidget.Configuration;

/// <summary>
/// Extension methods for configuring WPF applications with the Generic Host pattern.
/// Provides a clean, enterprise-grade setup for WPF applications following Microsoft's
/// recommended hosting patterns.
/// </summary>
public static class WpfHostingExtensions
{
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

        // Register health check services
        services.AddSingleton<WileyWidget.Services.HealthCheckService>();
        services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            .AddApplicationLifecycleHealthCheck();
    }

    /// <summary>
    /// Configures WPF-specific services including windows and view models.
    /// </summary>
    private static void ConfigureWpfServices(IServiceCollection services)
    {
        // Register main window and view models
        services.AddTransient<MainWindow>();
        services.AddTransient<ViewModels.MainViewModel>();

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
        // Background services will be added in later phases
        // services.AddHostedService<LicenseManagementService>();
        // services.AddHostedService<HealthMonitoringService>();
        // services.AddHostedService<AzureIntegrationService>();
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