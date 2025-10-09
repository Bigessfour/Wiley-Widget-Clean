using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Data.Resilience;
using WileyWidget.Models;
using WileyWidget.Models.DTOs;
using Xunit;
using FluentAssertions;

namespace WileyWidget.Tests.Data;

/// <summary>
/// Integration tests for enhanced repository features:
/// - Unit of Work pattern
/// - Retry policies (Polly)
/// - Audit trails (IAuditable)
/// - Soft deletes (ISoftDeletable)
/// - Query projections (DTOs)
/// - Domain behavior methods
/// - Eager loading and performance optimizations
/// </summary>
public class EnhancedRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly EnterpriseRepository _repository;

    public EnhancedRepositoryTests()
    {
        var connectionString = $"Data Source=:memory:;Cache=Shared;Mode=Memory";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _context = new AppDbContext(options);
        _contextFactory = new TestDbContextFactory(options);
        _repository = new EnterpriseRepository(_contextFactory);
        
        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    #region Audit Trail Tests

    [Fact]
    public async Task Enterprise_ImplementsAuditable_AutoPopulatesFields()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act
        await _repository.AddAsync(enterprise);

        // Assert
        enterprise.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        enterprise.CreatedBy.Should().NotBeNullOrEmpty();
        enterprise.ModifiedDate.Should().BeNull();
        Assert.NotNull(enterprise.ModifiedBy);
        Assert.False(string.IsNullOrEmpty(enterprise.ModifiedBy));
    }

    [Fact]
    public async Task Enterprise_Update_PopulatesModifiedFields()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(enterprise);

        // Act
        enterprise.CurrentRate = 15.00m;
        await _repository.UpdateAsync(enterprise);

        // Assert
        enterprise.ModifiedDate.Should().NotBeNull();
        enterprise.ModifiedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        enterprise.ModifiedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SoftDelete_SetsIsDeletedFlag_KeepsEntityInDatabase()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise SoftDelete",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        var added = await _repository.AddAsync(enterprise);
        var id = added.Id;

        // Act
        var result = await _repository.SoftDeleteAsync(id);

        // Assert
        result.Should().BeTrue();
        
        // Verify entity still exists but is marked deleted
        using var verifyContext = _contextFactory.CreateDbContext();
        var deleted = await verifyContext.Enterprises
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id);
        
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
        deleted.DeletedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        deleted.DeletedBy.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SoftDeletedEnterprise_NotReturnedByDefaultQueries()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise Hidden",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        var added = await _repository.AddAsync(enterprise);
        await _repository.SoftDeleteAsync(added.Id);

        // Act
        var all = await _repository.GetAllAsync();

        // Assert - soft deleted entity should not appear in default query
        all.Should().NotContain(e => e.Id == added.Id);
        all.Should().NotContain(e => e.Name == "Test Enterprise Hidden");
    }

    [Fact]
    public async Task GetAllIncludingDeletedAsync_ReturnsSoftDeletedEntities()
    {
        // Arrange
        var activeEnterprise = new Enterprise
        {
            Name = "Active Enterprise",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        var deletedEnterprise = new Enterprise
        {
            Name = "Deleted Enterprise",
            CurrentRate = 8.00m,
            MonthlyExpenses = 800.00m,
            CitizenCount = 80
        };
        
        var active = await _repository.AddAsync(activeEnterprise);
        var deleted = await _repository.AddAsync(deletedEnterprise);
        await _repository.SoftDeleteAsync(deleted.Id);

        // Act
        var all = await _repository.GetAllIncludingDeletedAsync();

        // Assert
        all.Should().Contain(e => e.Id == active.Id);
        all.Should().Contain(e => e.Id == deleted.Id);
        all.First(e => e.Id == deleted.Id).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_UndoesSoftDelete()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise Restore",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        var added = await _repository.AddAsync(enterprise);
        await _repository.SoftDeleteAsync(added.Id);

        // Act
        var restoreResult = await _repository.RestoreAsync(added.Id);

        // Assert
        restoreResult.Should().BeTrue();
        
        var restored = await _repository.GetByIdAsync(added.Id);
        restored.Should().NotBeNull();
        restored!.IsDeleted.Should().BeFalse();
        restored.DeletedDate.Should().BeNull();
        restored.DeletedBy.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAsync_NonExistentEntity_ReturnsFalse()
    {
        // Act
        var result = await _repository.RestoreAsync(99999);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Query Projection Tests

    [Fact]
    public async Task GetSummariesAsync_ReturnsLightweightDTOs()
    {
        // Arrange
        var enterprise1 = new Enterprise
        {
            Name = "Water Summary",
            CurrentRate = 10.00m,
            MonthlyExpenses = 800.00m,
            CitizenCount = 100
        };
        var enterprise2 = new Enterprise
        {
            Name = "Sewer Summary",
            CurrentRate = 8.00m,
            MonthlyExpenses = 700.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(enterprise1);
        await _repository.AddAsync(enterprise2);

        // Act
        var summaries = await _repository.GetSummariesAsync();

        // Assert
        summaries.Should().HaveCountGreaterOrEqualTo(2);
        summaries.Should().AllBeOfType<EnterpriseSummary>();
        
        var waterSummary = summaries.FirstOrDefault(s => s.Name == "Water Summary");
        waterSummary.Should().NotBeNull();
        waterSummary!.MonthlyRevenue.Should().Be(1000.00m); // 100 * 10
        waterSummary.MonthlyBalance.Should().Be(200.00m); // 1000 - 800
        waterSummary.Status.Should().Be("Surplus");
    }

    [Fact]
    public async Task GetActiveSummariesAsync_ExcludesSoftDeleted()
    {
        // Arrange
        var active = new Enterprise
        {
            Name = "Active Summary",
            CurrentRate = 10.00m,
            MonthlyExpenses = 800.00m,
            CitizenCount = 100
        };
        var toDelete = new Enterprise
        {
            Name = "Deleted Summary",
            CurrentRate = 8.00m,
            MonthlyExpenses = 700.00m,
            CitizenCount = 100
        };
        var activeAdded = await _repository.AddAsync(active);
        var deletedAdded = await _repository.AddAsync(toDelete);
        await _repository.SoftDeleteAsync(deletedAdded.Id);

        // Act
        var summaries = await _repository.GetActiveSummariesAsync();

        // Assert
        summaries.Should().Contain(s => s.Name == "Active Summary");
        summaries.Should().NotContain(s => s.Name == "Deleted Summary");
    }

    [Fact]
    public async Task GetSummariesAsync_CalculatesStatusCorrectly()
    {
        // Arrange - Deficit enterprise
        var deficit = new Enterprise
        {
            Name = "Deficit Enterprise",
            CurrentRate = 5.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(deficit);

        // Act
        var summaries = await _repository.GetSummariesAsync();

        // Assert
        var deficitSummary = summaries.FirstOrDefault(s => s.Name == "Deficit Enterprise");
        deficitSummary.Should().NotBeNull();
        deficitSummary!.MonthlyBalance.Should().Be(-500.00m); // 500 - 1000
        deficitSummary.Status.Should().Be("Deficit");
    }

    #endregion

    #region Domain Behavior Tests

    [Fact]
    public async Task DomainBehavior_IsProfitable_ReturnsCorrectValue()
    {
        // Arrange
        var profitableEnterprise = new Enterprise
        {
            Name = "Profitable",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };
        var unprofitableEnterprise = new Enterprise
        {
            Name = "Unprofitable",
            CurrentRate = 5.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act & Assert
        profitableEnterprise.IsProfitable().Should().BeTrue();
        unprofitableEnterprise.IsProfitable().Should().BeFalse();
    }

    [Fact]
    public void DomainBehavior_CalculateRateAdjustmentForTarget_ReturnsCorrectAdjustment()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act - target $200 surplus
        var adjustment = enterprise.CalculateRateAdjustmentForTarget(200m);

        // Assert
        adjustment.Should().Be(2.00m); // Need to go from $10 to $12 per citizen
    }

    [Fact]
    public void DomainBehavior_ValidateRateChange_RejectsNegativeRates()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test",
            CurrentRate = 10.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act
        var isValid = enterprise.ValidateRateChange(-5.00m, out var errorMessage);

        // Assert
        isValid.Should().BeFalse();
        errorMessage.Should().Contain("cannot be negative");
    }

    [Fact]
    public void DomainBehavior_GetRateRecommendation_ProvidesGuidance()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test",
            CurrentRate = 5.00m, // Below break-even of $10
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act
        var recommendation = enterprise.GetRateRecommendation();

        // Assert
        recommendation.Should().Contain("below break-even");
        recommendation.Should().Contain("adjustment required");
    }

    [Fact]
    public void DomainBehavior_ProjectAnnualRevenue_CalculatesCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };

        // Act
        var annualRevenue = enterprise.ProjectAnnualRevenue();

        // Assert
        annualRevenue.Should().Be(12000.00m); // 1000 * 12
    }

    [Fact]
    public void DomainBehavior_CalculateBreakEvenVariance_ReturnsCorrectValue()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test",
            CurrentRate = 12.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 100
        };

        // Act
        var variance = enterprise.CalculateBreakEvenVariance();

        // Assert
        variance.Should().Be(2.00m); // 12 - 10 (break-even)
    }

    [Fact]
    public void DomainBehavior_UpdateMeterReading_ValidatesCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100,
            MeterReading = 1000m,
            MeterReadDate = DateTime.UtcNow.AddDays(-30)
        };

        // Act
        var result = enterprise.UpdateMeterReading(1100m, DateTime.UtcNow, out var errorMessage);

        // Assert
        result.Should().BeTrue();
        errorMessage.Should().BeNull();
        enterprise.MeterReading.Should().Be(1100m);
        enterprise.PreviousMeterReading.Should().Be(1000m);
        enterprise.WaterConsumption.Should().Be(100m);
    }

    [Fact]
    public void DomainBehavior_UpdateMeterReading_RejectsDecrease()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100,
            MeterReading = 1000m,
            MeterReadDate = DateTime.UtcNow.AddDays(-30)
        };

        // Act
        var result = enterprise.UpdateMeterReading(900m, DateTime.UtcNow, out var errorMessage);

        // Assert
        result.Should().BeFalse();
        errorMessage.Should().Contain("cannot be less than previous");
    }

    #endregion

    #region Unit of Work Tests

    [Fact]
    public async Task UnitOfWork_ExecuteInTransaction_CommitsOnSuccess()
    {
        // Arrange
        using var uow = new UnitOfWork(_context);
        var enterprise1 = new Enterprise
        {
            Name = "Enterprise 1",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };
        var enterprise2 = new Enterprise
        {
            Name = "Enterprise 2",
            CurrentRate = 8.00m,
            MonthlyExpenses = 400.00m,
            CitizenCount = 100
        };

        // Act
        await uow.ExecuteInTransactionAsync(async () =>
        {
            await uow.Enterprises.AddAsync(enterprise1);
            await uow.Enterprises.AddAsync(enterprise2);
        });

        // Assert
        var count = await uow.Enterprises.GetCountAsync();
        count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task UnitOfWork_ExecuteInTransaction_RollsBackOnFailure()
    {
        // Arrange
        using var uow = new UnitOfWork(_context);
        var initialCount = await uow.Enterprises.GetCountAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await uow.ExecuteInTransactionAsync(async () =>
            {
                var enterprise = new Enterprise
                {
                    Name = "Test",
                    CurrentRate = 10.00m,
                    MonthlyExpenses = 500.00m,
                    CitizenCount = 100
                };
                await uow.Enterprises.AddAsync(enterprise);
                
                // Simulate error
                throw new InvalidOperationException("Test error");
            });
        });

        // Verify rollback
        var finalCount = await uow.Enterprises.GetCountAsync();
        finalCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task DatabaseResiliencePolicy_HandlesTransientErrors()
    {
        // This is a conceptual test - in reality, you'd mock transient failures
        // For now, we verify the policy executes successfully
        
        // Act
        var result = await DatabaseResiliencePolicy.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return "Success";
        });

        // Assert
        result.Should().Be("Success");
    }

    [Fact]
    public async Task DatabaseResiliencePolicy_RetriesOnTransientFailure()
    {
        // Arrange
        var attemptCount = 0;

        // Act & Assert
        var result = await DatabaseResiliencePolicy.ExecuteAsync(async () =>
        {
            attemptCount++;
            await Task.Delay(10);
            
            // Succeed on second attempt
            if (attemptCount == 1)
                throw new TimeoutException("Simulated timeout");
            
            return "Success after retry";
        });

        // Assert
        result.Should().Be("Success after retry");
        attemptCount.Should().Be(2); // Failed once, succeeded on retry
    }

    #endregion

    #region Repository Basic Operations Tests

    [Fact]
    public async Task GetByNameAsync_FindsExistingEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Unique Name Enterprise",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(enterprise);

        // Act
        var found = await _repository.GetByNameAsync("Unique Name Enterprise");

        // Assert
        found.Should().NotBeNull();
        found!.Name.Should().Be("Unique Name Enterprise");
    }

    [Fact]
    public async Task ExistsByNameAsync_ReturnsTrueForExisting()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Exists Test",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(enterprise);

        // Act
        var exists = await _repository.ExistsByNameAsync("Exists Test");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var initialCount = await _repository.GetCountAsync();
        
        await _repository.AddAsync(new Enterprise
        {
            Name = "Count Test 1",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        });
        await _repository.AddAsync(new Enterprise
        {
            Name = "Count Test 2",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        });

        // Act
        var finalCount = await _repository.GetCountAsync();

        // Assert
        (finalCount - initialCount).Should().Be(2);
    }

    [Fact]
    public async Task GetWithInteractionsAsync_IncludesRelatedData()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Enterprise With Interactions",
            CurrentRate = 10.00m,
            MonthlyExpenses = 500.00m,
            CitizenCount = 100
        };
        await _repository.AddAsync(enterprise);

        // Act
        var withInteractions = await _repository.GetWithInteractionsAsync();

        // Assert
        withInteractions.Should().NotBeEmpty();
        // Note: BudgetInteractions might be empty, but the query should work
    }

    [Fact]
    public void CreateFromHeaderMapping_MapsPropertiesCorrectly()
    {
        // Arrange
        var headerMap = new Dictionary<string, string>
        {
            { "Name", "Mapped Enterprise" },
            { "Current Rate", "15.50" },
            { "Monthly Expenses", "1200.00" },
            { "Citizen Count", "150" }
        };

        // Act
        var enterprise = _repository.CreateFromHeaderMapping(headerMap);

        // Assert
        enterprise.Name.Should().Be("Mapped Enterprise");
        enterprise.CurrentRate.Should().Be(15.50m);
        enterprise.MonthlyExpenses.Should().Be(1200.00m);
        enterprise.CitizenCount.Should().Be(150);
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }

    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext()
        {
            return new AppDbContext(_options);
        }
    }
}
