using System;
using System.Diagnostics;
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TaskCompletionSource<bool> _initializationCompleted = new();
    
    public Task InitializationCompleted => _initializationCompleted.Task;

    public BackgroundInitializationService(
        ILogger<BackgroundInitializationService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var correlationId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("🔄 [BACKGROUND INIT] Starting background initialization tasks - CorrelationId: {CorrelationId}", correlationId);
            var totalStopwatch = Stopwatch.StartNew();

            using var scope = _scopeFactory.CreateScope();
            var scopedProvider = scope.ServiceProvider;

            // ✅ STEP 1: Database creation/migration
            _logger.LogInformation("📊 [STEP 1/3] Ensuring database is created/migrated - CorrelationId: {CorrelationId}", correlationId);
            var ensureDbStopwatch = Stopwatch.StartNew();
            
            try
            {
                await WileyWidget.Configuration.DatabaseConfiguration.EnsureDatabaseCreatedAsync(scopedProvider);
                ensureDbStopwatch.Stop();
                
                _logger.LogInformation("✅ [STEP 1/3 SUCCESS] Database creation/migration completed in {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    ensureDbStopwatch.ElapsedMilliseconds, correlationId);
                _logger.LogInformation("   ➜ Database is ready for application use");
            }
            catch (Exception dbEx)
            {
                ensureDbStopwatch.Stop();
                _logger.LogError(dbEx, "❌ [STEP 1/3 FAILED] Database creation/migration failed after {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    ensureDbStopwatch.ElapsedMilliseconds, correlationId);
                throw; // Fatal: cannot proceed without database
            }

            // ✅ STEP 2: Schema validation
            _logger.LogInformation("🔍 [STEP 2/3] Validating database schema - CorrelationId: {CorrelationId}", correlationId);
            var validateSchemaStopwatch = Stopwatch.StartNew();
            
            try
            {
                await WileyWidget.Configuration.DatabaseConfiguration.ValidateDatabaseSchemaAsync(scopedProvider);
                validateSchemaStopwatch.Stop();
                
                _logger.LogInformation("✅ [STEP 2/3 SUCCESS] Database schema validation completed in {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    validateSchemaStopwatch.ElapsedMilliseconds, correlationId);
                _logger.LogInformation("   ➜ Database schema is valid and consistent");
            }
            catch (Exception schemaEx)
            {
                validateSchemaStopwatch.Stop();
                _logger.LogWarning(schemaEx, "⚠️ [STEP 2/3 WARNING] Database schema validation failed after {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    validateSchemaStopwatch.ElapsedMilliseconds, correlationId);
                _logger.LogWarning("   ➜ Continuing with potentially inconsistent schema");
                // Non-fatal: log and continue
            }

            // ✅ STEP 3: Azure initialization
            _logger.LogInformation("☁️ [STEP 3/3] Initializing Azure integrations - CorrelationId: {CorrelationId}", correlationId);
            var azureInitStopwatch = Stopwatch.StartNew();
            
            try
            {
                await InitializeAzureAsync(stoppingToken);
                azureInitStopwatch.Stop();
                
                _logger.LogInformation("✅ [STEP 3/3 SUCCESS] Azure initialization completed in {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    azureInitStopwatch.ElapsedMilliseconds, correlationId);
                _logger.LogInformation("   ➜ Azure services are ready (if configured)");
            }
            catch (Exception azureEx)
            {
                azureInitStopwatch.Stop();
                _logger.LogWarning(azureEx, "⚠️ [STEP 3/3 WARNING] Azure initialization failed after {ElapsedMs}ms - CorrelationId: {CorrelationId}", 
                    azureInitStopwatch.ElapsedMilliseconds, correlationId);
                _logger.LogWarning("   ➜ Continuing without Azure integration");
                // Non-fatal: Azure is optional
            }

            totalStopwatch.Stop();
            
            // ✅ FINAL VERIFICATION: All background initialization complete
            _logger.LogInformation("✅ [BACKGROUND INIT COMPLETE] All tasks completed in {TotalElapsedMs}ms - CorrelationId: {CorrelationId}", 
                totalStopwatch.ElapsedMilliseconds, correlationId);
            _logger.LogInformation("   ➜ Database: {DbTime}ms | Schema: {SchemaTime}ms | Azure: {AzureTime}ms",
                ensureDbStopwatch.ElapsedMilliseconds, 
                validateSchemaStopwatch.ElapsedMilliseconds, 
                azureInitStopwatch.ElapsedMilliseconds);
            
            _initializationCompleted.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("🛑 [BACKGROUND INIT CANCELLED] Shutdown requested - CorrelationId: {CorrelationId}", correlationId);
            _initializationCompleted.TrySetCanceled();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [BACKGROUND INIT FAILED] Fatal error during background initialization - CorrelationId: {CorrelationId}", correlationId);
            _logger.LogError("   ➜ Application startup may be incomplete");
            _initializationCompleted.TrySetException(ex);
        }
    }

    private Task InitializeAzureAsync(CancellationToken token)
    {
        // Add additional Azure initialization here as needed
        return Task.CompletedTask;
    }
}
