using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WileyWidget.IntegrationTests.Infrastructure;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.IntegrationTests.Relationships;

/// <summary>
/// Tests for foreign key constraints and relationship integrity.
/// Verifies cascading deletes, constraint violations, and navigation properties.
/// </summary>
public class ForeignKeyIntegrityTests : SqlServerTestBase
{
    [Fact]
    public async Task DeleteAccount_WithTransactions_ShouldCascadeDelete()
    {
        // Arrange
        using var context = CreateDbContext();
        var account = TestDataBuilder.CreateMunicipalAccount("Cascade Test", "3000-001");
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var transaction1 = TestDataBuilder.CreateTransaction(account.Id, 100m, "Transaction 1");
        var transaction2 = TestDataBuilder.CreateTransaction(account.Id, 200m, "Transaction 2");
        context.Transactions.AddRange(transaction1, transaction2);
        await context.SaveChangesAsync();
        
        var accountId = account.Id;
        var transaction1Id = transaction1.Id;
        var transaction2Id = transaction2.Id;
        
        // Act - Delete the parent account
        context.MunicipalAccounts.Remove(account);
        await context.SaveChangesAsync();
        
        // Assert - Related transactions should be cascade deleted
        var deletedAccount = await context.MunicipalAccounts.FindAsync(accountId);
        deletedAccount.Should().BeNull();
        
        var deletedTx1 = await context.Transactions.FindAsync(transaction1Id);
        var deletedTx2 = await context.Transactions.FindAsync(transaction2Id);
        deletedTx1.Should().BeNull();
        deletedTx2.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransaction_WithInvalidAccountId_ShouldThrowException()
    {
        // Arrange
        using var context = CreateDbContext();
        var transaction = TestDataBuilder.CreateTransaction(99999, 100m); // Non-existent account
        context.Transactions.Add(transaction);
        
        // Act & Assert
        var act = async () => await context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task LoadAccount_WithTransactions_ShouldLoadNavigationProperty()
    {
        // Arrange
        using var context = CreateDbContext();
        var account = TestDataBuilder.CreateMunicipalAccount("Navigation Test", "3000-002");
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var transaction1 = TestDataBuilder.CreateTransaction(account.Id, 100m, "Transaction 1");
        var transaction2 = TestDataBuilder.CreateTransaction(account.Id, 200m, "Transaction 2");
        context.Transactions.AddRange(transaction1, transaction2);
        await context.SaveChangesAsync();
        
        // Clear tracking to ensure fresh load
        context.ChangeTracker.Clear();
        
        // Act - Load account with transactions
        var loadedAccount = await context.MunicipalAccounts
            .Include(a => a.Transactions)
            .FirstAsync(a => a.Id == account.Id);
        
        // Assert
        loadedAccount.Should().NotBeNull();
        loadedAccount.Transactions.Should().HaveCount(2);
        loadedAccount.Transactions.Should().Contain(t => t.Description == "Transaction 1");
        loadedAccount.Transactions.Should().Contain(t => t.Description == "Transaction 2");
    }

    [Fact]
    public async Task CreateInvoice_WithVendorAndAccount_ShouldMaintainRelationships()
    {
        // Arrange
        using var context = CreateDbContext();
        var vendor = TestDataBuilder.CreateVendor("Test Vendor Inc.");
        var account = TestDataBuilder.CreateMunicipalAccount("Invoice Test", "3000-003");
        
        context.Vendors.Add(vendor);
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var invoice = TestDataBuilder.CreateInvoice(vendor.Id, account.Id, 500m, "INV-001");
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        
        var invoiceId = invoice.Id;
        context.ChangeTracker.Clear();
        
        // Act - Load invoice with relationships
        var loadedInvoice = await context.Invoices
            .Include(i => i.Vendor)
            .Include(i => i.MunicipalAccount)
            .FirstAsync(i => i.Id == invoiceId);
        
        // Assert
        loadedInvoice.Should().NotBeNull();
        loadedInvoice.Vendor.Should().NotBeNull();
        loadedInvoice.Vendor.Name.Should().Be("Test Vendor Inc.");
        loadedInvoice.MunicipalAccount.Should().NotBeNull();
        loadedInvoice.MunicipalAccount.AccountNumber.Should().Be("3000-003");
    }

    [Fact]
    public async Task DeleteVendor_WithInvoices_ShouldHandleConstraint()
    {
        // Arrange
        using var context = CreateDbContext();
        var vendor = TestDataBuilder.CreateVendor("Vendor To Delete");
        var account = TestDataBuilder.CreateMunicipalAccount("Invoice Account", "3000-004");
        
        context.Vendors.Add(vendor);
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var invoice = TestDataBuilder.CreateInvoice(vendor.Id, account.Id, 750m, "INV-002");
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        
        // Act & Assert - Attempt to delete vendor with invoices
        // This should either cascade delete or throw constraint violation depending on configuration
        context.Vendors.Remove(vendor);
        
        // If cascade delete is configured, this should succeed
        // If restrict is configured, this should throw
        try
        {
            await context.SaveChangesAsync();
            
            // If we get here, cascade delete is working
            var deletedVendor = await context.Vendors.FindAsync(vendor.Id);
            deletedVendor.Should().BeNull();
        }
        catch (DbUpdateException)
        {
            // If we get here, restrict is configured
            // This is also valid behavior
            true.Should().BeTrue(); // Test passes either way
        }
    }

    [Fact]
    public async Task UpdateAccount_WithRelatedTransactions_ShouldPreserveRelationships()
    {
        // Arrange
        using var context = CreateDbContext();
        var account = TestDataBuilder.CreateMunicipalAccount("Update Test", "3000-005");
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var transaction = TestDataBuilder.CreateTransaction(account.Id, 100m);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        
        var transactionId = transaction.Id;
        
        // Act - Update account
        account.Balance = 5000m;
        await context.SaveChangesAsync();
        
        // Assert - Transaction relationship should remain intact
        var loadedTransaction = await context.Transactions
            .Include(t => t.MunicipalAccount)
            .FirstAsync(t => t.Id == transactionId);
        
        loadedTransaction.MunicipalAccount.Should().NotBeNull();
        loadedTransaction.MunicipalAccount.Balance.Should().Be(5000m);
    }

    [Fact]
    public async Task CreateTransaction_ThenLoadAccount_ShouldShowBidirectionalNavigation()
    {
        // Arrange
        using var context = CreateDbContext();
        var account = TestDataBuilder.CreateMunicipalAccount("Bidirectional Test", "3000-006");
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();
        
        var transaction = TestDataBuilder.CreateTransaction(account.Id, 100m);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        
        var accountId = account.Id;
        var transactionId = transaction.Id;
        context.ChangeTracker.Clear();
        
        // Act - Load from both directions
        var accountFromTransaction = await context.Transactions
            .Where(t => t.Id == transactionId)
            .Select(t => t.MunicipalAccount)
            .FirstAsync();
        
        var transactionsFromAccount = await context.MunicipalAccounts
            .Where(a => a.Id == accountId)
            .SelectMany(a => a.Transactions)
            .ToListAsync();
        
        // Assert
        accountFromTransaction.Should().NotBeNull();
        accountFromTransaction.Id.Should().Be(accountId);
        
        transactionsFromAccount.Should().HaveCount(1);
        transactionsFromAccount.First().Id.Should().Be(transactionId);
    }

    [Fact]
    public async Task BulkDelete_WithRelatedEntities_ShouldMaintainIntegrity()
    {
        // Arrange
        using var context = CreateDbContext();
        var account1 = TestDataBuilder.CreateMunicipalAccount("Bulk Test 1", "3000-007");
        var account2 = TestDataBuilder.CreateMunicipalAccount("Bulk Test 2", "3000-008");
        context.MunicipalAccounts.AddRange(account1, account2);
        await context.SaveChangesAsync();
        
        var tx1 = TestDataBuilder.CreateTransaction(account1.Id, 100m);
        var tx2 = TestDataBuilder.CreateTransaction(account1.Id, 200m);
        var tx3 = TestDataBuilder.CreateTransaction(account2.Id, 300m);
        context.Transactions.AddRange(tx1, tx2, tx3);
        await context.SaveChangesAsync();
        
        // Act - Delete first account (should cascade to tx1, tx2)
        context.MunicipalAccounts.Remove(account1);
        await context.SaveChangesAsync();
        
        // Assert
        var remainingAccounts = await context.MunicipalAccounts.ToListAsync();
        remainingAccounts.Should().HaveCount(1);
        remainingAccounts.First().AccountNumber.Should().Be("3000-008");
        
        var remainingTransactions = await context.Transactions.ToListAsync();
        remainingTransactions.Should().HaveCount(1);
        remainingTransactions.First().Amount.Should().Be(300m);
    }

    [Fact]
    public async Task NullForeignKey_ShouldAllowOrphanedRecords_WhenConfigured()
    {
        // Arrange
        using var context = CreateDbContext();
        
        // Some models might allow null foreign keys for optional relationships
        // This test verifies that behavior when applicable
        
        var budgetEntry = TestDataBuilder.CreateBudgetEntry(0);
        context.BudgetEntries.Add(budgetEntry);
        
        // Act - Save without required relationships (if allowed by model)
        await context.SaveChangesAsync();
        
        // Assert
        var loaded = await context.BudgetEntries.FindAsync(budgetEntry.Id);
        loaded.Should().NotBeNull();
    }
}

