using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.Tests;

/// <summary>
/// Test implementation of IDbContextFactory for unit testing
/// Following Microsoft best practices for database testing with SQLite in-memory
/// </summary>
public class TestDbContextFactory : IDbContextFactory<AppDbContext>, IDisposable
{
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly TestDatabaseConnection? _connection;
    private bool _disposed;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options, TestDatabaseConnection? connection = null)
    {
        _options = options;
        _connection = connection;
    }

    public AppDbContext CreateDbContext()
    {
        return new TestAppDbContext(_options);
    }

    /// <summary>
    /// Creates a test database factory using SQLite in-memory database
    /// Following Microsoft best practices for testing without production database
    /// </summary>
    /// <param name="databaseName">Unique database name to ensure test isolation</param>
    public static TestDbContextFactory CreateSqliteInMemory(string? databaseName = null)
    {
        // Use unique database name for each test to prevent interference
        // If no name provided, generate a GUID-based name
        var dbName = databaseName ?? $"TestDb_{Guid.NewGuid()}";

        var connection = new TestDatabaseConnection(dbName);
        connection.Open(); // Keep connection open for in-memory SQLite

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new TestDbContextFactory(options, connection);
    }

/// <summary>
/// Creates a test database factory for integration tests using WebApplicationFactory
/// Following Microsoft best practices for ASP.NET Core integration testing
/// </summary>
public static TestDbContextFactory CreateForIntegrationTest()
{
    // For integration tests, use a unique database name to avoid conflicts
    var databaseName = $"IntegrationTest_{Guid.NewGuid()}";
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite($"DataSource={databaseName}.db")
        .Options;

    return new TestDbContextFactory(options);
}

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _connection?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Test-specific DbContext that configures RowVersion for SQLite compatibility
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure RowVersion for SQLite test compatibility
        // Set default value for RowVersion to avoid NOT NULL constraint issues
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProperty = entityType.FindProperty("RowVersion");
            if (rowVersionProperty != null && rowVersionProperty.ClrType == typeof(byte[]))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<byte[]>("RowVersion")
                    .HasDefaultValue(new byte[8])
                    .ValueGeneratedOnAddOrUpdate();
            }

            // Set default audit field values for testing
            var createdByProperty = entityType.FindProperty("CreatedBy");
            if (createdByProperty != null && createdByProperty.ClrType == typeof(string))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<string>("CreatedBy")
                    .HasDefaultValue("TestUser");
            }

            var modifiedByProperty = entityType.FindProperty("ModifiedBy");
            if (modifiedByProperty != null && modifiedByProperty.ClrType == typeof(string))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property<string>("ModifiedBy")
                    .HasDefaultValue("TestUser");
            }
        }
    }
}

/// <summary>
/// SQLite connection wrapper for in-memory testing
/// Ensures connection stays open for the lifetime of tests
/// </summary>
public class TestDatabaseConnection : Microsoft.Data.Sqlite.SqliteConnection
{
    public TestDatabaseConnection(string? databaseName = null)
        : base($"DataSource={databaseName ?? ":memory:"};Mode=Memory;Cache=Shared")
    {
    }
}