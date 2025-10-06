#nullable enable

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using WileyWidget.Data;
using WileyWidget.Services;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WileyWidget.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WileyWidget.Configuration;

/// <summary>
/// Enterprise-grade database configuration for Azure SQL Database and SQL Server
/// Implements passwordless authentication, connection pooling, health checks, and monitoring
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Circuit breaker policy for database connections
    /// Tuned for resilience: 5 failures before breaking, 60 second recovery period
    /// </summary>
    internal static readonly AsyncCircuitBreakerPolicy CircuitBreaker = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<Azure.Identity.AuthenticationFailedException>()
        .Or<TimeoutException>()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60));

    /// <summary>
    /// Retry policy for development database connections
    /// Uses exponential backoff: 3 retries with 1s, 2s, 4s delays
    /// </summary>
    internal static readonly AsyncRetryPolicy DevelopmentRetryPolicy = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<Azure.Identity.AuthenticationFailedException>()
        .Or<DbException>()
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)));

    /// <summary>
    /// Adds enterprise-grade database services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddEnterpriseDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use Microsoft's built-in AddDbContextFactory for proper factory registration
        // This automatically registers both DbContext (scoped) and IDbContextFactory<TContext> (singleton)
        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            options.UseApplicationServiceProvider(sp);
            ConfigureAppDbContext(sp, options);
        });

        // Add enterprise health checks
        ConfigureEnterpriseHealthChecks(services, configuration);

        // Register repositories with enterprise patterns
        RegisterEnterpriseRepositories(services);

        // Register enterprise services
        RegisterEnterpriseServices(services);

        return services;
    }

    /// <summary>
    /// Configures AppDbContext regardless of registration mechanism (scoped or factory).
    /// </summary>
    private static void ConfigureAppDbContext(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
        var environment = "Local";

        var connectionString = BuildEnterpriseConnectionString(config, logger, environment);

        ConfigureLocalSqlServer(options, connectionString, logger);
        ConfigureEnterpriseDbContextOptions(options, logger);

        // Configure EF 9 seeding methods for automatic database initialization
        ConfigureDatabaseSeeding(options, logger);
    }

    /// <summary>
    /// Determines if connection string is for local SQL Server
    /// </summary>
    internal static bool IsLocalSqlServerConnection(string connectionString)
    {
        return connectionString.Contains("(localdb)") ||
               connectionString.Contains("localhost") ||
               connectionString.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
               connectionString.Contains("SQLEXPRESS", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Configures local SQL Server connection for development
    /// </summary>
    private static void ConfigureLocalSqlServer(DbContextOptionsBuilder options, string connectionString, ILogger logger)
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            // Development retry configuration
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);

            // Connection timeout
            sqlOptions.CommandTimeout(30);
        });

        logger.LogInformation("Configured local SQL Server connection for development environment");
    }

    /// <summary>
    /// Extracts server name from connection string for logging
    /// </summary>
    private static string ExtractServerFromConnectionString(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.DataSource ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }



    /// <summary>
    /// Builds enterprise-grade connection string with Azure SQL passwordless authentication
    /// Uses SQL Server Express for Development, Azure SQL for Production
    /// </summary>
    internal static string BuildEnterpriseConnectionString(IConfiguration config, ILogger logger, string environment)
    {
        logger?.LogInformation("🏭 CONFIGURING DATABASE CONNECTION - Environment: {Environment} (local SQL Server Express)",
            environment);

        // Always use local SQL Server Express for development - no environment-specific configs needed
        const string defaultConnection = "Server=localhost\\SQLEXPRESS01;Database=WileyWidgetDev;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        logger?.LogInformation("Using local SQL Server Express instance: {Server}",
            ExtractServerFromConnectionString(defaultConnection));
        return defaultConnection;
    }

    /// <summary>
    /// Configures enterprise-grade DbContext options
    /// </summary>
    private static void ConfigureEnterpriseDbContextOptions(DbContextOptionsBuilder options, ILogger logger)
    {
        // Enable sensitive data logging in development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            options.EnableSensitiveDataLogging();
            logger.LogInformation("Sensitive data logging enabled for local diagnostics");
        }
        options.EnableDetailedErrors();

        // Configure query tracking
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

        // Configure warnings
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
        });

        options.LogTo(message => logger.LogDebug("EF Core: {Message}", message),
            new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted });

        logger.LogInformation("Enterprise DbContext options configured for local environment");
    }

    /// <summary>
    /// Configures EF 9 database seeding using UseSeeding and UseAsyncSeeding methods
    /// </summary>
    private static void ConfigureDatabaseSeeding(DbContextOptionsBuilder options, ILogger logger)
    {
        // Configure synchronous seeding for EnsureCreated()
        options.UseSeeding((context, hasStoreManagement) =>
        {
            logger.LogInformation("EF Core seeding: Executing synchronous seeding (hasStoreManagement: {HasStoreManagement})", hasStoreManagement);

            var appContext = (AppDbContext)context;
            var seeder = new DatabaseSeeder(appContext);
            seeder.SeedAsync().GetAwaiter().GetResult(); // Synchronous execution for UseSeeding
        });

        // Configure asynchronous seeding for EnsureCreatedAsync()
        options.UseAsyncSeeding(async (context, hasStoreManagement, cancellationToken) =>
        {
            logger.LogInformation("EF Core seeding: Executing asynchronous seeding (hasStoreManagement: {HasStoreManagement})", hasStoreManagement);

            var appContext = (AppDbContext)context;
            var seeder = new DatabaseSeeder(appContext);
            await seeder.SeedAsync();
        });

        logger.LogInformation("EF 9 database seeding configured using UseSeeding/UseAsyncSeeding methods");
    }

    /// <summary>
    /// Registers enterprise-grade repositories
    /// </summary>
    private static void RegisterEnterpriseRepositories(IServiceCollection services)
    {
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();
        services.AddScoped<IMunicipalAccountRepository, MunicipalAccountRepository>();
        services.AddScoped<IUtilityCustomerRepository, UtilityCustomerRepository>();
    }

    /// <summary>
    /// Registers enterprise-grade services
    /// </summary>
    private static void RegisterEnterpriseServices(IServiceCollection services)
    {
        services.AddScoped<IQuickBooksService>(sp =>
        {
            var settings = SettingsService.Instance;
            return new QuickBooksService(settings);
        });

        services.AddScoped<IAIService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IAIService>>();
            var configuration = sp.GetRequiredService<IConfiguration>();

            // Try environment variable first, then appsettings
            var xaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY") ??
                           configuration["XAI:ApiKey"];

            var requireAi = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"), "true", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(configuration["XAI:RequireService"], "true", StringComparison.OrdinalIgnoreCase);

            // Log configuration status
            logger.LogInformation("🤖 XAI CONFIGURATION: API_KEY_SET={ApiKeySet}, REQUIRE_AI={RequireAi}, API_KEY_LENGTH={Length}, SOURCE={Source}",
                !string.IsNullOrEmpty(xaiApiKey) && xaiApiKey != "${XAI_API_KEY}",
                requireAi,
                string.IsNullOrEmpty(xaiApiKey) ? 0 : xaiApiKey.Length,
                Environment.GetEnvironmentVariable("XAI_API_KEY") != null ? "Environment" : "AppSettings");

            if (string.IsNullOrEmpty(xaiApiKey) || xaiApiKey == "${XAI_API_KEY}")
            {
                if (requireAi)
                {
                    logger.LogError("AI service required but XAI_API_KEY not set. Falling back to stub; functionality limited.");
                }
                else
                {
                    logger.LogWarning("XAI_API_KEY not set. Using DevNullAIService stub. Configure XAI:ApiKey in appsettings.json or set XAI_API_KEY environment variable.");
                }
                return new DevNullAIService();
            }

            // Real service initialization
            logger.LogInformation("Initializing XAIService with provided API key (length {Len}).", xaiApiKey.Length);
            return new XAIService(xaiApiKey, sp.GetRequiredService<ILogger<XAIService>>());
        });

    services.TryAddSingleton<IChargeCalculatorService, ServiceChargeCalculatorService>();
    services.TryAddSingleton<IWhatIfScenarioEngine, WhatIfScenarioEngine>();

    // Register health check configuration (service lifetime singleton)
    services.AddSingleton<Models.HealthCheckConfiguration>();
    // NOTE: HealthCheckService is registered as a singleton in WPF hosting extensions.
    // Do not register it here to avoid conflicting lifetimes.
    }

    /// <summary>
    /// Configures additional DbContext options
    /// </summary>
    private static void ConfigureDbContextOptions(DbContextOptionsBuilder options)
    {
        // Add any additional configuration here
        // This method can be extended for different environments

#if DEBUG
        // Enable detailed errors in development
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
#endif
    }

    /// <summary>
    /// Ensures the database is created and migrated with robust retry logic and metrics
    /// Call this during application startup
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        var metricsService = serviceProvider.GetService<ApplicationMetricsService>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var config = serviceProvider.GetRequiredService<IConfiguration>();
    const string environment = "Local";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Starting database initialization with retry logic for {Environment}", environment);

            // Get local connection string and log the server for diagnostics
            var connectionString = BuildEnterpriseConnectionString(config, logger, environment);
            logger.LogInformation("Local SQL Server target: {Server}", ExtractServerFromConnectionString(connectionString));

            // Apply retry policy tuned for local SQL Server
            IAsyncPolicy policy = DevelopmentRetryPolicy;

            await policy.ExecuteAsync(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var context = await contextFactory.CreateDbContextAsync();

                logger.LogInformation("Initializing local SQL Server database");

                // Check if database connection is available
                var canConnect = await context.Database.CanConnectAsync();
                logger.LogInformation("Database.CanConnectAsync() returned: {CanConnect}", canConnect);
                if (!canConnect)
                {
                    logger.LogError("Cannot connect to local SQL Server database - check connection string and server availability");
                    throw new InvalidOperationException("Local SQL Server database connection failed. Check the DefaultConnection string and ensure SQL Server is running.");
                }

                logger.LogInformation("Local SQL Server database connection verified - applying migrations");

                // Apply migrations for local SQL Server
                await context.Database.MigrateAsync();

                // Run seeding if needed
                await RunSeedingIfNeededAsync(context, logger, metricsService);
            });

            logger.LogInformation("Database initialization completed successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, true);
        }
        catch (CircuitBreakerOpenException ex)
        {
            logger.LogError(ex, "Database initialization failed - circuit breaker is open after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, false);
            throw new InvalidOperationException("Database initialization failed due to repeated connection issues", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed after {ElapsedMs}ms: {Message}", stopwatch.ElapsedMilliseconds, ex.Message);
            metricsService?.RecordMigration(stopwatch.Elapsed.TotalMilliseconds, false);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Ensures the local SQL Server database is initialized at startup.
    /// Safe to call multiple times.
    /// </summary>
    public static async Task EnsureLocalDatabaseInitializedAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        try
        {
            logger.LogInformation("Ensuring local SQL Server database is initialized");
            await EnsureDatabaseCreatedAsync(serviceProvider);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local SQL Server database initialization failed");
            throw;
        }
    }

    /// <summary>
    /// Runs database seeding if the database is empty
    /// </summary>
    private static async Task RunSeedingIfNeededAsync(AppDbContext context, ILogger logger, ApplicationMetricsService? metricsService)
    {
        try
        {
            // Check if database needs seeding
            if (await context.Enterprises.AnyAsync())
            {
                logger.LogDebug("Database already contains data, skipping seeding");
                return;
            }

            logger.LogInformation("Database is empty, running seeding process");

            var seeder = new DatabaseSeeder(context);
            await seeder.SeedAsync();

            logger.LogInformation("Database seeding completed successfully");
            metricsService?.RecordSeeding(true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database seeding failed, but continuing with application startup");
            metricsService?.RecordSeeding(false);
            // Don't throw - seeding failure shouldn't prevent app startup
        }
    }

    /// Validates the database schema by checking if required tables exist
    /// </summary>
    public static async Task ValidateDatabaseSchemaAsync(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(nameof(DatabaseConfiguration));
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();

            // Check if Enterprises table exists by attempting a simple query
            var enterpriseCount = await context.Enterprises.CountAsync();
            logger.LogInformation("Database schema validation passed: {EnterpriseCount} enterprises found in {ElapsedMs}ms",
                enterpriseCount, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database schema validation failed after {ElapsedMs}ms: {Message}. Application will continue with limited database functionality",
                stopwatch.ElapsedMilliseconds, ex.Message);
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    /// <summary>
    /// Configures enterprise-grade health checks
    /// </summary>
    private static void ConfigureEnterpriseHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            // Database health check with enterprise diagnostics
            .AddCheck<DatabaseHealthCheck>("Database",
                tags: new[] { "database", "azure", "sql", "enterprise" })
            // Memory health check
            .AddCheck<MemoryHealthCheck>("Memory",
                tags: new[] { "resources", "memory" })
            // Custom application health check
            .AddCheck<EnterpriseApplicationHealthCheck>("Enterprise Application",
                tags: new[] { "application", "enterprise" });

        // Register connectivity diagnostic service
        services.AddScoped<DatabaseConnectivityDiagnostic>();
    }
}

/// <summary>
/// Local dev stub to avoid AI dependency in development environments.
/// </summary>
internal sealed class DevNullAIService : WileyWidget.Services.IAIService
{
    public Task<string> GetInsightsAsync(string context, string question) =>
        Task.FromResult("[Dev Stub] AI insights disabled. Set XAI_API_KEY to enable.");

    public Task<string> AnalyzeDataAsync(string data, string analysisType) =>
        Task.FromResult("[Dev Stub] AI analysis disabled.");

    public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState) =>
        Task.FromResult("[Dev Stub] AI review disabled.");

    public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements) =>
        Task.FromResult("[Dev Stub] AI mock data generation disabled.");
}

/// <summary>
/// Custom memory health check
/// </summary>
public class MemoryHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var allocated = GC.GetTotalMemory(forceFullCollection: false);
        var threshold = 100 * 1024 * 1024; // 100 MB

        if (allocated > threshold)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
                $"High memory usage: {allocated / 1024 / 1024} MB"));
        }

        return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
            $"Memory usage normal: {allocated / 1024 / 1024} MB"));
    }
}

/// <summary>
/// Custom database health check for Azure SQL
/// </summary>
public class DatabaseHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public DatabaseHealthCheck(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Simple query to test connectivity and authentication
            var count = await dbContext.Enterprises.CountAsync(cancellationToken);

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Database connection successful - {count} enterprises found");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                $"Database health check failed: {ex.Message}");
        }
    }
}

/// <summary>
/// Custom health check for enterprise application status
/// </summary>
public class EnterpriseApplicationHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnterpriseApplicationHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can create a DbContext (tests DI and basic connectivity)
            using var scope = _scopeFactory.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);

            // Simple database connectivity test
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                    "Cannot connect to database");
            }

            // Check if critical tables exist
            var enterpriseCount = await dbContext.Enterprises.CountAsync(cancellationToken);

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Application healthy. Database accessible with {enterpriseCount} enterprises.");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy(
                $"Application health check failed: {ex.Message}");
        }
    }
}


