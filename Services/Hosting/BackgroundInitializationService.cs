using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace WileyWidget.Services.Hosting;

/// <summary>
/// Performs background initialization tasks after the host starts: database migrations,
/// Azure-related setup, and any light-weight warmups. This keeps App.xaml.cs lean.
/// </summary>
public class BackgroundInitializationService : BackgroundService
{
    private readonly ILogger<BackgroundInitializationService> _logger;
    private readonly IServiceProvider _services;

    public BackgroundInitializationService(
        ILogger<BackgroundInitializationService> logger,
        IServiceProvider services)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting background initialization tasks...");

            // 1) Ensure database is created/migrated
            await WileyWidget.Configuration.DatabaseConfiguration.EnsureDatabaseCreatedAsync(_services);

            // 2) Validate database schema
            await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(_services);

            // 3) Placeholder for Azure services initialization
            await InitializeAzureAsync(stoppingToken);

            _logger.LogInformation("Background initialization completed");
        }
        catch (OperationCanceledException)
        {
            // Shutdown requested
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background initialization failed");
        }
    }

    private Task InitializeAzureAsync(CancellationToken token)
    {
        // Add additional Azure initialization here as needed
        return Task.CompletedTask;
    }
}
