using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.LifecycleTests;

internal sealed class SqliteAppDbContextFactory : IDbContextFactory<AppDbContext>, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    private SqliteAppDbContextFactory(SqliteConnection connection, DbContextOptions<AppDbContext> options)
    {
        _connection = connection;
        _options = options;
    }

    public static async Task<SqliteAppDbContextFactory> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync().ConfigureAwait(false);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .Options;

        // Ensure schema is created once for the shared connection.
        await using (var context = new SqliteTestAppDbContext(options))
        {
            await context.Database.EnsureDeletedAsync().ConfigureAwait(false);
            await context.Database.EnsureCreatedAsync().ConfigureAwait(false);
        }

        return new SqliteAppDbContextFactory(connection, options);
    }

    public AppDbContext CreateDbContext()
    {
#pragma warning disable CA2000 // Caller is responsible for disposing created context
        return new SqliteTestAppDbContext(_options);
#pragma warning restore CA2000
    }

    public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
#pragma warning disable CA2000 // Caller is responsible for disposing created context
        return new ValueTask<AppDbContext>(new SqliteTestAppDbContext(_options));
#pragma warning restore CA2000
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
