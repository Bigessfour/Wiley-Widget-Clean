using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.ApplicationInsights.Extensibility;
using WileyWidget.Configuration;

namespace WileyWidget.DependencyInjection.Tests.TestFixtures;

/// <summary>
/// WPF Test Factory following Microsoft's WebApplicationFactory pattern.
/// Creates a properly configured WPF host for testing with disabled telemetry
/// and in-memory database configuration.
/// </summary>
public class WpfTestFactory : IDisposable
{
    private IHost? _host;
    private bool _disposed;

    public IServiceProvider Services => _host?.Services ?? throw new InvalidOperationException("Factory not initialized");

    public WpfTestFactory()
    {
        InitializeHost();
    }

    private void InitializeHost()
    {
        // Clear any Application Insights environment variables that might interfere
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", null);

        var hostBuilder = Host.CreateApplicationBuilder();

        // Set test environment to ensure proper test configuration
        hostBuilder.Environment.EnvironmentName = "Test";

        // Configure test-specific settings
        hostBuilder.Configuration.Sources.Clear();
        hostBuilder.Configuration
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("testappsettings.json", optional: false, reloadOnChange: false);
            // Intentionally NOT adding environment variables to avoid interference

        // Apply WPF application configuration
        hostBuilder.ConfigureWpfApplication();

        // Override services for testing following Microsoft's ConfigureTestServices pattern
        ConfigureTestServices(hostBuilder);

        // Build the host
        _host = hostBuilder.Build();
    }

    /// <summary>
    /// Configures services specifically for testing, following Microsoft's integration testing patterns.
    /// This replaces production services with test-friendly alternatives.
    /// </summary>
    private void ConfigureTestServices(IHostApplicationBuilder builder)
    {
        // Remove Application Insights and replace with disabled telemetry for testing
        var telemetryDescriptor = builder.Services.SingleOrDefault(
            d => d.ServiceType == typeof(TelemetryConfiguration));
        if (telemetryDescriptor != null)
        {
            builder.Services.Remove(telemetryDescriptor);

            // Add disabled telemetry for tests (Microsoft recommended approach)
            builder.Services.AddSingleton(sp =>
            {
                var cfg = new TelemetryConfiguration();
                cfg.DisableTelemetry = true;
                return cfg;
            });
        }

        // Register test-specific services that aren't in production
        builder.Services.AddScoped<WileyWidget.DependencyInjection.Tests.ConstructorResolutionTests.SimpleTestViewModelWithOptional>();

        // Replace database with in-memory database for testing
        // Note: This would need to be implemented based on your actual database configuration
        // For now, we'll rely on the testappsettings.json configuration
    }

    public void Dispose()
    {
        if (_disposed) return;

        _host?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}