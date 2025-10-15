using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WileyWidget.Tests;

/// <summary>
/// Unit tests for MunicipalAccountRepository
/// Tests all repository methods using in-memory database
/// Follows Microsoft unit testing best practices with GASB-compliant test data
/// </summary>
public sealed class MunicipalAccountRepositoryUnitTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly MunicipalAccountRepository _repository;
    private bool _disposed;

    public MunicipalAccountRepositoryUnitTests()
    {
        // Create SQLite in-memory database for testing
        _contextFactory = TestDbContextFactory.CreateSqliteInMemory("MunicipalAccountTests");
        _repository = new MunicipalAccountRepository(_contextFactory);
        
        // Ensure database schema is created and configure for testing
        using var context = _contextFactory.CreateDbContext();
        context.Database.EnsureCreated();
        
        // Disable foreign key constraints for unit tests
        // This allows testing repository logic without setting up full entity relationships
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _contextFactory?.Dispose();
            }
            _disposed = true;
        }
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new MunicipalAccountRepository(null!));
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange
        var accounts = new List<MunicipalAccount>();
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_WithAccounts_ReturnsOrderedByAccountNumber()
    {
        // Arrange - GASB compliant: 100-199=Assets, 300-399=Equity, 500-699=Expenses
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("501.2000", "Salaries Expense", AccountType.Salaries, MunicipalFundType.General),
            CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General),
            CreateTestAccount("301.3000", "Fund Balance", AccountType.FundBalance, MunicipalFundType.General)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count());
        var resultList = result.ToList();
        Assert.Equal("101.1000", resultList[0].AccountNumber!.ToString());
        Assert.Equal("301.3000", resultList[1].AccountNumber!.ToString());
        Assert.Equal("501.2000", resultList[2].AccountNumber!.ToString());
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveAccounts()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, isActive: true),
            CreateTestAccount("102.1000", "Investments", AccountType.Investments, MunicipalFundType.General, isActive: true),
            CreateTestAccount("103.1000", "Closed Account", AccountType.Cash, MunicipalFundType.General, isActive: false)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetActiveAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, a => Assert.True(a.IsActive));
        Assert.Contains(result, a => a.AccountNumber!.ToString() == "101.1000");
        Assert.Contains(result, a => a.AccountNumber!.ToString() == "102.1000");
    }

    [Fact]
    public async Task GetByFundAsync_ExistingFund_ReturnsFilteredAccounts()
    {
        // Arrange - GASB compliant: 100-199=Assets, 500-699=Expenses
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "General Fund Cash", AccountType.Cash, MunicipalFundType.General, isActive: true),
            CreateTestAccount("501.2000", "General Fund Salaries", AccountType.Salaries, MunicipalFundType.General, isActive: true),
            CreateTestAccount("102.1000", "Water Fund Cash", AccountType.Cash, MunicipalFundType.Water, isActive: true)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByFundAsync(MunicipalFundType.General);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, a => Assert.Equal(MunicipalFundType.General, a.Fund));
        Assert.All(result, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task GetByFundAsync_InactiveAccountsExcluded()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "Active General Fund", AccountType.Cash, MunicipalFundType.General, isActive: true),
            CreateTestAccount("102.1000", "Inactive General Fund", AccountType.Investments, MunicipalFundType.General, isActive: false)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByFundAsync(MunicipalFundType.General);

        // Assert
        Assert.Single(result);
        Assert.Equal("101.1000", result.First().AccountNumber!.ToString());
    }

    [Fact]
    public async Task GetByTypeAsync_ExistingType_ReturnsFilteredAccounts()
    {
        // Arrange - GASB compliant: 100-199=Assets, 500-699=Expenses
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "Cash", AccountType.Cash, MunicipalFundType.General, isActive: true),
            CreateTestAccount("102.1000", "Investments", AccountType.Investments, MunicipalFundType.General, isActive: true),
            CreateTestAccount("501.2000", "Salaries", AccountType.Salaries, MunicipalFundType.General, isActive: true)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByTypeAsync(AccountType.Cash);

        // Assert
        Assert.Single(result);
        Assert.All(result, a => Assert.Equal(AccountType.Cash, a.Type));
        Assert.All(result, a => Assert.True(a.IsActive));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsAccount()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var account = CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, isActive: true);
        var accounts = new List<MunicipalAccount> { account };
        SeedDatabase(accounts);

        // Act - Note: Id will be auto-generated by database
        var allAccounts = await _repository.GetAllAsync();
        var firstAccount = allAccounts.First();
        var result = await _repository.GetByIdAsync(firstAccount.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101.1000", result!.AccountNumber!.ToString());
        Assert.Equal("Cash Account", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Arrange
        var accounts = new List<MunicipalAccount>();
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_ExistingAccountNumber_ReturnsAccount()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, isActive: true)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByAccountNumberAsync("101.1000");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101.1000", result!.AccountNumber!.ToString());
        Assert.Equal("Cash Account", result.Name);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_NonExistingAccountNumber_ReturnsNull()
    {
        // Arrange
        var accounts = new List<MunicipalAccount>();
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetByAccountNumberAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ValidAccount_AddsAndReturnsAccount()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var accounts = new List<MunicipalAccount>();
        SeedDatabase(accounts);
        var account = CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, 
            isActive: true, balance: 50000.00m, budget: 55000.00m);

        // Act
        var result = await _repository.AddAsync(account);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("101.1000", result!.AccountNumber!.ToString());
        Assert.Equal("Cash Account", result.Name);
        Assert.Equal(AccountType.Cash, result.Type);
        Assert.Equal(MunicipalFundType.General, result.Fund);

        // Verify it was added to database
        var savedAccount = await _repository.GetByIdAsync(result.Id);
        Assert.NotNull(savedAccount);
        Assert.Equal("101.1000", savedAccount!.AccountNumber!.ToString());
    }

    [Fact]
    public async Task UpdateAsync_ExistingAccount_UpdatesAndReturnsAccount()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var account = CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General,
            isActive: true, balance: 50000.00m);
        var accounts = new List<MunicipalAccount> { account };
        SeedDatabase(accounts);

        // Get the actual account with its generated Id
        var savedAccount = (await _repository.GetAllAsync()).First();
        savedAccount.Name = "Updated Cash Account";
        savedAccount.Balance = 75000.00m;

        // Act
        var result = await _repository.UpdateAsync(savedAccount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Cash Account", result.Name);
        Assert.Equal(75000.00m, result.Balance);

        // Verify it was updated
        var updatedAccount = await _repository.GetByIdAsync(savedAccount.Id);
        Assert.NotNull(updatedAccount);
        Assert.Equal("Updated Cash Account", updatedAccount.Name);
        Assert.Equal(75000.00m, updatedAccount.Balance);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesAccount()
    {
        // Arrange - GASB compliant: 100-199=Assets
        var account = CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, isActive: true);
        var accounts = new List<MunicipalAccount> { account };
        SeedDatabase(accounts);

        // Get the actual account with its generated Id
        var savedAccount = (await _repository.GetAllAsync()).First();

        // Act
        await _repository.DeleteAsync(savedAccount.Id);

        // Assert - Verify it was deleted
        var deletedAccount = await _repository.GetByIdAsync(savedAccount.Id);
        Assert.Null(deletedAccount);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_DoesNotThrowException()
    {
        // Arrange
        var accounts = new List<MunicipalAccount>();
        SeedDatabase(accounts);

        // Act & Assert - Should not throw exception
        await _repository.DeleteAsync(999);
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_ReturnsActiveAccountsWithBudget()
    {
        // Arrange - GASB compliant: 100-199=Assets, 500-699=Expenses
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("101.1000", "Cash Account", AccountType.Cash, MunicipalFundType.General, 
                isActive: true, balance: 50000.00m, budget: 55000.00m),
            CreateTestAccount("501.2000", "Salaries", AccountType.Salaries, MunicipalFundType.General, 
                isActive: true, balance: 40000.00m, budget: 45000.00m),
            CreateTestAccount("103.1000", "Closed Account", AccountType.Receivables, MunicipalFundType.General, 
                isActive: false, balance: 10000.00m, budget: 15000.00m),
            CreateTestAccount("102.1000", "No Budget Account", AccountType.Investments, MunicipalFundType.General, 
                isActive: true, balance: 25000.00m, budget: 0.00m)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetBudgetAnalysisAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, a => Assert.True(a.IsActive));
        Assert.All(result, a => Assert.True(a.BudgetAmount != 0));
        Assert.Contains(result, a => a.AccountNumber!.ToString() == "101.1000");
        Assert.Contains(result, a => a.AccountNumber!.ToString() == "501.2000");
    }

    [Fact]
    public async Task GetBudgetAnalysisAsync_OrdersByAccountNumber()
    {
        // Arrange - GASB compliant: 100-199=Assets, 500-699=Expenses
        var accounts = new List<MunicipalAccount>
        {
            CreateTestAccount("501.2000", "Salaries", AccountType.Salaries, MunicipalFundType.General, 
                isActive: true, budget: 45000.00m),
            CreateTestAccount("101.1000", "Cash", AccountType.Cash, MunicipalFundType.General, 
                isActive: true, budget: 55000.00m)
        };
        SeedDatabase(accounts);

        // Act
        var result = await _repository.GetBudgetAnalysisAsync();

        // Assert
        Assert.Equal(2, result.Count);
        var resultList = result.ToList();
        Assert.Equal("101.1000", resultList[0].AccountNumber!.ToString());
        Assert.Equal("501.2000", resultList[1].AccountNumber!.ToString());
    }

    [Fact(Skip = "Concurrency tests require database with row versioning - move to integration tests")]
    public async Task UpdateAsync_ConcurrencyConflict_ThrowsConcurrencyConflictException()
    {
        // NOTE: SQLite in-memory doesn't support proper concurrency conflict simulation
        // This test should be moved to integration tests against SQL Server
        // Microsoft best practices: https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
        await Task.CompletedTask;
    }

    [Fact(Skip = "Concurrency tests require database with row versioning - move to integration tests")]
    public async Task DeleteAsync_ConcurrencyConflict_ThrowsConcurrencyConflictException()
    {
        // NOTE: SQLite in-memory doesn't support proper concurrency conflict simulation
        // This test should be moved to integration tests against SQL Server
        // Microsoft best practices: https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
        await Task.CompletedTask;
    }

    private void SeedDatabase(List<MunicipalAccount> accounts)
    {
        using var context = _contextFactory.CreateDbContext();
        
        // Clear existing data
        context.MunicipalAccounts.RemoveRange(context.MunicipalAccounts);
        context.SaveChanges();
        
        // Add new test data with proper GASB-compliant values
        if (accounts != null && accounts.Any())
        {
            // Ensure all accounts have FundClass set for SQLite NOT NULL constraint
            foreach (var account in accounts.Where(a => !a.FundClass.HasValue))
            {
                account.FundClass = GetFundClassForFund(account.Fund);
            }
            
            context.MunicipalAccounts.AddRange(accounts);
            context.SaveChanges();
        }
    }

    /// <summary>
    /// Helper method to get appropriate FundClass for a MunicipalFundType
    /// Following GASB 34 standards
    /// </summary>
    private static FundClass GetFundClassForFund(MunicipalFundType MunicipalFundType)
    {
        return MunicipalFundType switch
        {
            MunicipalFundType.General or MunicipalFundType.SpecialRevenue or MunicipalFundType.CapitalProjects or MunicipalFundType.DebtService => FundClass.Governmental,
            MunicipalFundType.Enterprise or MunicipalFundType.InternalService or MunicipalFundType.Water or MunicipalFundType.Sewer or MunicipalFundType.Trash or MunicipalFundType.Utility => FundClass.Proprietary,
            MunicipalFundType.Trust or MunicipalFundType.Agency => FundClass.Fiduciary,
            _ => FundClass.Governmental // Default to Governmental
        };
    }

    /// <summary>
    /// Creates a GASB-compliant test account with proper AccountType for the account number
    /// Following GASB account number ranges:
    /// - 100-199: Assets (Cash, Investments, Receivables, etc.)
    /// - 200-299: Liabilities (Payables, Debt, etc.)
    /// - 300-399: Equity (RetainedEarnings, FundBalance)
    /// - 400-499: Revenue (Taxes, Fees, Grants, Sales, etc.)
    /// - 500-699: Expenses (Salaries, Supplies, Services, etc.)
    /// </summary>
    private static MunicipalAccount CreateTestAccount(string accountNumber, string name, AccountType accountType, MunicipalFundType fund, bool isActive = true, decimal balance = 0m, decimal budget = 0m)
    {
        return new MunicipalAccount
        {
            AccountNumber = new AccountNumber(accountNumber),
            Name = name,
            Type = accountType,
            Fund = fund,
            FundClass = GetFundClassForFund(fund),
            IsActive = isActive,
            Balance = balance,
            BudgetAmount = budget
        };
    }
}

