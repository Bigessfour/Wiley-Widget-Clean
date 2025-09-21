using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;
using Polly;
using Polly.CircuitBreaker;

namespace WileyWidget.Configuration;

/// <summary>
/// Enterprise-grade database configuration for Azure SQL Database
/// Implements passwordless authentication, connection pooling, health checks, and monitoring
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Circuit breaker policy for database connections
    /// </summary>
    internal static readonly AsyncCircuitBreakerPolicy CircuitBreaker = Policy
        .Handle<SqlException>(ex => ex.Number is 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920 or 11001)
        .Or<Azure.Identity.AuthenticationFailedException>()
        .CircuitBreakerAsync(5, TimeSpan.FromMinutes(1));

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

            // Configure Azure Identity with comprehensive credential sources
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                // Exclude interactive browser for production scenarios
                ExcludeInteractiveBrowserCredential = !Debugger.IsAttached,
                // Diagnostic options for troubleshooting
                Diagnostics =
                {
                    LoggedHeaderNames = { "x-ms-request-id" },
                    LoggedQueryParameters = { "api-version" },
                    IsLoggingEnabled = true,
                    IsDistributedTracingEnabled = true,
                    IsTelemetryEnabled = true
                }
            });

            logger.LogInformation("Azure Identity configured with DefaultAzureCredential");
            return credential;
        });

        services.AddDbContextFactory<AppDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<AppDbContext>>();
            var credential = sp.GetRequiredService<DefaultAzureCredential>();

            string connectionString = BuildEnterpriseConnectionString(config, logger);

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

            // Add smart Azure AD connection interceptor that handles both Azure SQL and local connections
            options.AddInterceptors(new AzureAdConnectionInterceptor(credential, logger));
            logger.LogInformation("Smart Azure AD connection interceptor added - will apply only to Azure SQL connections");

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
    /// Builds enterprise-grade connection string with passwordless authentication
    /// Falls back to local SQL Server for development when Azure SQL variables are not configured
    /// </summary>
    internal static string BuildEnterpriseConnectionString(IConfiguration config, ILogger logger)
    {
        var rawServer = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
        var rawDatabase = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");
        var server = string.IsNullOrWhiteSpace(rawServer) || rawServer == "${AZURE_SQL_SERVER}" ? null : rawServer.Trim();
        var database = string.IsNullOrWhiteSpace(rawDatabase) || rawDatabase == "${AZURE_SQL_DATABASE}" ? null : rawDatabase.Trim();

        // Determine environment context
        var isProductionEnvironment = IsProductionEnvironment();
        var isDevelopmentEnvironment = IsDevelopmentEnvironment();
        
        logger?.LogDebug("Environment detection - Production: {IsProduction}, Development: {IsDevelopment}", 
            isProductionEnvironment, isDevelopmentEnvironment);

        // In Development, prefer local DefaultConnection unless explicitly overridden
        if (isDevelopmentEnvironment && !string.Equals(Environment.GetEnvironmentVariable("AZURE_SQL_USE_IN_DEVELOPMENT"), "1", StringComparison.Ordinal))
        {
            logger?.LogInformation("Development environment detected - preferring local DefaultConnection");
            var devConnection = config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(devConnection) && ValidateLocalConnectionString(devConnection, logger))
            {
                logger?.LogInformation("Using local SQL Server connection string for development: {Server}", 
                    ExtractServerFromConnectionString(devConnection));
                return devConnection;
            }
            logger?.LogWarning("Local DefaultConnection invalid or missing; will evaluate Azure env vars as fallback");
        }

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
        {
            if (isProductionEnvironment)
            {
                logger?.LogError("Azure SQL environment variables are required in production but not found");
                throw new InvalidOperationException(
                    "Production environment requires Azure SQL configuration. " +
                    "Please configure AZURE_SQL_SERVER and AZURE_SQL_DATABASE environment variables.");
            }

            logger?.LogWarning("Azure SQL environment variables not found, falling back to local SQL Server for development");

            // Fall back to local SQL Server connection string from configuration
            var fallbackConnection = config.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrEmpty(fallbackConnection))
            {
                logger?.LogInformation("Using local SQL Server connection string for development: {Server}", 
                    ExtractServerFromConnectionString(fallbackConnection));
                
                // Validate the connection string format
                if (ValidateLocalConnectionString(fallbackConnection, logger))
                {
                    return fallbackConnection;
                }
            }

            throw new InvalidOperationException(
                "Azure SQL connection information not found and no valid fallback connection available. " +
                "Please configure AZURE_SQL_SERVER and AZURE_SQL_DATABASE environment variables, " +
                "or ensure a valid DefaultConnection is configured in appsettings.json.");
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
            var xaiApiKey = Environment.GetEnvironmentVariable("XAI_API_KEY");
            if (string.IsNullOrEmpty(xaiApiKey))
            {
                throw new InvalidOperationException("XAI_API_KEY environment variable is not set");
            }
            return new XAIService(xaiApiKey);
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
    /// Ensures the database is created and migrated
    /// Call this during application startup
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        try
        {
            // Apply any pending migrations
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            // Log the error - in a real app, you'd use a proper logging framework
            Console.WriteLine($"Database initialization failed: {ex.Message}");

            // For development, don't crash the app - just log the error
            // The app can still run with limited functionality
            Console.WriteLine("Application will continue without database connectivity.");
        }
    }

    /// <summary>
    /// Validates the database schema by checking if required tables exist
    /// </summary>
    public static async Task ValidateDatabaseSchemaAsync(IServiceProvider serviceProvider)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        try
        {
            // Check if Enterprises table exists by attempting a simple query
            var enterpriseCount = await context.Enterprises.CountAsync();
            Console.WriteLine($"Database health check passed: {enterpriseCount} enterprises found.");
        }
        catch (Exception ex)
        {
            // For development, don't crash the app - just log the error
            Console.WriteLine($"Database schema validation failed: {ex.Message}");
            Console.WriteLine("Application will continue with limited database functionality.");
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
    private readonly IConfiguration _configuration;

    public DatabaseHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = DatabaseConfiguration.BuildEnterpriseConnectionString(_configuration, null);
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Simple query to test connectivity
            using var command = new SqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                "Database connection successful");
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

        return await DatabaseConfiguration.CircuitBreaker.ExecuteAsync(async () =>
        {
            var token = await _credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(new[] { "https://database.windows.net/.default" }),
                cancellationToken);
            sqlConnection.AccessToken = token.Token;
            _logger.LogInformation("Azure AD access token provided for database connection.");
            return result;
        });
    }
}
