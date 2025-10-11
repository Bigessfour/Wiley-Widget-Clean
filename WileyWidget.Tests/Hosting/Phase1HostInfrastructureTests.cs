using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Services.Hosting;
using Xunit;

namespace WileyWidget.Tests.Hosting;

/// <summary>
/// Tests for Phase 1 implementation of the Enterprise Host Infrastructure.
/// </summary>
public class Phase1HostInfrastructureTests : TestApplication
{
    [StaFact]
    public async Task HostBuilder_ShouldConfigureServicesCorrectly()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.ConfigureWpfApplication();
        var host = builder.Build();

        try
        {
            // Assert
            Assert.NotNull(host);
            Assert.NotNull(host.Services);

            // Verify core services are registered
            var serviceProvider = host.Services.GetService<IServiceProvider>();
            Assert.NotNull(serviceProvider);

            var logger = host.Services.GetService<ILogger<Phase1HostInfrastructureTests>>();
            Assert.NotNull(logger);

            // Verify hosted service is registered
            var hostedServices = host.Services.GetServices<IHostedService>();
            Assert.Contains(hostedServices, service => service is HostedWpfApplication);
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    [StaFact]
    public void WpfHostingExtensions_ShouldRegisterRequiredServices()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();

        // Act
        builder.ConfigureWpfApplication();
        var host = builder.Build();

        try
        {
            // Assert - Test specific service registrations
            var authService = host.Services.GetService<WileyWidget.Services.AuthenticationService>();
            Assert.NotNull(authService);

            var healthCheckService = host.Services.GetService<WileyWidget.Services.HealthCheckService>();
            Assert.NotNull(healthCheckService);
        }
        finally
        {
            host.Dispose();
        }
    }

    [StaFact]
    public void ConfigurationOptions_ShouldBindCorrectly()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.ConfigureWpfApplication();
        var host = builder.Build();

        try
        {
            // Act
            var databaseOptions = host.Services.GetService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>();
            var syncfusionOptions = host.Services.GetService<Microsoft.Extensions.Options.IOptions<SyncfusionOptions>>();

            // Assert
            Assert.NotNull(databaseOptions);
            Assert.NotNull(syncfusionOptions);
        }
        finally
        {
            host.Dispose();
        }
    }
}