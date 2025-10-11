using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using WileyWidget.Data;
using Xunit;

namespace WileyWidget.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that require SQL Server via TestContainers.
/// Manages the lifecycle of the SQL Server container and database context.
/// </summary>
public abstract class SqlServerTestBase : IAsyncLifetime
{
    private MsSqlContainer _sqlContainer;
    protected string ConnectionString { get; private set; }

    /// <summary>
    /// Initializes the SQL Server container before tests run.
    /// </summary>
    public async Task InitializeAsync()
    {
        // Create and start SQL Server container
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong!Passw0rd")
            .WithCleanUp(true)
            .Build();

        await _sqlContainer.StartAsync();
        
        ConnectionString = _sqlContainer.GetConnectionString();
    }

    /// <summary>
    /// Cleans up the SQL Server container after tests complete.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_sqlContainer != null)
        {
            await _sqlContainer.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new database context with the TestContainers connection string.
    /// </summary>
    protected AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Resets the database to a clean state between tests.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        using var context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
