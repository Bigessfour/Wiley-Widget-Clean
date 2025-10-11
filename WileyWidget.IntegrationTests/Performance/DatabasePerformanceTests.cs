using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using WileyWidget.Data;
using WileyWidget.IntegrationTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using WileyWidget.Models;

namespace WileyWidget.IntegrationTests.Performance;

/// <summary>
/// Performance comparison tests between SQLite and SQL Server.
/// These tests measure execution time for common operations.
/// </summary>
public class DatabasePerformanceTests : SqlServerTestBase
{
    private readonly ITestOutputHelper _output;

    public DatabasePerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CompareInsertPerformance_SingleRecord()
    {
        // Arrange
        var sqliteTime = await MeasureSqliteInsert(1);
        var sqlServerTime = await MeasureSqlServerInsert(1);

        // Output
        _output.WriteLine($"SQLite Insert (1 record): {sqliteTime}ms");
        _output.WriteLine($"SQL Server Insert (1 record): {sqlServerTime}ms");
        _output.WriteLine($"Difference: {Math.Abs(sqliteTime - sqlServerTime)}ms");

        // Assert - Just verify both completed successfully
        sqliteTime.Should().BeGreaterThan(0);
        sqlServerTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompareInsertPerformance_BulkRecords()
    {
        // Arrange
        var sqliteTime = await MeasureSqliteInsert(100);
        var sqlServerTime = await MeasureSqlServerInsert(100);

        // Output
        _output.WriteLine($"SQLite Bulk Insert (100 records): {sqliteTime}ms");
        _output.WriteLine($"SQL Server Bulk Insert (100 records): {sqlServerTime}ms");
        _output.WriteLine($"Difference: {Math.Abs(sqliteTime - sqlServerTime)}ms");
        _output.WriteLine($"Ratio (SQLite/SQLServer): {(double)sqliteTime / sqlServerTime:F2}x");

        // Assert
        sqliteTime.Should().BeGreaterThan(0);
        sqlServerTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompareQueryPerformance_SimpleFilter()
    {
        // Arrange - Seed both databases
        await SeedSqliteDatabase(50);
        await SeedSqlServerDatabase(50);

        // Act
        var sqliteTime = await MeasureSqliteQuery();
        var sqlServerTime = await MeasureSqlServerQuery();

        // Output
        _output.WriteLine($"SQLite Query: {sqliteTime}ms");
        _output.WriteLine($"SQL Server Query: {sqlServerTime}ms");
        _output.WriteLine($"Difference: {Math.Abs(sqliteTime - sqlServerTime)}ms");

        // Assert
        sqliteTime.Should().BeGreaterThan(0);
        sqlServerTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompareUpdatePerformance()
    {
        // Arrange
        await SeedSqliteDatabase(10);
        await SeedSqlServerDatabase(10);

        // Act
        var sqliteTime = await MeasureSqliteUpdate();
        var sqlServerTime = await MeasureSqlServerUpdate();

        // Output
        _output.WriteLine($"SQLite Update: {sqliteTime}ms");
        _output.WriteLine($"SQL Server Update: {sqlServerTime}ms");
        _output.WriteLine($"Difference: {Math.Abs(sqliteTime - sqlServerTime)}ms");

        // Assert
        sqliteTime.Should().BeGreaterThan(0);
        sqlServerTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CompareComplexQueryPerformance()
    {
        // Arrange
        await SeedSqliteWithRelationships(20);
        await SeedSqlServerWithRelationships(20);

        // Act
        var sqliteTime = await MeasureSqliteComplexQuery();
        var sqlServerTime = await MeasureSqlServerComplexQuery();

        // Output
        _output.WriteLine($"SQLite Complex Query (with joins): {sqliteTime}ms");
        _output.WriteLine($"SQL Server Complex Query (with joins): {sqlServerTime}ms");
        _output.WriteLine($"Difference: {Math.Abs(sqliteTime - sqlServerTime)}ms");

        // Assert
        sqliteTime.Should().BeGreaterThan(0);
        sqlServerTime.Should().BeGreaterThan(0);
    }

    #region SQLite Measurement Helpers

    private async Task<long> MeasureSqliteInsert(int count)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();

        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < count; i++)
        {
            var account = TestDataBuilder.CreateMunicipalAccount($"SQLite Account {i}", $"SQLITE-{i:D4}");
            context.MunicipalAccounts.Add(account);
        }
        
        await context.SaveChangesAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqliteQuery()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedContext(context, 50);

        var sw = Stopwatch.StartNew();
        var results = await context.MunicipalAccounts
            .Where(a => a.Balance > 500m && a.IsActive)
            .ToListAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqliteUpdate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedContext(context, 10);

        var sw = Stopwatch.StartNew();
        var accounts = await context.MunicipalAccounts.ToListAsync();
        foreach (var account in accounts)
        {
            account.Balance += 100m;
        }
        await context.SaveChangesAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqliteComplexQuery()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedContextWithRelationships(context, 20);

        var sw = Stopwatch.StartNew();
        var results = await context.MunicipalAccounts
            .Include(a => a.Transactions)
            .Where(a => a.IsActive)
            .ToListAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task SeedSqliteDatabase(int count)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedContext(context, count);
    }

    private async Task SeedSqliteWithRelationships(int count)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var context = new AppDbContext(options);
        await context.Database.OpenConnectionAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedContextWithRelationships(context, count);
    }

    #endregion

    #region SQL Server Measurement Helpers

    private async Task<long> MeasureSqlServerInsert(int count)
    {
        using var context = CreateDbContext();
        
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < count; i++)
        {
            var account = TestDataBuilder.CreateMunicipalAccount($"SQL Server Account {i}", $"SQLSRV-{i:D4}");
            context.MunicipalAccounts.Add(account);
        }
        
        await context.SaveChangesAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqlServerQuery()
    {
        using var context = CreateDbContext();

        var sw = Stopwatch.StartNew();
        var results = await context.MunicipalAccounts
            .Where(a => a.Balance > 500m && a.IsActive)
            .ToListAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqlServerUpdate()
    {
        using var context = CreateDbContext();

        var sw = Stopwatch.StartNew();
        var accounts = await context.MunicipalAccounts.Take(10).ToListAsync();
        foreach (var account in accounts)
        {
            account.Balance += 100m;
        }
        await context.SaveChangesAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task<long> MeasureSqlServerComplexQuery()
    {
        using var context = CreateDbContext();

        var sw = Stopwatch.StartNew();
        var results = await context.MunicipalAccounts
            .Include(a => a.Transactions)
            .Where(a => a.IsActive)
            .ToListAsync();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    private async Task SeedSqlServerDatabase(int count)
    {
        using var context = CreateDbContext();
        await SeedContext(context, count);
    }

    private async Task SeedSqlServerWithRelationships(int count)
    {
        using var context = CreateDbContext();
        await SeedContextWithRelationships(context, count);
    }

    #endregion

    #region Seeding Helpers

    private async Task SeedContext(AppDbContext context, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var account = TestDataBuilder.CreateMunicipalAccount($"Account {i}", $"ACC-{i:D4}", 1000m + (i * 100));
            context.MunicipalAccounts.Add(account);
        }
        await context.SaveChangesAsync();
    }

    private async Task SeedContextWithRelationships(AppDbContext context, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var account = TestDataBuilder.CreateMunicipalAccount($"Account {i}", $"ACC-{i:D4}", 1000m + (i * 100));
            context.MunicipalAccounts.Add(account);
        }
        await context.SaveChangesAsync();

        var accounts = await context.MunicipalAccounts.ToListAsync();
        foreach (var account in accounts)
        {
            var transaction = TestDataBuilder.CreateTransaction(account.Id, 50m, $"Transaction for {account.Name}");
            context.Transactions.Add(transaction);
        }
        await context.SaveChangesAsync();
    }

    #endregion
}

