using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Unit tests for MunicipalAccountRepository
/// Tests all repository methods using in-memory database
/// Complements existing integration tests
/// </summary>
public class MunicipalAccountRepositoryUnitTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly AppDbContext _context;
    private readonly MunicipalAccountRepository _repository;

    public MunicipalAccountRepositoryUnitTests()
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
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MunicipalAccountRepository(null));
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_WithAccounts_ReturnsOrderedByAccountNumber()
    {
        // Arrange
        var account1 = new MunicipalAccount
        {
            AccountNumber = "201-2000",
            Name = "Salaries Expense",
            Type = AccountType.Expense,
            Fund = FundType.General
        };
        var account2 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General
        };
        var account3 = new MunicipalAccount
        {
            AccountNumber = "301-3000",
            Name = "Retained Earnings",
            Type = AccountType.Equity,
            Fund = FundType.General
        };

        await _context.MunicipalAccounts.AddRangeAsync(account1, account2, account3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count);
        var accounts = result.ToList();
        Assert.Equal("101-1000", accounts[0].AccountNumber);
        Assert.Equal("201-2000", accounts[1].AccountNumber);
        Assert.Equal("301-3000", accounts[2].AccountNumber);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveAccounts()
    {
        // Arrange
        var activeAccount1 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var activeAccount2 = new MunicipalAccount
        {
            AccountNumber = "102-1000",
            Name = "Investments",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var inactiveAccount = new MunicipalAccount
        {
            AccountNumber = "999-9999",
            Name = "Closed Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = false
        };

        await _context.MunicipalAccounts.AddRangeAsync(activeAccount1, activeAccount2, inactiveAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.True(a.IsActive));
        Assert.Contains(result, a => a.AccountNumber == "101-1000");
        Assert.Contains(result, a => a.AccountNumber == "102-1000");
    }

    [Fact]
    public async Task GetByFundAsync_ExistingFund_ReturnsFilteredAccounts()
    {
        // Arrange
        var generalFundAccount1 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "General Fund Cash",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var generalFundAccount2 = new MunicipalAccount
        {
            AccountNumber = "201-2000",
            Name = "General Fund Salaries",
            Type = AccountType.Expense,
            Fund = FundType.General,
            IsActive = true
        };
        var waterFundAccount = new MunicipalAccount
        {
            AccountNumber = "102-1000",
            Name = "Water Fund Cash",
            Type = AccountType.Asset,
            Fund = FundType.Water,
            IsActive = true
        };

        await _context.MunicipalAccounts.AddRangeAsync(generalFundAccount1, generalFundAccount2, waterFundAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFundAsync(FundType.General);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(FundType.General, a.Fund));
        Assert.All(result, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task GetByFundAsync_InactiveAccountsExcluded()
    {
        // Arrange
        var activeAccount = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Active General Fund",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var inactiveAccount = new MunicipalAccount
        {
            AccountNumber = "102-1000",
            Name = "Inactive General Fund",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = false
        };

        await _context.MunicipalAccounts.AddRangeAsync(activeAccount, inactiveAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByFundAsync(FundType.General);

        // Assert
        Assert.Single(result);
        Assert.Equal("101-1000", result.First().AccountNumber);
    }

    [Fact]
    public async Task GetByTypeAsync_ExistingType_ReturnsFilteredAccounts()
    {
        // Arrange
        var assetAccount1 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var assetAccount2 = new MunicipalAccount
        {
            AccountNumber = "102-1000",
            Name = "Investments",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        var expenseAccount = new MunicipalAccount
        {
            AccountNumber = "201-2000",
            Name = "Salaries",
            Type = AccountType.Expense,
            Fund = FundType.General,
            IsActive = true
        };

        await _context.MunicipalAccounts.AddRangeAsync(assetAccount1, assetAccount2, expenseAccount);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTypeAsync(AccountType.Asset);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.Equal(AccountType.Asset, a.Type));
        Assert.All(result, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        _context.MunicipalAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(account.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
        Assert.Equal("101-1000", result.AccountNumber);
        Assert.Equal("Cash Account", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_ExistingAccountNumber_ReturnsAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        _context.MunicipalAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByAccountNumberAsync("101-1000");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101-1000", result.AccountNumber);
        Assert.Equal("Cash Account", result.Name);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_NonExistingAccountNumber_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByAccountNumberAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ValidAccount_AddsAndReturnsAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        // Act
        var result = await _repository.AddAsync(account);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101-1000", result.AccountNumber);
        Assert.Equal("Cash Account", result.Name);
        Assert.Equal(AccountType.Asset, result.Type);
        Assert.Equal(FundType.General, result.Fund);

        // Verify it was added to database
        var savedAccount = await _context.MunicipalAccounts.FindAsync(result.Id);
        Assert.NotNull(savedAccount);
        Assert.Equal("101-1000", savedAccount.AccountNumber);
    }

    [Fact]
    public async Task UpdateAsync_ExistingAccount_UpdatesAndReturnsAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            Balance = 50000.00m,
            IsActive = true
        };
        _context.MunicipalAccounts.Add(account);
        await _context.SaveChangesAsync();

        account.Name = "Updated Cash Account";
        account.Balance = 75000.00m;

        // Act
        var result = await _repository.UpdateAsync(account);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Cash Account", result.Name);
        Assert.Equal(75000.00m, result.Balance);

        // Verify it was updated in database
        var updatedAccount = await _context.MunicipalAccounts.FindAsync(account.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal("Updated Cash Account", updatedAccount.Name);
        Assert.Equal(75000.00m, updatedAccount.Balance);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            IsActive = true
        };
        _context.MunicipalAccounts.Add(account);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(account.Id);

        // Assert - Verify it was deleted from database
        var deletedAccount = await _context.MunicipalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == account.Id);
        Assert.Null(deletedAccount);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_DoesNotThrowException()
    {
        // Act & Assert - Should not throw exception
        await _repository.DeleteAsync(999);
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_ReturnsActiveAccountsWithBudget()
    {
        // Arrange
        var accountWithBudget1 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            Balance = 50000.00m,
            BudgetAmount = 55000.00m,
            IsActive = true
        };
        var accountWithBudget2 = new MunicipalAccount
        {
            AccountNumber = "201-2000",
            Name = "Salaries",
            Type = AccountType.Expense,
            Fund = FundType.General,
            Balance = 40000.00m,
            BudgetAmount = 45000.00m,
            IsActive = true
        };
        var inactiveAccount = new MunicipalAccount
        {
            AccountNumber = "999-9999",
            Name = "Closed Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            Balance = 10000.00m,
            BudgetAmount = 15000.00m,
            IsActive = false
        };
        var accountWithoutBudget = new MunicipalAccount
        {
            AccountNumber = "102-1000",
            Name = "No Budget Account",
            Type = AccountType.Asset,
            Fund = FundType.General,
            Balance = 25000.00m,
            BudgetAmount = 0.00m,
            IsActive = true
        };

        await _context.MunicipalAccounts.AddRangeAsync(accountWithBudget1, accountWithBudget2, inactiveAccount, accountWithoutBudget);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBudgetAnalysisAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.True(a.IsActive));
        Assert.All(result, a => Assert.True(a.BudgetAmount != 0));
        Assert.Contains(result, a => a.AccountNumber == "101-1000");
        Assert.Contains(result, a => a.AccountNumber == "201-2000");
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_OrdersByAccountNumber()
    {
        // Arrange
        var account1 = new MunicipalAccount
        {
            AccountNumber = "201-2000",
            Name = "Salaries",
            Type = AccountType.Expense,
            Fund = FundType.General,
            BudgetAmount = 45000.00m,
            IsActive = true
        };
        var account2 = new MunicipalAccount
        {
            AccountNumber = "101-1000",
            Name = "Cash",
            Type = AccountType.Asset,
            Fund = FundType.General,
            BudgetAmount = 55000.00m,
            IsActive = true
        };

        await _context.MunicipalAccounts.AddRangeAsync(account1, account2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBudgetAnalysisAsync();

        // Assert
        Assert.Equal(2, result.Count);
        var accounts = result.ToList();
        Assert.Equal("101-1000", accounts[0].AccountNumber);
        Assert.Equal("201-2000", accounts[1].AccountNumber);
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