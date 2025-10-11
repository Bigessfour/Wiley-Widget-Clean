using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Services;

namespace WileyWidget.Startup;

/// <summary>
/// Validates QuickBooks Online configuration and ensures service is ready for use.
/// </summary>
public sealed class QuickBooksStartupTask : IStartupTask
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuickBooksStartupTask> _logger;

    public QuickBooksStartupTask(
        IServiceScopeFactory scopeFactory,
        ILogger<QuickBooksStartupTask> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => "QuickBooks Online initialization";

    public int Order => 300; // Run after settings but before UI

    public async Task ExecuteAsync(StartupTaskContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();
        context.ProgressReporter.Report(75, "Initializing QuickBooks Online...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var quickBooksService = scope.ServiceProvider.GetRequiredService<IQuickBooksService>();
            var secretVaultService = scope.ServiceProvider.GetService<ISecretVaultService>();

            _logger.LogInformation("Starting QuickBooks Online service initialization");

            // Test secret vault connectivity for QBO secrets
            if (secretVaultService != null)
            {
                var svTestResult = await secretVaultService.TestConnectionAsync();
                if (svTestResult)
                {
                    _logger.LogInformation("Secret vault connection verified for QBO secrets");
                }
                else
                {
                    _logger.LogWarning("Secret vault not available - QBO secrets will be loaded from environment variables");
                }
            }

            // Test QBO service instantiation (this validates credentials are loaded)
            _logger.LogInformation("QuickBooks service instantiated successfully");

            // Test basic connectivity (lightweight test)
            try
            {
                var connectionTest = await quickBooksService.TestConnectionAsync();
                if (connectionTest)
                {
                    _logger.LogInformation("QuickBooks Online API connection test successful");
                    context.ProgressReporter.Report(80, "QuickBooks Online connected...");
                }
                else
                {
                    _logger.LogWarning("QuickBooks Online API connection test failed - may require user authentication");
                    context.ProgressReporter.Report(80, "QuickBooks Online requires authentication...");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QuickBooks Online connection test failed - service may not be fully configured yet");
                context.ProgressReporter.Report(80, "QuickBooks Online configuration pending...");
            }

            _logger.LogInformation("QuickBooks Online initialization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize QuickBooks Online service");
            // Don't throw - QBO is not critical for app startup
            context.ProgressReporter.Report(80, "QuickBooks Online initialization failed (non-critical)...");
        }
    }
}