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
        builder.Services.AddScoped<ViewModels.MainViewModel>();

        // Add authentication service
        builder.Services.AddSingleton<AuthenticationService>();

        // Add database configuration
        builder.Services.AddEnterpriseDatabaseServices(builder.Configuration);

        // Register HttpClient factory for efficient, pooled HTTP usage across the app
        builder.Services.AddHttpClient();

        // Add health checks
        builder.Services.AddHealthChecks()
            .AddResourceUtilizationHealthCheck()
            .AddApplicationLifecycleHealthCheck();

        // Add custom health check service
        builder.Services.Configure<HealthCheckConfiguration>(builder.Configuration.GetSection("HealthChecks"));
        builder.Services.AddSingleton<HealthCheckConfiguration>();
        builder.Services.AddSingleton<WileyWidget.Services.HealthCheckService>();

        // Add hosted services
        builder.Services.AddHostedService<HostedWpfApplication>();
        builder.Services.AddHostedService<HealthCheckHostedService>();
        builder.Services.AddHostedService<BackgroundInitializationService>();
        builder.Services.AddSingleton<BackgroundInitializationService>();

        // Add other services
        builder.Services.AddSingleton<ApplicationMetricsService>();
        builder.Services.AddSingleton(SettingsService.Instance);
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddSingleton(ErrorReportingService.Instance);

        // Add AI services
        builder.Services.AddSingleton<IAIService>(sp =>
        {
            var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            var logger = sp.GetService<ILogger<XAIService>>();
            return new XAIService(apiKey, logger);
        });
        builder.Services.AddSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();

        // Add QuickBooks service
        builder.Services.AddSingleton<IQuickBooksService, QuickBooksService>();
        builder.Services.AddSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();

        // Add Azure Key Vault service
        builder.Services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();

        return builder;
    }
}