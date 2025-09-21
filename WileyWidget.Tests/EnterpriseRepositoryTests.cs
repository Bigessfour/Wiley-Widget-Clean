using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Unit tests for EnterpriseRepository
/// Tests all repository methods using in-memory database
/// </summary>
public class EnterpriseRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly AppDbContext _context;
    private readonly EnterpriseRepository _repository;

    public EnterpriseRepositoryTests()
    {
        // Create in-memory database for testing
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _context = _contextFactory.CreateDbContext();
        _repository = new EnterpriseRepository(_contextFactory);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EnterpriseRepository(null));
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
    public async Task GetAllAsync_WithEnterprises_ReturnsOrderedByName()
    {
        // Arrange
        var enterprise1 = new Enterprise { Name = "Zeta Corp", Description = "Test enterprise Z" };
        var enterprise2 = new Enterprise { Name = "Alpha Corp", Description = "Test enterprise A" };
        var enterprise3 = new Enterprise { Name = "Beta Corp", Description = "Test enterprise B" };

        await _context.Enterprises.AddRangeAsync(enterprise1, enterprise2, enterprise3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count());
        var enterprises = result.ToList();
        Assert.Equal("Alpha Corp", enterprises[0].Name);
        Assert.Equal("Beta Corp", enterprises[1].Name);
        Assert.Equal("Zeta Corp", enterprises[2].Name);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(enterprise.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(enterprise.Id, result.Id);
        Assert.Equal("Test Corp", result.Name);
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
    public async Task GetByNameAsync_ExistingName_ReturnsEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByNameAsync("test corp");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(enterprise.Id, result.Id);
        Assert.Equal("Test Corp", result.Name);
    }

    [Fact]
    public async Task GetByNameAsync_CaseInsensitiveSearch_Works()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByNameAsync("TEST CORP");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Corp", result.Name);
    }

    [Fact]
    public async Task GetByNameAsync_NonExistingName_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByNameAsync("NonExisting Corp");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddAsync_ValidEnterprise_AddsAndReturnsEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "New Corp",
            Description = "New enterprise",
            BudgetAmount = 100000.00m
        };

        // Act
        var result = await _repository.AddAsync(enterprise);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Corp", result.Name);
        Assert.Equal(100000.00m, result.BudgetAmount);

        // Verify it was added to database
        var savedEnterprise = await _context.Enterprises.FindAsync(result.Id);
        Assert.NotNull(savedEnterprise);
        Assert.Equal("New Corp", savedEnterprise.Name);
    }

    [Fact]
    public async Task UpdateAsync_ExistingEnterprise_UpdatesAndReturnsEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Original Corp", Description = "Original" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        enterprise.Name = "Updated Corp";
        enterprise.Description = "Updated description";

        // Act
        var result = await _repository.UpdateAsync(enterprise);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Corp", result.Name);
        Assert.Equal("Updated description", result.Description);

        // Verify it was updated in database
        var updatedEnterprise = await _context.Enterprises.FindAsync(enterprise.Id);
        Assert.NotNull(updatedEnterprise);
        Assert.Equal("Updated Corp", updatedEnterprise.Name);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesAndReturnsTrue()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Delete Corp", Description = "To be deleted" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(enterprise.Id);

        // Assert
        Assert.True(result);

        // Verify it was deleted from database
        var deletedEnterprise = await _context.Enterprises.AsNoTracking().FirstOrDefaultAsync(e => e.Id == enterprise.Id);
        Assert.Null(deletedEnterprise);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByNameAsync_ExistingName_ReturnsTrue()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Existing Corp", Description = "Existing" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsByNameAsync("existing corp");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByNameAsync_CaseInsensitiveCheck_Works()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Existing Corp", Description = "Existing" };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsByNameAsync("EXISTING CORP");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByNameAsync_WithExcludeId_ExcludesSpecifiedEnterprise()
    {
        // Arrange
        var enterprise1 = new Enterprise { Name = "Test Corp", Description = "First" };
        var enterprise2 = new Enterprise { Name = "Test Corp", Description = "Second" };
        _context.Enterprises.AddRange(enterprise1, enterprise2);
        await _context.SaveChangesAsync();

        // Act - Should return true because enterprise2 exists with same name
        var result = await _repository.ExistsByNameAsync("test corp", enterprise1.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByNameAsync_NonExistingName_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByNameAsync("NonExisting Corp");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCountAsync_EmptyDatabase_ReturnsZero()
    {
        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetCountAsync_WithEnterprises_ReturnsCorrectCount()
    {
        // Arrange
        var enterprises = new List<Enterprise>
        {
            new Enterprise { Name = "Corp 1", Description = "Test 1" },
            new Enterprise { Name = "Corp 2", Description = "Test 2" },
            new Enterprise { Name = "Corp 3", Description = "Test 3" }
        };

        _context.Enterprises.AddRange(enterprises);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetWithInteractionsAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Act
        var result = await _repository.GetWithInteractionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetWithInteractionsAsync_WithEnterprises_IncludesBudgetInteractions()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise" };
        var budgetInteraction = new BudgetInteraction
        {
            Enterprise = enterprise,
            Amount = 50000.00m,
            Description = "Test interaction",
            InteractionDate = DateTime.Now
        };

        enterprise.BudgetInteractions = new List<BudgetInteraction> { budgetInteraction };

        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetWithInteractionsAsync();

        // Assert
        Assert.Single(result);
        var retrievedEnterprise = result.First();
        Assert.Equal("Test Corp", retrievedEnterprise.Name);
        Assert.NotNull(retrievedEnterprise.BudgetInteractions);
        Assert.Single(retrievedEnterprise.BudgetInteractions);
        Assert.Equal(50000.00m, retrievedEnterprise.BudgetInteractions.First().Amount);
    }

    [Fact]
    public async Task GetWithInteractionsAsync_OrdersByName()
    {
        // Arrange
        var enterprise1 = new Enterprise { Name = "Zeta Corp", Description = "Test Z" };
        var enterprise2 = new Enterprise { Name = "Alpha Corp", Description = "Test A" };

        _context.Enterprises.AddRange(enterprise1, enterprise2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetWithInteractionsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        var enterprises = result.ToList();
        Assert.Equal("Alpha Corp", enterprises[0].Name);
        Assert.Equal("Zeta Corp", enterprises[1].Name);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}