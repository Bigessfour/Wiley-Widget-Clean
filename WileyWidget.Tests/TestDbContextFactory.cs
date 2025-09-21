using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;

namespace WileyWidget.Tests;

/// <summary>
/// Test implementation of IDbContextFactory for unit testing
/// </summary>
public class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}