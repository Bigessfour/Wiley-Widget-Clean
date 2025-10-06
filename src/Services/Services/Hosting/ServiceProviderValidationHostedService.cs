using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Services;

namespace WileyWidget.Services.Hosting;

/// <summary>
/// Hosted service that validates critical dependency injection registrations during startup.
/// Aligns with Microsoft DI guidance to fail fast when required services are missing or
/// misconfigured (see https://learn.microsoft.com/dotnet/core/extensions/dependency-injection-guidelines#recommendations).
/// </summary>
public sealed class ServiceProviderValidationHostedService : IHostedService
{
    private static readonly IReadOnlyList<Type> CriticalServices = new[]
    {
        typeof(IViewManager),
        typeof(IDbContextFactory<AppDbContext>),
        typeof(IAIService),
        typeof(IQuickBooksService),
        typeof(IReportExportService),
        typeof(IChargeCalculatorService),
        typeof(IWhatIfScenarioEngine),
        typeof(IGrokSupercomputer),
        typeof(ApplicationMetricsService),
        typeof(HealthCheckService)
    };

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceProviderValidationHostedService> _logger;

    public ServiceProviderValidationHostedService(
        IServiceProvider serviceProvider,
        ILogger<ServiceProviderValidationHostedService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        using var validationScope = _serviceProvider.CreateScope();
        foreach (var serviceType in CriticalServices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                _ = validationScope.ServiceProvider.GetRequiredService(serviceType);
                _logger.LogDebug("Validated DI registration for {ServiceType}", serviceType.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Dependency injection validation failed for {ServiceType}", serviceType.FullName);
                throw new InvalidOperationException($"Failed to resolve required service {serviceType.FullName}.", ex);
            }
        }

        _logger.LogInformation("Dependency injection validation completed successfully.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
