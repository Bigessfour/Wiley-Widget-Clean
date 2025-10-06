#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using WileyWidget.Configuration;

namespace WileyWidget.Data;

/// <summary>
/// Design-time factory for AppDbContext to support EF Core migrations and tools
/// Uses enterprise database configuration for consistent connection handling
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Creates a new instance of AppDbContext for design-time operations
    /// </summary>
    public AppDbContext CreateDbContext(string[] args)
    {
        // Build configuration
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Local";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Use enterprise connection string building logic
    var connectionString = DatabaseConfiguration.BuildEnterpriseConnectionString(configuration, NullLogger<AppDbContextFactory>.Instance, environment);

        // Configure DbContext options based on environment
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);

            sqlOptions.CommandTimeout(30);
        });

        // Configure common options for design-time
    ConfigureDesignTimeOptions(optionsBuilder);

        return new AppDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Builds enterprise connection string (copied from DatabaseConfiguration for design-time independence)
    /// </summary>
    /// <summary>
    /// Configures design-time specific options
    /// </summary>
    private static void ConfigureDesignTimeOptions(DbContextOptionsBuilder options)
    {
        // Enable sensitive data logging in development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            options.EnableSensitiveDataLogging();
        }
        options.EnableDetailedErrors();

        // Configure query tracking for design-time
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
    }
}
