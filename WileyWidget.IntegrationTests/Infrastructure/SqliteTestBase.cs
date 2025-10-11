using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests using SQLite in-memory database.
/// Useful for faster tests that don't require SQL Server specific features.
/// </summary>
public abstract class SqliteTestBase : IDisposable
{
    protected AppDbContext Context { get; private set; }
    private readonly DbContextOptions<AppDbContext> _options;

    protected SqliteTestBase()
    {
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        Context = new AppDbContext(_options);
        Context.Database.OpenConnection();
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// Resets the database to a clean state.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        await Context.Database.EnsureDeletedAsync();
        await Context.Database.EnsureCreatedAsync();
    }

    public void Dispose()
    {
        Context?.Dispose();
    }
}
