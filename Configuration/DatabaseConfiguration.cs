#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;

namespace WileyWidget.Configuration;

/// <summary>
/// Configuration class for database setup and dependency injection
/// </summary>
public static class DatabaseConfiguration
{
    /// <summary>
    /// Adds database services to the dependency injection container
    /// </summary>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext with SQL Server (supports both LocalDB and Azure SQL)
        services.AddDbContext<AppDbContext>(options =>
        {
            // Try Azure SQL first, fallback to LocalDB
            var azureConnectionString = configuration.GetConnectionString("AzureConnection");
            var localConnectionString = configuration.GetConnectionString("DefaultConnection");

            string connectionString;

            if (!string.IsNullOrEmpty(azureConnectionString) &&
                !azureConnectionString.Contains("${AZURE_SQL_CONNECTION_STRING}"))
            {
                // Use Azure SQL if properly configured
                connectionString = azureConnectionString;
                Console.WriteLine("Using Azure SQL Database");
            }
            else if (!string.IsNullOrEmpty(localConnectionString))
            {
                // Fallback to LocalDB for development
                connectionString = localConnectionString;
                Console.WriteLine("Using LocalDB for development");
            }
            else
            {
                throw new InvalidOperationException(
                    "No valid database connection string found. " +
                    "Please configure either AzureConnection or DefaultConnection in appsettings.json");
            }

            options.UseSqlServer(connectionString, sqlOptions =>
            {
                // Configure SQL Server options
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);

                sqlOptions.CommandTimeout(30);

                // Enable query splitting for better performance with includes
                sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            });

            // Configure logging and other options
            ConfigureDbContextOptions(options);
        });

        // Register repository
        services.AddScoped<IEnterpriseRepository, EnterpriseRepository>();

        return services;
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
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Create database if it doesn't exist
            await context.Database.EnsureCreatedAsync();

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
<<<<<<< Updated upstream
=======

    /// <summary>
    /// Validates the database schema by checking if required tables exist
    /// </summary>
    public static async Task ValidateDatabaseSchemaAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
>>>>>>> Stashed changes
}
