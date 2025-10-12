using System;
using System.Linq;
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
public class DatabasePerformanceBenchmarks : IDisposable
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
        var sqliteDependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqliteContext);
        await PerformanceTestDataHelper.EnsureBenchmarkSeedAsync(_sqliteContext, sqliteDependencies);

        // Setup SQL Server (would need TestContainers for real benchmarks)
        // For now, we'll use LocalDB if available
        _sqlServerConnectionString = @"Server=(localdb)\mssqllocaldb;Database=WileyWidgetBenchmark;Trusted_Connection=true;";
        
        var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_sqlServerConnectionString)
            .Options;
        
        _sqlServerContext = new AppDbContext(sqlServerOptions);
        await _sqlServerContext.Database.EnsureCreatedAsync();
        var sqlDependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqlServerContext);
        await PerformanceTestDataHelper.EnsureBenchmarkSeedAsync(_sqlServerContext, sqlDependencies);
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup().GetAwaiter().GetResult();
        }
    }

    [Benchmark]
    public async Task InsertSingleAccount_SQLite()
    {
        var dependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqliteContext);
        var existingCount = await _sqliteContext.MunicipalAccounts.CountAsync();
        var accountNumber = PerformanceTestDataHelper.BuildAccountNumber(410, existingCount);
        var account = TestDataBuilder.CreateMunicipalAccount(accountNumber, "Benchmark Account", 1000m, dependencies.DepartmentId, dependencies.BudgetPeriodId);
        _sqliteContext.MunicipalAccounts.Add(account);
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task InsertSingleAccount_SQLServer()
    {
        var dependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqlServerContext);
        var existingCount = await _sqlServerContext.MunicipalAccounts.CountAsync();
        var accountNumber = PerformanceTestDataHelper.BuildAccountNumber(410, existingCount);
        var account = TestDataBuilder.CreateMunicipalAccount(accountNumber, "Benchmark Account", 1000m, dependencies.DepartmentId, dependencies.BudgetPeriodId);
        _sqlServerContext.MunicipalAccounts.Add(account);
        await _sqlServerContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task QueryAccountById_SQLite()
    {
        var account = await _sqliteContext.MunicipalAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber.Value == PerformanceTestDataHelper.BenchmarkAccountNumber);
    }

    [Benchmark]
    public async Task QueryAccountById_SQLServer()
    {
        var account = await _sqlServerContext.MunicipalAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber.Value == PerformanceTestDataHelper.BenchmarkAccountNumber);
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
            .FirstAsync(a => a.AccountNumber.Value == PerformanceTestDataHelper.BenchmarkAccountNumber);
        
        account.Balance += 100m;
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task UpdateAccount_SQLServer()
    {
        var account = await _sqlServerContext.MunicipalAccounts
            .FirstAsync(a => a.AccountNumber.Value == PerformanceTestDataHelper.BenchmarkAccountNumber);
        
        account.Balance += 100m;
        await _sqlServerContext.SaveChangesAsync();
    }
}

/// <summary>
/// Benchmarks for bulk operations comparing SQLite and SQL Server.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class BulkOperationsBenchmarks : IDisposable
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
        var sqliteDependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqliteContext);
        await PerformanceTestDataHelper.EnsureBenchmarkSeedAsync(_sqliteContext, sqliteDependencies);

        var sqlServerConnectionString = @"Server=(localdb)\mssqllocaldb;Database=WileyWidgetBulkBenchmark;Trusted_Connection=true;";
        var sqlServerOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(sqlServerConnectionString)
            .Options;
        
        _sqlServerContext = new AppDbContext(sqlServerOptions);
        await _sqlServerContext.Database.EnsureCreatedAsync();
        var sqlDependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqlServerContext);
        await PerformanceTestDataHelper.EnsureBenchmarkSeedAsync(_sqlServerContext, sqlDependencies);
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup().GetAwaiter().GetResult();
        }
    }

    [Benchmark]
    public async Task BulkInsert_SQLite()
    {
        var dependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqliteContext);
        var existingCount = await _sqliteContext.MunicipalAccounts.CountAsync();
        var accounts = Enumerable.Range(0, BatchSize)
            .Select(i =>
            {
                var accountNumber = PerformanceTestDataHelper.BuildAccountNumber(500, existingCount + i);
                return TestDataBuilder.CreateMunicipalAccount(accountNumber, $"Bulk Account {i}", 1000m, dependencies.DepartmentId, dependencies.BudgetPeriodId);
            })
            .ToList();

        _sqliteContext.MunicipalAccounts.AddRange(accounts);
        await _sqliteContext.SaveChangesAsync();
    }

    [Benchmark]
    public async Task BulkInsert_SQLServer()
    {
        var dependencies = await PerformanceTestDataHelper.EnsureAccountDependenciesAsync(_sqlServerContext);
        var existingCount = await _sqlServerContext.MunicipalAccounts.CountAsync();
        var accounts = Enumerable.Range(0, BatchSize)
            .Select(i =>
            {
                var accountNumber = PerformanceTestDataHelper.BuildAccountNumber(500, existingCount + i);
                return TestDataBuilder.CreateMunicipalAccount(accountNumber, $"Bulk Account {i}", 1000m, dependencies.DepartmentId, dependencies.BudgetPeriodId);
            })
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

internal static class PerformanceTestDataHelper
{
    public const string BenchmarkAccountNumber = "400-0001";

    public static async Task<(int DepartmentId, int BudgetPeriodId)> EnsureAccountDependenciesAsync(AppDbContext context)
    {
        var department = await context.Departments.FirstOrDefaultAsync();
        if (department == null)
        {
            department = TestDataBuilder.CreateDepartment();
            context.Departments.Add(department);
            await context.SaveChangesAsync();
        }

        var budgetPeriod = await context.BudgetPeriods.FirstOrDefaultAsync();
        if (budgetPeriod == null)
        {
            budgetPeriod = TestDataBuilder.CreateBudgetPeriod();
            context.BudgetPeriods.Add(budgetPeriod);
            await context.SaveChangesAsync();
        }

        return (department.Id, budgetPeriod.Id);
    }

    public static string BuildAccountNumber(int baseSegment, int index)
    {
        return $"{baseSegment:D3}-{index + 1:0000}";
    }

    public static async Task EnsureBenchmarkSeedAsync(AppDbContext context, (int DepartmentId, int BudgetPeriodId) dependencies)
    {
        if (!await context.MunicipalAccounts.AnyAsync(a => a.AccountNumber.Value == BenchmarkAccountNumber))
        {
            var account = TestDataBuilder.CreateMunicipalAccount(BenchmarkAccountNumber, "Benchmark Account", 5000m, dependencies.DepartmentId, dependencies.BudgetPeriodId);
            context.MunicipalAccounts.Add(account);
            await context.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Entry point for running benchmarks.


