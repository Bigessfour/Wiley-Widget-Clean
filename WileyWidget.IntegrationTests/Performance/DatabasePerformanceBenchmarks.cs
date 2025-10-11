using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.IntegrationTests.Infrastructure;
using WileyWidget.Models;

namespace WileyWidget.IntegrationTests.Performance;

/// <summary>
/// Performance benchmarks comparing SQLite vs SQL Server for common database operations.
/// Run with: dotnet run -c Release --project WileyWidget.IntegrationTests --filter *DatabasePerformanceBenchmarks*
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class DatabasePerformanceBenchmarks
{
    private AppDbContext _sqliteContext;
    private AppDbContext _sqlServerContext;
    private string _sqlServerConnectionString;

    [GlobalSetup]
    public async Task Setup()
    {
        // Setup SQLite in-memory
        var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        
        _sqliteContext = new AppDbContext(sqliteOptions);
        await _sqliteContext.Database.OpenConnectionAsync();
        await _sqliteContext.Database.EnsureCreatedAsync();

        // Setup SQL Server (would need TestContainers for real benchmarks)
        // For now, we'll use LocalDB if available
        _sqlServerConnectionString = @"Server=(localdb)\mssqllocaldb;Database=WileyWidgetBenchmark;Trusted_Connection=true;";
        
        var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlServerConnectionString)
            .Options;
        
        _sqlServerContext = new AppDbContext(sqlServerOptions);
        await _sqlServerContext.Database.EnsureCreatedAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_sqliteContext != null)
        {
            await _sqliteContext.DisposeAsync();
        }

        if (_sqlServerContext != null)
        {
            await _sqlServerContext.Database.EnsureDeletedAsync();
            await _sqlServerContext.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task InsertSingleAccount_SQLite()
    {
        var account = TestDataBuilder.CreateMunicipalAccount("Benchmark Account", "BENCH-001");
        _sqliteContext.MunicipalAccounts.Add(account);
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task InsertSingleAccount_SQLServer()
    {
        var account = TestDataBuilder.CreateMunicipalAccount("Benchmark Account", "BENCH-001");
        _sqlServerContext.MunicipalAccounts.Add(account);
        await _sqlServerContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task QueryAccountById_SQLite()
    {
        var account = await _sqliteContext.MunicipalAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber.Value == "BENCH-001");
    }

    [Benchmark]
    public async Task QueryAccountById_SQLServer()
    {
        var account = await _sqlServerContext.MunicipalAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber.Value == "BENCH-001");
    }

    [Benchmark]
    public async Task QueryWithFilter_SQLite()
    {
        var accounts = await _sqliteContext.MunicipalAccounts
            .Where(a => a.Balance > 1000m && a.IsActive)
            .ToListAsync();
    }

    [Benchmark]
    public async Task QueryWithFilter_SQLServer()
    {
        var accounts = await _sqlServerContext.MunicipalAccounts
            .Where(a => a.Balance > 1000m && a.IsActive)
            .ToListAsync();
    }

    [Benchmark]
    public async Task UpdateAccount_SQLite()
    {
        var account = await _sqliteContext.MunicipalAccounts
            .FirstAsync(a => a.AccountNumber.Value == "BENCH-001");
        
        account.Balance += 100m;
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task UpdateAccount_SQLServer()
    {
        var account = await _sqlServerContext.MunicipalAccounts
            .FirstAsync(a => a.AccountNumber.Value == "BENCH-001");
        
        account.Balance += 100m;
        await _sqlServerContext.SaveChangesAsync();
    }
}

/// <summary>
/// Benchmarks for bulk operations comparing SQLite and SQL Server.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class BulkOperationsBenchmarks
{
    private AppDbContext _sqliteContext;
    private AppDbContext _sqlServerContext;
    private const int BatchSize = 100;

    [GlobalSetup]
    public async Task Setup()
    {
        var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        
        _sqliteContext = new AppDbContext(sqliteOptions);
        await _sqliteContext.Database.OpenConnectionAsync();
        await _sqliteContext.Database.EnsureCreatedAsync();

        var sqlServerConnectionString = @"Server=(localdb)\mssqllocaldb;Database=WileyWidgetBulkBenchmark;Trusted_Connection=true;";
        var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(sqlServerConnectionString)
            .Options;
        
        _sqlServerContext = new AppDbContext(sqlServerOptions);
        await _sqlServerContext.Database.EnsureCreatedAsync();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        if (_sqliteContext != null)
        {
            await _sqliteContext.DisposeAsync();
        }

        if (_sqlServerContext != null)
        {
            await _sqlServerContext.Database.EnsureDeletedAsync();
            await _sqlServerContext.DisposeAsync();
        }
    }

    [Benchmark]
    public async Task BulkInsert_SQLite()
    {
        var accounts = Enumerable.Range(1, BatchSize)
            .Select(i => TestDataBuilder.CreateMunicipalAccount($"Bulk Account {i}", $"BULK-{i:D4}"))
            .ToList();

        _sqliteContext.MunicipalAccounts.AddRange(accounts);
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkInsert_SQLServer()
    {
        var accounts = Enumerable.Range(1, BatchSize)
            .Select(i => TestDataBuilder.CreateMunicipalAccount($"Bulk Account {i}", $"BULK-{i:D4}"))
            .ToList();

        _sqlServerContext.MunicipalAccounts.AddRange(accounts);
        await _sqlServerContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task QueryLargeResultSet_SQLite()
    {
        var accounts = await _sqliteContext.MunicipalAccounts
            .Take(BatchSize)
            .ToListAsync();
    }

    [Benchmark]
    public async Task QueryLargeResultSet_SQLServer()
    {
        var accounts = await _sqlServerContext.MunicipalAccounts
            .Take(BatchSize)
            .ToListAsync();
    }

    [Benchmark]
    public async Task ComplexJoinQuery_SQLite()
    {
        var results = await _sqliteContext.MunicipalAccounts
            .Include(a => a.Transactions)
            .Where(a => a.IsActive)
            .Take(50)
            .ToListAsync();
    }

    [Benchmark]
    public async Task ComplexJoinQuery_SQLServer()
    {
        var results = await _sqlServerContext.MunicipalAccounts
            .Include(a => a.Transactions)
            .Where(a => a.IsActive)
            .Take(50)
            .ToListAsync();
    }
}

/// <summary>
/// Entry point for running benchmarks.


