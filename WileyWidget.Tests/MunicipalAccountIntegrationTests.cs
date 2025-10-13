using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.Tests;

/// <summary>
/// Integration tests for Municipal Account functionality
/// </summary>
public class MunicipalAccountIntegrationTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly AppDbContext _context;
    private readonly MunicipalAccountRepository _repository;

    public MunicipalAccountIntegrationTests()
    {
        // Use SQLite in-memory database for testing (Microsoft recommended approach)
        // Provides better SQL compatibility than EF Core In-Memory provider
        var databaseName = $"MunicipalAccountTest_{Guid.NewGuid()}";
        _contextFactory = TestDbContextFactory.CreateSqliteInMemory(databaseName);
        _context = _contextFactory.CreateDbContext();
        _repository = new MunicipalAccountRepository(_contextFactory);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task MunicipalAccountRepository_AddAccount_Succeeds()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000"),
            Name = "General Fund - Cash",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        // Act
        var result = await _repository.AddAsync(account);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101-1000", result.AccountNumber.ToString());
        Assert.Equal(AccountType.Asset, result.Type);
        Assert.Equal(MunicipalFundType.General, result.Fund);
    }

    [Fact]
    public async Task MunicipalAccountRepository_GetAllAccounts_ReturnsAllAccounts()
    {
        // Arrange
        var account1 = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000"),
            Name = "General Fund - Cash",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        var account2 = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("201-2000"),
            Name = "General Fund - Salaries",
            Type = AccountType.Expense,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 0.00m,
            BudgetAmount = 45000.00m,
            IsActive = true
        };

        await _repository.AddAsync(account1);
        await _repository.AddAsync(account2);

        // Act
        var accounts = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(2, accounts.Count());
        Assert.Contains(accounts, a => a.AccountNumber.ToString() == "101-1000");
        Assert.Contains(accounts, a => a.AccountNumber.ToString() == "201-2000");
    }

    [Fact]
    public async Task MunicipalAccountRepository_GetByFund_ReturnsFilteredAccounts()
    {
        // Arrange
        var generalFundAccount = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000"),
            Name = "General Fund - Cash",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        var EnterpriseAccount = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("102-1000"),
            Name = "Special Revenue Fund - Grants",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.Enterprise,
            FundClass = FundClass.Proprietary,
            Balance = 25000.00m,
            BudgetAmount = 30000.00m,
            IsActive = true
        };

        await _repository.AddAsync(generalFundAccount);
        await _repository.AddAsync(EnterpriseAccount);

        // Act
        var generalFundAccounts = await _repository.GetByFundAsync(MunicipalFundType.General);

        // Assert
        Assert.Single(generalFundAccounts);
        Assert.Equal("101-1000", generalFundAccounts.First().AccountNumber.ToString());
        Assert.Equal(MunicipalFundType.General, generalFundAccounts.First().Fund);
    }

    [Fact]
    public void MunicipalAccount_VarianceCalculation_WorksCorrectly()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000"),
            Name = "General Fund - Cash",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        // Act & Assert
        Assert.Equal(5000.00m, account.Variance); // Budget - Balance = 55000 - 50000 = 5000
        Assert.Equal(9.0909m, Math.Round(account.VariancePercent, 4)); // 5000 / 55000 ≈ 9.0909
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _contextFactory.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
