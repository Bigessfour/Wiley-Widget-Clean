using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.Tests;

/// <summary>
/// Database connection testing utilities following Microsoft best practices
/// </summary>
public static class DatabaseConnectionTestUtilities
{
    /// <summary>
    /// Tests if a database connection string is valid
    /// </summary>
    public static async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync();

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tests if an Entity Framework DbContext can connect to the database
    /// </summary>
    public static async Task<bool> TestDbContextConnectionAsync(IDbContextFactory<AppDbContext> contextFactory)
    {
        try
        {
            using var context = contextFactory.CreateDbContext();
            return await context.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates connection string format for different database providers
    /// </summary>
    public static bool ValidateConnectionString(string connectionString, DatabaseProvider provider)
    {
        try
        {
            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
                    return !string.IsNullOrEmpty(sqlBuilder.DataSource) &&
                           !string.IsNullOrEmpty(sqlBuilder.InitialCatalog);

                case DatabaseProvider.Sqlite:
                    return connectionString.Contains("DataSource") ||
                           connectionString.Contains("Data Source");

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets database provider from connection string
    /// </summary>
    public static DatabaseProvider GetProviderFromConnectionString(string connectionString)
    {
        if (connectionString.Contains("DataSource") || connectionString.Contains("Data Source"))
        {
            return DatabaseProvider.Sqlite;
        }
        else if (connectionString.Contains("Server") || connectionString.Contains("Data Source"))
        {
            return DatabaseProvider.SqlServer;
        }

        return DatabaseProvider.Unknown;
    }

    /// <summary>
    /// Extracts server name from connection string for logging
    /// </summary>
    public static string ExtractServerName(string connectionString)
    {
        try
        {
            var provider = GetProviderFromConnectionString(connectionString);

            switch (provider)
            {
                case DatabaseProvider.SqlServer:
                    var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
                    return sqlBuilder.DataSource;

                case DatabaseProvider.Sqlite:
                    // For SQLite, return the database file name
                    var sqliteBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
                    return sqliteBuilder.DataSource;

                default:
                    return "Unknown";
            }
        }
        catch
        {
            return "Invalid connection string";
        }
    }

    /// <summary>
    /// Creates a test database with seeded data
    /// </summary>
    public static async Task<AppDbContext> CreateTestDatabaseAsync(IDbContextFactory<AppDbContext> contextFactory)
    {
        var context = contextFactory.CreateDbContext();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed with test data if needed
        await SeedTestDataAsync(context);

        return context;
    }

    /// <summary>
    /// Seeds test data into the database
    /// </summary>
    private static async Task SeedTestDataAsync(AppDbContext context)
    {
        // Add any common test data here
        // This can be extended by test classes as needed
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Database provider enumeration
/// </summary>
public enum DatabaseProvider
{
    Unknown,
    SqlServer,
    Sqlite,
    InMemory
}