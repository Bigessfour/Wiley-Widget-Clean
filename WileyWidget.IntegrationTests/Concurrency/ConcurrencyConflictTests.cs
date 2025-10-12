using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.IntegrationTests.Infrastructure;
using WileyWidget.Models;
using Xunit;

namespace WileyWidget.IntegrationTests.Concurrency;

/// <summary>
/// Tests for EF Core optimistic concurrency control using row versioning.
/// These tests verify that concurrent updates are properly detected and handled.
/// </summary>
public class ConcurrencyConflictTests : SqlServerTestBase
{
    /// <summary>
    /// Seeds required reference data for tests.
    /// </summary>
    private async Task SeedRequiredDataAsync(AppDbContext context)
    {
        // Seed Department (will get Id=1 if table is empty)
        if (!await context.Departments.AnyAsync())
        {
            var department = TestDataBuilder.CreateDepartment("General Government", "GEN GOVT");
            context.Departments.Add(department);
        }

        // Seed BudgetPeriod (will get Id=1 if table is empty)
        if (!await context.BudgetPeriods.AnyAsync())
        {
            var budgetPeriod = TestDataBuilder.CreateBudgetPeriod(2024, "FY2024", BudgetStatus.Adopted);
            context.BudgetPeriods.Add(budgetPeriod);
        }

        await context.SaveChangesAsync();
    }
    [Fact]
    public async Task UpdateAccount_WhenRowVersionMatches_ShouldSucceed()
    {
        // Arrange
        using var context = CreateDbContext();
        await SeedRequiredDataAsync(context);
        var account = TestDataBuilder.CreateMunicipalAccount("101.1", "Concurrent Test");
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        // Act - Update with current row version
        account.Balance = 2000m;
        await context.SaveChangesAsync();
        
        // Assert
        var updated = await context.MunicipalAccounts.FindAsync(account.Id);
        updated.Balance.Should().Be(2000m);
    }

    [Fact]
    public async Task UpdateAccount_WhenConcurrentUpdate_ShouldThrowDbUpdateConcurrencyException()
    {
        // Arrange - Create account in first context
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.2", "Concurrent Test");
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        // Load same account in two different contexts
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // Act - Update in first context
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // Act & Assert - Attempt to update in second context should fail
        account3.Balance = 3000m;
        var act = async () => await context3.SaveChangesAsync();
        
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>()
            .WithMessage("*concurrency*");
    }

    [Fact]
    public async Task UpdateAccount_AfterConcurrencyConflict_CanReloadAndRetry()
    {
        // Arrange
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.7", "Retry Test");
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // First update succeeds
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // Second update fails
        account3.Balance = 3000m;
        
        // Act - Handle concurrency exception with reload
        try
        {
            await context3.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reload the current values from database
            await context3.Entry(account3).ReloadAsync();
            
            // Retry the update with fresh data
            account3.Balance = 3000m;
            await context3.SaveChangesAsync();
        }
        
        // Assert
        using var finalContext = CreateDbContext();
        var final = await finalContext.MunicipalAccounts.FindAsync(accountId);
        final.Balance.Should().Be(3000m);
    }

    [Fact]
    public async Task UpdateAccount_WithClientWins_ShouldOverwriteServerChanges()
    {
        // Arrange
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.3", "Client Wins");
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // First update
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // Second update with client-wins strategy
        account3.Balance = 3000m;
        
        // Act - Implement client-wins concurrency resolution
        try
        {
            await context3.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var databaseValues = await entry.GetDatabaseValuesAsync();
            
            // Client wins - use current values, but update the row version
            entry.OriginalValues.SetValues(databaseValues);
            await context3.SaveChangesAsync();
        }
        
        // Assert
        using var finalContext = CreateDbContext();
        var final = await finalContext.MunicipalAccounts.FindAsync(accountId);
        final.Balance.Should().Be(3000m);
    }

    [Fact]
    public async Task UpdateAccount_WithServerWins_ShouldDiscardClientChanges()
    {
        // Arrange
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.4", "Server Wins");
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // First update
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // Second update with server-wins strategy
        account3.Balance = 3000m;
        
        // Act - Implement server-wins concurrency resolution
        decimal finalBalance = 0;
        try
        {
            await context3.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var databaseValues = await entry.GetDatabaseValuesAsync();
            
            // Server wins - discard current values, keep database values
            entry.CurrentValues.SetValues(databaseValues);
            finalBalance = ((MunicipalAccount)entry.Entity).Balance;
        }
        
        // Assert
        finalBalance.Should().Be(2000m); // Server value preserved
        using var finalContext = CreateDbContext();
        var final = await finalContext.MunicipalAccounts.FindAsync(accountId);
        final.Balance.Should().Be(2000m);
    }

    [Fact]
    public async Task UpdateAccount_WithMergeStrategy_ShouldCombineChanges()
    {
        // Arrange
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.5", "Merge Test", 1000m);
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // User 1 updates balance
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // User 2 updates name (different property)
        account3.Name = "Merged Account Name";
        
        // Act - Implement merge strategy for non-conflicting properties
        try
        {
            await context3.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entry = ex.Entries.Single();
            var currentValues = entry.CurrentValues;
            var databaseValues = await entry.GetDatabaseValuesAsync();
            var originalValues = entry.OriginalValues;
            
            // Merge: keep database values for Balance, client values for Name
            foreach (var property in currentValues.Properties)
            {
                var current = currentValues[property];
                var database = databaseValues[property];
                var original = originalValues[property];
                
                // If client changed the value, keep it; otherwise use database value
                if (!Equals(current, original))
                {
                    currentValues[property] = current; // Client change
                }
                else
                {
                    currentValues[property] = database; // Server change
                }
            }
            
            // Update original values to match database for concurrency check
            entry.OriginalValues.SetValues(databaseValues);
            await context3.SaveChangesAsync();
        }
        
        // Assert - Both changes should be preserved
        using var finalContext = CreateDbContext();
        var final = await finalContext.MunicipalAccounts.FindAsync(accountId);
        final.Balance.Should().Be(2000m); // From User 1
        final.Name.Should().Be("Merged Account Name"); // From User 2
    }

    [Fact]
    public async Task DeleteAccount_WhenConcurrentUpdate_ShouldThrowConcurrencyException()
    {
        // Arrange
        using var context1 = CreateDbContext();
        await SeedRequiredDataAsync(context1);
        var account = TestDataBuilder.CreateMunicipalAccount("101.6", "Delete Test");
        context1.MunicipalAccounts.Add(account);
        await context1.SaveChangesAsync();
        var accountId = account.Id;
        
        using var context2 = CreateDbContext();
        using var context3 = CreateDbContext();
        
        var account2 = await context2.MunicipalAccounts.FindAsync(accountId);
        var account3 = await context3.MunicipalAccounts.FindAsync(accountId);
        
        // Update in one context
        account2.Balance = 2000m;
        await context2.SaveChangesAsync();
        
        // Act & Assert - Delete in another context should fail
        context3.MunicipalAccounts.Remove(account3);
        var act = async () => await context3.SaveChangesAsync();
        
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
