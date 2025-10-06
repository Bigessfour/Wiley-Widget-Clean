#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Data;

namespace WileyWidget.Services;

/// <summary>
/// Enterprise database connection health check service
/// Provides comprehensive monitoring and diagnostics for database connections
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<DatabaseHealthCheck> _logger;
    private readonly string _environment;

    // Health check metrics
    private static readonly object _metricsLock = new();
    private static DateTime _lastSuccessfulCheck = DateTime.MinValue;
    private static int _consecutiveFailures = 0;
    private static TimeSpan _lastResponseTime = TimeSpan.Zero;

    public DatabaseHealthCheck(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<DatabaseHealthCheck> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
    }

    /// <summary>
    /// Performs comprehensive database health check
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Starting database health check");

            using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Test 1: Basic connectivity
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return await ReportFailureAsync("Database connection failed", stopwatch.Elapsed);
            }

            // Test 2: Simple query execution
            var testQuery = await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            if (testQuery != -1) // ExecuteSqlRaw returns -1 for non-SELECT statements
            {
                _logger.LogWarning("Unexpected result from test query: {Result}", testQuery);
            }

            // Test 3: Entity query (if tables exist)
            try
            {
                // Try to query a common entity - this will fail gracefully if tables don't exist
                var entityCount = await dbContext.Enterprises.CountAsync(cancellationToken);
                _logger.LogDebug("Entity count query successful: {Count} enterprises", entityCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Entity query test failed - this may be expected if database is not fully initialized");
                // Don't fail the health check for this - database might still be initializing
            }

            // Test 4: Connection pooling status (SQL Server only)
            if (dbContext.Database.IsSqlServer())
            {
                await TestSqlServerConnectionPoolingAsync(dbContext, cancellationToken);
            }

            // All tests passed
            return await ReportSuccessAsync(stopwatch.Elapsed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return await ReportFailureAsync($"Database health check failed: {ex.Message}", stopwatch.Elapsed);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Tests SQL Server connection pooling metrics
    /// </summary>
    private Task TestSqlServerConnectionPoolingAsync(
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query connection pooling statistics
            var connectionString = dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Cannot retrieve connection string for pooling analysis");
                return Task.CompletedTask;
            }

            // Parse connection string to check pooling settings
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

            _logger.LogDebug("Connection pooling status - Pooling: {Pooling}, MinPoolSize: {Min}, MaxPoolSize: {Max}",
                builder.Pooling, builder.MinPoolSize, builder.MaxPoolSize);

            // Additional pooling diagnostics could be added here
            // Note: Actual pool statistics require access to SqlConnection internals

        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze connection pooling");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reports successful health check
    /// </summary>
    private Task<HealthCheckResult> ReportSuccessAsync(TimeSpan responseTime)
    {
        lock (_metricsLock)
        {
            _lastSuccessfulCheck = DateTime.UtcNow;
            _consecutiveFailures = 0;
            _lastResponseTime = responseTime;
        }

        var data = new Dictionary<string, object>
        {
            ["environment"] = _environment,
            ["response_time_ms"] = responseTime.TotalMilliseconds,
            ["last_success"] = _lastSuccessfulCheck,
            ["consecutive_failures"] = _consecutiveFailures
        };

        _logger.LogInformation("Database health check passed in {ResponseTimeMs}ms", responseTime.TotalMilliseconds);

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Database connection healthy (Response: {responseTime.TotalMilliseconds:F0}ms)",
            data));
    }

    /// <summary>
    /// Reports failed health check
    /// </summary>
    private Task<HealthCheckResult> ReportFailureAsync(string message, TimeSpan responseTime)
    {
        lock (_metricsLock)
        {
            _consecutiveFailures++;
        }

        var data = new Dictionary<string, object>
        {
            ["environment"] = _environment,
            ["response_time_ms"] = responseTime.TotalMilliseconds,
            ["last_success"] = _lastSuccessfulCheck,
            ["consecutive_failures"] = _consecutiveFailures,
            ["failure_time"] = DateTime.UtcNow
        };

        var healthStatus = _consecutiveFailures >= 3 ? HealthStatus.Unhealthy : HealthStatus.Degraded;

        _logger.LogWarning("Database health check failed: {Message} (Failures: {ConsecutiveFailures}, Response: {ResponseTimeMs}ms)",
            message, _consecutiveFailures, responseTime.TotalMilliseconds);

        return Task.FromResult(new HealthCheckResult(
            healthStatus,
            $"{message} (Failures: {_consecutiveFailures}, Response: {responseTime.TotalMilliseconds:F0}ms)",
            data: data));
    }

    /// <summary>
    /// Gets current health metrics
    /// </summary>
    public static (DateTime LastSuccess, int ConsecutiveFailures, TimeSpan LastResponseTime) GetHealthMetrics()
    {
        lock (_metricsLock)
        {
            return (_lastSuccessfulCheck, _consecutiveFailures, _lastResponseTime);
        }
    }
}

/// <summary>
/// Database connectivity diagnostic service
/// Provides detailed diagnostics for connection issues
/// </summary>
public class DatabaseConnectivityDiagnostic
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<DatabaseConnectivityDiagnostic> _logger;

    public DatabaseConnectivityDiagnostic(
        IDbContextFactory<AppDbContext> contextFactory,
        ILogger<DatabaseConnectivityDiagnostic> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Performs comprehensive connectivity diagnostics
    /// </summary>
    public async Task<ConnectivityReport> DiagnoseConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var report = new ConnectivityReport
        {
            Timestamp = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"
        };

        try
        {
            using var dbContext = await _contextFactory.CreateDbContextAsync(cancellationToken);

            // Test basic connectivity
            report.CanConnect = await TestBasicConnectivityAsync(dbContext, cancellationToken);

            // Test query execution
            report.CanExecuteQueries = await TestQueryExecutionAsync(dbContext, cancellationToken);

            // Test entity operations
            report.CanAccessEntities = await TestEntityAccessAsync(dbContext, cancellationToken);

            // Gather connection information
            report.ConnectionInfo = await GatherConnectionInfoAsync(dbContext, cancellationToken);

            // Test connection pooling
            report.PoolingInfo = await TestConnectionPoolingAsync(dbContext, cancellationToken);

            report.Success = report.CanConnect && report.CanExecuteQueries;
            report.ErrorMessage = report.Success ? null : "One or more connectivity tests failed";

        }
        catch (Exception ex)
        {
            report.Success = false;
            report.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Connectivity diagnostic failed");
        }

        return report;
    }

    private async Task<bool> TestBasicConnectivityAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Basic connectivity test failed");
            return false;
        }
    }

    private async Task<bool> TestQueryExecutionAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Query execution test failed");
            return false;
        }
    }

    private async Task<bool> TestEntityAccessAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            // Try to access entities - this will fail if database is not initialized
            await dbContext.Enterprises.AnyAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entity access test failed - database may not be initialized");
            return false;
        }
    }

    private Task<ConnectionInfo> GatherConnectionInfoAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var info = new ConnectionInfo();

        try
        {
            info.ConnectionString = dbContext.Database.GetConnectionString();
            info.DatabaseProvider = dbContext.Database.ProviderName;
            info.DatabaseName = dbContext.Database.GetDbConnection().Database;

            if (dbContext.Database.IsSqlServer())
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(info.ConnectionString);
                info.ServerName = builder.DataSource;
                info.IsSqlServer = true;
                info.PoolingEnabled = builder.Pooling;
                info.MinPoolSize = builder.MinPoolSize;
                info.MaxPoolSize = builder.MaxPoolSize;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to gather connection information");
        }

        return Task.FromResult(info);
    }

    private async Task<PoolingInfo> TestConnectionPoolingAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var info = new PoolingInfo();

        try
        {
            if (dbContext.Database.IsSqlServer())
            {
                var connectionString = dbContext.Database.GetConnectionString();
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

                info.PoolingEnabled = builder.Pooling;
                info.MinPoolSize = builder.MinPoolSize;
                info.MaxPoolSize = builder.MaxPoolSize;
                info.ConnectionTimeout = builder.ConnectTimeout;

                // Test multiple connections to verify pooling
                var connections = new List<Microsoft.Data.SqlClient.SqlConnection>();
                try
                {
                    for (int i = 0; i < Math.Min(3, info.MaxPoolSize); i++)
                    {
                        var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                        await conn.OpenAsync(cancellationToken);
                        connections.Add(conn);
                        info.ActiveConnections++;
                    }

                    info.PoolingFunctional = true;
                }
                finally
                {
                    foreach (var conn in connections)
                    {
                        await conn.CloseAsync();
                        await conn.DisposeAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Connection pooling test failed");
            info.PoolingFunctional = false;
        }

        return info;
    }
}

/// <summary>
/// Connectivity diagnostic report
/// </summary>
public class ConnectivityReport
{
    public DateTime Timestamp { get; set; }
    public string Environment { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool CanConnect { get; set; }
    public bool CanExecuteQueries { get; set; }
    public bool CanAccessEntities { get; set; }
    public ConnectionInfo? ConnectionInfo { get; set; }
    public PoolingInfo? PoolingInfo { get; set; }
}

/// <summary>
/// Connection information
/// </summary>
public class ConnectionInfo
{
    public string? ConnectionString { get; set; }
    public string? DatabaseProvider { get; set; }
    public string? DatabaseName { get; set; }
    public string? ServerName { get; set; }
    public bool IsSqlServer { get; set; }
    public bool PoolingEnabled { get; set; }
    public int MinPoolSize { get; set; }
    public int MaxPoolSize { get; set; }
}

/// <summary>
/// Connection pooling information
/// </summary>
public class PoolingInfo
{
    public bool PoolingEnabled { get; set; }
    public int MinPoolSize { get; set; }
    public int MaxPoolSize { get; set; }
    public int ConnectionTimeout { get; set; }
    public int ActiveConnections { get; set; }
    public bool PoolingFunctional { get; set; }
}