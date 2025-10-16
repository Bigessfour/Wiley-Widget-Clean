using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly Mock<ILogger<EnterpriseRepository>> _mockLogger;
    private readonly EnterpriseRepository _repository;

    public EnterpriseRepositoryTests()
    {
        // Use SQLite in-memory database for testing (Microsoft recommended approach)
        // Provides better SQL compatibility than EF Core In-Memory provider
        var databaseName = $"EnterpriseTest_{Guid.NewGuid()}";
        _contextFactory = TestDbContextFactory.CreateSqliteInMemory(databaseName);
        _context = _contextFactory.CreateDbContext();
        _mockLogger = new Mock<ILogger<EnterpriseRepository>>();
        _repository = new EnterpriseRepository(_contextFactory, _mockLogger.Object);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<EnterpriseRepository>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EnterpriseRepository(null!, mockLogger.Object));
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Arrange - Clear seeded data
        using var context = _contextFactory.CreateDbContext();
        context.Database.ExecuteSqlRaw("DELETE FROM Enterprises");

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
        var enterprise1 = new Enterprise { Name = "Zeta Corp", Description = "Test enterprise Z", RowVersion = new byte[8] };
        var enterprise2 = new Enterprise { Name = "Alpha Corp", Description = "Test enterprise A", RowVersion = new byte[8] };
        var enterprise3 = new Enterprise { Name = "Beta Corp", Description = "Test enterprise B", RowVersion = new byte[8] };

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
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise", RowVersion = new byte[8] };
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
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise", RowVersion = new byte[8] };
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
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise", RowVersion = new byte[8] };
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
            BudgetAmount = 100000.00m,
            RowVersion = new byte[8]
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
        var enterprise = new Enterprise { Name = "Original Corp", Description = "Original", RowVersion = new byte[8] };
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
        var enterprise = new Enterprise { Name = "Delete Corp", Description = "To be deleted", RowVersion = new byte[8] };
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
        var enterprise = new Enterprise { Name = "Existing Corp", Description = "Existing", RowVersion = new byte[8] };
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
        var enterprise = new Enterprise { Name = "Existing Corp", Description = "Existing", RowVersion = new byte[8] };
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
        var enterprise1 = new Enterprise { Name = "Unique Corp 1", Description = "First", RowVersion = new byte[8] };
        var enterprise2 = new Enterprise { Name = "Unique Corp 2", Description = "Second", RowVersion = new byte[8] };
        _context.Enterprises.AddRange(enterprise1, enterprise2);
        await _context.SaveChangesAsync();

        // Act - Should return false because enterprise1 is excluded and enterprise2 has different name
        var result = await _repository.ExistsByNameAsync("unique corp 1", enterprise1.Id);

        // Assert
        Assert.False(result);
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
        // Arrange - Clear seeded data
        using var context = _contextFactory.CreateDbContext();
        context.Database.ExecuteSqlRaw("DELETE FROM Enterprises");

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
            new Enterprise { Name = "Corp 1", Description = "Test 1", RowVersion = new byte[8] },
            new Enterprise { Name = "Corp 2", Description = "Test 2", RowVersion = new byte[8] },
            new Enterprise { Name = "Corp 3", Description = "Test 3", RowVersion = new byte[8] }
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
        // Arrange - Clear seeded data
        using var context = _contextFactory.CreateDbContext();
        context.Database.ExecuteSqlRaw("DELETE FROM Enterprises");

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
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test enterprise", RowVersion = new byte[8] };
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
        var enterprise1 = new Enterprise { Name = "Zeta Corp", Description = "Test Z", RowVersion = new byte[8] };
        var enterprise2 = new Enterprise { Name = "Alpha Corp", Description = "Test A", RowVersion = new byte[8] };

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

    [Fact]
    public void CreateFromHeaderMapping_WithValidHeaders_MapsPropertiesCorrectly()
    {
        // Arrange
        var headerValueMap = new Dictionary<string, string>
        {
            { "Name", "Test Enterprise" },
            { "Description", "A test enterprise" },
            { "CurrentRate", "10.50" },
            { "MonthlyExpenses", "2500.00" },
            { "CitizenCount", "300" },
            { "Type", "Utility" },
            { "Notes", "Test notes" }
        };

        // Act
        var result = _repository.CreateFromHeaderMapping(headerValueMap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Enterprise", result.Name);
        Assert.Equal("A test enterprise", result.Description);
        Assert.Equal(10.50m, result.CurrentRate);
        Assert.Equal(2500.00m, result.MonthlyExpenses);
        Assert.Equal(300, result.CitizenCount);
        Assert.Equal("Utility", result.Type);
        Assert.Equal("Test notes", result.Notes);
    }

    [Fact]
    public void CreateFromHeaderMapping_WithAlternativeHeaders_MapsPropertiesCorrectly()
    {
        // Arrange
        var headerValueMap = new Dictionary<string, string>
        {
            { "Enterprise Name", "Water Utility" },
            { "Rate", "5.25" },
            { "Monthly Expenses", "1500.75" },
            { "Citizen Count", "450" },
            { "Budget", "10000.00" }
        };

        // Act
        var result = _repository.CreateFromHeaderMapping(headerValueMap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Water Utility", result.Name);
        Assert.Equal(5.25m, result.CurrentRate);
        Assert.Equal(1500.75m, result.MonthlyExpenses);
        Assert.Equal(450, result.CitizenCount);
        Assert.Equal(10000.00m, result.TotalBudget);
    }

    [Fact]
    public void CreateFromHeaderMapping_WithInvalidValues_SkipsInvalidProperties()
    {
        // Arrange
        var headerValueMap = new Dictionary<string, string>
        {
            { "Name", "Valid Name" },
            { "CurrentRate", "invalid_rate" },
            { "CitizenCount", "not_a_number" },
            { "Type", "Valid Type" }
        };

        // Act
        var result = _repository.CreateFromHeaderMapping(headerValueMap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Valid Name", result.Name);
        Assert.Equal("Valid Type", result.Type);
        // Invalid values should not be set (remain default)
        Assert.Equal(0m, result.CurrentRate);
        Assert.Equal(0, result.CitizenCount);
    }

    [Fact]
    public void CreateFromHeaderMapping_WithNullMap_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.CreateFromHeaderMapping(null!));
    }

    [Fact]
    public void CreateFromHeaderMapping_WithEmptyValues_SkipsEmptyProperties()
    {
        // Arrange
        var headerValueMap = new Dictionary<string, string>
        {
            { "Name", "" },
            { "Description", "   " },
            { "CurrentRate", "15.00" }
        };

        // Act
        var result = _repository.CreateFromHeaderMapping(headerValueMap);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Name); // Empty string is still valid for string properties
        Assert.Null(result.Description); // Whitespace-only values are treated as null
        Assert.Equal(15.00m, result.CurrentRate);
    }

    [Fact(Skip = "SQLite in-memory doesn't support concurrency conflict simulation")]
    public async Task UpdateAsync_ConcurrencyConflict_ThrowsConcurrencyConflictException()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test Description", RowVersion = new byte[8] };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        // Simulate concurrent modification by changing the entity in the database
        var dbEntity = await _context.Enterprises.FirstAsync();
        dbEntity.Description = "Modified by another user";
        await _context.SaveChangesAsync();

        // Modify the original entity (detached from context)
        enterprise.Description = "Modified by current user";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _repository.UpdateAsync(enterprise));
        
        Assert.Contains("concurrency conflict", exception.Message.ToLowerInvariant());
        Assert.NotNull(exception.DatabaseValues);
        Assert.NotNull(exception.ClientValues);
    }

    [Fact(Skip = "SQLite in-memory doesn't support concurrency conflict simulation")]
    public async Task DeleteAsync_ConcurrencyConflict_ThrowsConcurrencyConflictException()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Corp", Description = "Test Description", RowVersion = new byte[8] };
        _context.Enterprises.Add(enterprise);
        await _context.SaveChangesAsync();

        var enterpriseId = enterprise.Id;

        // Simulate concurrent deletion by removing the entity from the database
        var dbEntity = await _context.Enterprises.FirstAsync();
        _context.Enterprises.Remove(dbEntity);
        await _context.SaveChangesAsync();

        // Try to delete the original entity (now stale)
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => _repository.DeleteAsync(enterpriseId));
        
        Assert.Contains("concurrency conflict", exception.Message.ToLowerInvariant());
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