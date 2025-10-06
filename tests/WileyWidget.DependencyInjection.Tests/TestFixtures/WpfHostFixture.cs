using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WileyWidget.Configuration;

namespace WileyWidget.DependencyInjection.Tests.TestFixtures;

/// <summary>
/// Shared test fixture for WPF host configuration.
/// Creates a single instance of the configured host that is shared across all tests in the collection.
/// Implements IDisposable for proper cleanup.
/// </summary>
public sealed class WpfHostFixture : IDisposable
{
    public IHost Host { get; }
    public IServiceProvider ServiceProvider => Host.Services;

    public WpfHostFixture()
    {
        // Clear any Application Insights environment variables that might interfere with tests
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", null);
        Environment.SetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", null);
        
        var hostBuilder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        
        // Configure test-specific settings
        hostBuilder.Configuration.Sources.Clear();
        hostBuilder.Configuration
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("testappsettings.json", optional: false, reloadOnChange: false);
            // Intentionally NOT adding environment variables to avoid interference

        // Apply WPF application configuration
        hostBuilder.ConfigureWpfApplication();

        // Build the host
        Host = hostBuilder.Build();
    }

    public void Dispose()
    {
        Host?.Dispose();
    }
}
