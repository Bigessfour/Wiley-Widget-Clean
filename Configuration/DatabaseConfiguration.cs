using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Data.SqlClient;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;
using Polly.CircuitBreaker;
using WileyWidget.Models;

namespace WileyWidget.Configuration;

/// <summary>
/// Enterprise-grade database configuration for Azure SQL Database
/// Implements passwordless authentication, connection pooling, health checks, and monitoring
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Circuit breaker policy for database connections
    /// Tuned for resilience: 3 failures before breaking, 30 second recovery period
    /// </summary>
    internal static readonly AsyncCircuitBreakerPolicy CircuitBreaker = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<Azure.Identity.AuthenticationFailedException>()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(30));

    /// <summary>
    /// Adds enterprise-grade database services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddEnterpriseDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add Azure Identity for passwordless authentication
        services.AddSingleton<DefaultAzureCredential>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<DefaultAzureCredential>>();

            // Configure Azure Identity with comprehensive credential sources and diagnostics
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                // Exclude interactive browser for production scenarios
                ExcludeInteractiveBrowserCredential = !Debugger.IsAttached,
                // Diagnostic options for troubleshooting
                Diagnostics =
                {
                    LoggedHeaderNames = { "x-ms-request-id", "x-ms-return-client-request-id", "x-ms-client-request-id" },
                    LoggedQueryParameters = { "api-version" },
                    IsLoggingEnabled = true,
                    IsDistributedTracingEnabled = true,
                    IsTelemetryEnabled = true
                }
            });

            // Only test credential acquisition in production or when explicitly requested
            var isProductionEnvironment = IsProductionEnvironment();
            var forceAzureAd = string.Equals(Environment.GetEnvironmentVariable("AZURE_SQL_FORCE_AAD"), "true", StringComparison.OrdinalIgnoreCase);

            if (isProductionEnvironment || forceAzureAd)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("Testing Azure credential acquisition...");
                        var token = await credential.GetTokenAsync(
                            new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
                            CancellationToken.None);
                        logger.LogInformation("Azure credential test successful - token acquired for database.windows.net");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Azure credential test failed - this may cause database connection issues");
                        logger.LogError("Credential error details: {Message}", ex.Message);
                    }
                });
            }
            else
            {
                logger.LogInformation("Skipping Azure credential test in development environment");
            }

            logger.LogInformation("Azure Identity configured with DefaultAzureCredential");
            return credential;
        });

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
            var credential = sp.GetRequiredService<DefaultAzureCredential>();

            string connectionString = BuildEnterpriseConnectionString(config, logger);

            // Check if this is a SQLite connection string (starts with "Data Source=")
            if (connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                // Use SQLite for development/local database
                options.UseSqlite(connectionString, sqliteOptions =>
                {
                    // SQLite-specific configuration
                    sqliteOptions.CommandTimeout(60);
                });
                logger.LogInformation("Using SQLite database for development");
            }
            else
            {
                // Use SQL Server for production
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    // Enterprise-grade retry configuration
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 10,
                        maxRetryDelay: TimeSpan.FromSeconds(60),
                        errorNumbersToAdd: new[] { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001 });

                    // Connection timeout and command timeout
                    sqlOptions.CommandTimeout(60);

                    // Query splitting for better performance
                    sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                });
                logger.LogInformation("Using SQL Server database");
            }

            // Only add Azure AD interceptor in production or when explicitly requested
            var isProductionEnvironment = IsProductionEnvironment();
            var forceAzureAd = string.Equals(Environment.GetEnvironmentVariable("AZURE_SQL_FORCE_AAD"), "true", StringComparison.OrdinalIgnoreCase);

            if (isProductionEnvironment || forceAzureAd)
            {
                // Add smart Azure AD connection interceptor that handles both Azure SQL and local connections
                options.AddInterceptors(new AzureAdConnectionInterceptor(credential, logger));
                logger.LogInformation("Azure AD connection interceptor added for production environment");
            }
            else
            {
                logger.LogInformation("Azure AD authentication disabled in development environment - using connection string credentials");
            }

            // Configure DbContext options
            ConfigureEnterpriseDbContextOptions(options, logger);

            logger.LogInformation("Enterprise database configuration applied successfully");
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
    /// Determines if the current environment is production
    /// </summary>
    private static bool IsProductionEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                         Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? 
                         "Development";
        
        return string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines if the current environment is development
    /// </summary>
    private static bool IsDevelopmentEnvironment()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? 
                         Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? 
                         "Development";
        
        return string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
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
    /// Validates local connection string format and accessibility
    /// </summary>
    private static bool ValidateLocalConnectionString(string connectionString, ILogger logger)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            
            // Validate required properties for local connection
            if (string.IsNullOrEmpty(builder.DataSource))
            {
                logger?.LogError("Connection string missing DataSource");
                return false;
            }

            if (string.IsNullOrEmpty(builder.InitialCatalog))
            {
                logger?.LogError("Connection string missing InitialCatalog (database name)");
                return false;
            }

            // For local development, we expect either Trusted_Connection or valid credentials
            if (!builder.IntegratedSecurity && string.IsNullOrEmpty(builder.UserID))
            {
                logger?.LogError("Connection string requires either Trusted_Connection=true or valid User ID for local development");
                return false;
            }

            logger?.LogDebug("Connection string validation passed for server: {Server}, database: {Database}", 
                builder.DataSource, builder.InitialCatalog);
            
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to validate connection string format");
            return false;
        }
    }

    /// <summary>
    /// Builds enterprise-grade connection string with conditional authentication
    /// In development: Always uses local SQL Server with Trusted_Connection
    /// In production: Uses Azure SQL with passwordless authentication
    /// </summary>
    internal static string BuildEnterpriseConnectionString(IConfiguration config, ILogger logger)
    {
        var isProductionEnvironment = IsProductionEnvironment();
        var isDevelopmentEnvironment = IsDevelopmentEnvironment();

        logger?.LogDebug("Environment detection - Production: {IsProduction}, Development: {IsDevelopment}",
            isProductionEnvironment, isDevelopmentEnvironment);
        logger?.LogDebug("Environment variables - ASPNETCORE_ENVIRONMENT: {AspNetCore}, DOTNET_ENVIRONMENT: {DotNet}",
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"));

        // DEVELOPMENT ENVIRONMENT: Always use local SQL Server
        if (isDevelopmentEnvironment)
        {
            // Check if developer wants to force Azure SQL testing in development
            var forceAzureSql = string.Equals(Environment.GetEnvironmentVariable("FORCE_AZURE_SQL_DEV"), "true", StringComparison.OrdinalIgnoreCase);
            
            if (forceAzureSql)
            {
                logger?.LogWarning("🔧 FORCE_AZURE_SQL_DEV is set - attempting Azure SQL connection in development mode");
                
                // Try to build Azure SQL connection with SQL auth fallback
                try
                {
                    var azureServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
                    var azureDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
                    var azureUser = Environment.GetEnvironmentVariable("AZURE_SQL_USER");
                    var azurePassword = Environment.GetEnvironmentVariable("AZURE_SQL_PASSWORD");
                    
                    if (!string.IsNullOrEmpty(azureServer) && !string.IsNullOrEmpty(azureDatabase) &&
                        !string.IsNullOrEmpty(azureUser) && !string.IsNullOrEmpty(azurePassword))
                    {
                        var fallbackConnection = $"Server={azureServer};Database={azureDatabase};User Id={azureUser};Password={azurePassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
                        logger?.LogInformation("Using Azure SQL with SQL authentication fallback in development: {Server}", azureServer);
                        return fallbackConnection;
                    }
                    else
                    {
                        logger?.LogWarning("FORCE_AZURE_SQL_DEV set but missing required environment variables (AZURE_SQL_SERVER, AZURE_SQL_DATABASE, AZURE_SQL_USER, AZURE_SQL_PASSWORD)");
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to build Azure SQL fallback connection in development");
                }
            }

            logger?.LogInformation("✅ DEVELOPMENT ENVIRONMENT DETECTED - using local database connection");

            var devConnection = config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(devConnection))
            {
                throw new InvalidOperationException(
                    "Development environment requires DefaultConnection in appsettings.json or appsettings.Development.json. " +
                    "Please configure a valid database connection string.");
            }

            // Check if it's a SQLite connection string
            if (devConnection.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogInformation("Using SQLite database for development: {Connection}", devConnection);
                return devConnection;
            }

            // Validate SQL Server connection string
            if (!ValidateLocalConnectionString(devConnection, logger))
            {
                throw new InvalidOperationException(
                    "DefaultConnection in configuration is not valid for development. " +
                    "Expected format: Server=(local)\\SQLEXPRESS;Database=...;Trusted_Connection=True;... or Data Source=... for SQLite. " +
                    "Check both appsettings.json and appsettings.Development.json files.");
            }

            logger?.LogInformation("Using local SQL Server connection string for development: {Server}",
                ExtractServerFromConnectionString(devConnection));
            return devConnection;
        }

        // PRODUCTION ENVIRONMENT: Prefer passwordless Azure SQL connection string from configuration
        logger?.LogInformation("🏭 PRODUCTION ENVIRONMENT DETECTED - configuring Azure SQL connection");

        // Preferred: read a passwordless Azure SQL connection string configured in appsettings or environment
        // per Microsoft docs (Authentication=Active Directory Default)
        var configuredAzureConn = config.GetConnectionString("AZURE_SQL_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(configuredAzureConn) &&
            !string.Equals(configuredAzureConn, "${AZURE_SQL_CONNECTIONSTRING}", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogInformation("Using configured AZURE_SQL_CONNECTIONSTRING from configuration");
            return configuredAzureConn.Trim();
        }

        var rawServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
        var rawDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
        var server = string.IsNullOrWhiteSpace(rawServer) || rawServer == "${AZURE_SQL_SERVER}" ? null : rawServer.Trim();
        var database = string.IsNullOrWhiteSpace(rawDatabase) || rawDatabase == "${AZURE_SQL_DATABASE}" ? null : rawDatabase.Trim();

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
        {
            logger?.LogWarning("Azure SQL environment variables missing in Production. Falling back to SQLite dev database for diagnostics.");
            var fallback = config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(fallback) && fallback.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                logger?.LogWarning("Using fallback SQLite database in Production: {Fallback}", fallback);
                return fallback;
            }
            // If no SQLite dev fallback, last resort: explicit in-memory (not persistent)
            logger?.LogWarning("No SQLite DefaultConnection available; using in-memory database placeholder.");
            return "Data Source=wileywidget_fallback.db"; // persistent file for minimal risk
        }

        logger?.LogInformation("Building Azure SQL connection string for server: {Server}, database: {Database}",
            server, database);

        // Build passwordless connection string. The access token will be provided by the interceptor.
        // Normalize server name to avoid double suffix if a full FQDN is provided
        var dataSource = server.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase)
            ? server
            : $"{server}.database.windows.net";

        var connectionStringBuilder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = database,
            Encrypt = true,
            TrustServerCertificate = false,
            ConnectTimeout = 30,
            // Enterprise connection pooling
            MaxPoolSize = 100,
            MinPoolSize = 5,
            Pooling = true,
            // Performance optimizations
            MultipleActiveResultSets = true,
            ApplicationName = "WileyWidget-Enterprise",
            WorkstationID = Environment.MachineName
        };

        // Include Authentication directive to allow SqlClient to acquire token using DefaultAzureCredential
        connectionStringBuilder["Authentication"] = "Active Directory Default";

        var finalConnectionString = connectionStringBuilder.ToString();
        logger?.LogInformation("Enterprise passwordless connection string configured for server: {Server}", server);

        return finalConnectionString;
    }

    /// <summary>
    /// Configures enterprise-grade DbContext options
    /// </summary>
    private static void ConfigureEnterpriseDbContextOptions(DbContextOptionsBuilder options, ILogger logger)
    {
        // Enable sensitive data logging only in development
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }

        // Configure query tracking
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);

        // Configure warnings
        options.ConfigureWarnings(warnings =>
        {
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);

        });

        // Add logging
        options.LogTo(message => logger.LogInformation("EF Core: {Message}", message),
            new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted });

        logger.LogInformation("Enterprise DbContext options configured");
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
            var xaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            var requireAi = string.Equals(Environment.GetEnvironmentVariable("REQUIRE_AI_SERVICE"), "true", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(xaiApiKey) || xaiApiKey == "${XAI_API_KEY}")
            {
                if (requireAi)
                {
                    logger.LogError("AI service required (REQUIRE_AI_SERVICE=true) but XAI_API_KEY not set. Falling back to stub; functionality limited.");
                }
                else
                {
                    logger.LogWarning("XAI_API_KEY not set. Using DevNullAIService stub (set REQUIRE_AI_SERVICE=true to enforce or provide key).");
                }
                return new DevNullAIService();
            }

            // Real service initialization
            logger.LogInformation("Initializing XAIService with provided API key (length {Len}).", xaiApiKey.Length);
            return new XAIService(xaiApiKey, sp.GetRequiredService<ILogger<XAIService>>());
        });

        services.AddScoped<IChargeCalculatorService, ServiceChargeCalculatorService>();
        services.AddScoped<IWhatIfScenarioEngine, WhatIfScenarioEngine>();

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
        var logger = serviceProvider.GetRequiredService<ILogger>();
        var metricsService = serviceProvider.GetService<ApplicationMetricsService>();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Starting database initialization with retry logic");

            await CircuitBreaker.ExecuteAsync(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var context = await contextFactory.CreateDbContextAsync();

                // If using SQLite (dev), EnsureCreated is appropriate. For SQL Server, run migrations.
                var providerName = context.Database.ProviderName ?? string.Empty;
                if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug("Using SQLite - calling EnsureCreated");
                    await context.Database.EnsureCreatedAsync();
                }
                else
                {
                    logger.LogDebug("Using SQL Server - running migrations");
                    await context.Database.MigrateAsync();
                }

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
    /// Dev helper: ensure the dev database exists at startup to avoid CanConnect=false.
    /// Safe to call multiple times.
    /// </summary>
    public static async Task EnsureDevDatabaseIfNeededAsync(IServiceProvider serviceProvider)
    {
        if (!IsDevelopmentEnvironment()) return;

        var logger = serviceProvider.GetRequiredService<ILogger>();
        try
        {
            await EnsureDatabaseCreatedAsync(serviceProvider);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Dev database initialization failed, but continuing");
            // swallow in dev - don't crash the app
        }
    }

    /// Runs database seeding if the database is empty
    /// </summary>
    private static async Task RunSeedingIfNeededAsync(AppDbContext context, ILogger logger, ApplicationMetricsService metricsService)
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
        var logger = serviceProvider.GetRequiredService<ILogger>();
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
            // Database health check
            .AddCheck<DatabaseHealthCheck>("Database", tags: new[] { "database", "azure", "sql" })
            // Memory health check
            .AddCheck<MemoryHealthCheck>("Memory", tags: new[] { "resources", "memory" })
            // Custom application health check
            .AddCheck<EnterpriseApplicationHealthCheck>("Enterprise Application");
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
    private readonly IServiceProvider _serviceProvider;

    public EnterpriseApplicationHealthCheck(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if we can create a DbContext (tests DI and basic connectivity)
            using var scope = _serviceProvider.CreateScope();
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

/// <summary>
/// Connection interceptor for Azure AD authentication
/// </summary>
public class AzureAdConnectionInterceptor : DbConnectionInterceptor
{
    private readonly DefaultAzureCredential _credential;
    private readonly ILogger _logger;

    public AzureAdConnectionInterceptor(DefaultAzureCredential credential, ILogger logger)
    {
        _credential = credential;
        _logger = logger;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        throw new NotSupportedException("Synchronous operations are not supported. Use ConnectionOpeningAsync.");
    }

    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        var sqlConnection = (SqlConnection)connection;

        // Only apply Azure AD authentication to Azure SQL Database connections
        // Check if this is an Azure SQL Database connection by examining the data source
        var connectionString = sqlConnection.ConnectionString;
        var isAzureSqlConnection = connectionString.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase);

        if (!isAzureSqlConnection)
        {
            _logger.LogDebug("Skipping Azure AD token for local SQL Server connection: {DataSource}", sqlConnection.DataSource);
            return result;
        }

        // Check if the connection already has Integrated Security enabled, which conflicts with AccessToken
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (builder.IntegratedSecurity)
        {
            _logger.LogWarning("Cannot apply Azure AD token to connection with Integrated Security. Connection: {DataSource}", sqlConnection.DataSource);
            return result;
        }

        _logger.LogInformation("Attempting Azure AD token acquisition for database connection to {DataSource}", sqlConnection.DataSource);

        return await DatabaseConfiguration.CircuitBreaker.ExecuteAsync(async () =>
        {
            try
            {
                var token = await _credential.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
                    cancellationToken);

                // Log non-sensitive token metadata (expiry). Token claim parsing was temporary and removed.
                _logger.LogInformation("Token acquired. ExpiresOn: {Exp}", token.ExpiresOn);

                sqlConnection.AccessToken = token.Token;
                _logger.LogInformation("Azure AD access token successfully applied to database connection for {DataSource}", sqlConnection.DataSource);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to acquire Azure AD token for database connection to {DataSource}", sqlConnection.DataSource);
                _logger.LogError("Token acquisition error details: {Message}", ex.Message);
                throw; // Re-throw to let the circuit breaker handle it
            }
        });
    }
}
