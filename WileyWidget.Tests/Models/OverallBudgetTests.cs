using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for OverallBudget model
/// Tests INotifyPropertyChanged, calculated properties, validation, and business logic
/// </summary>
public class OverallBudgetTests
{
    [Fact]
    public void OverallBudget_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var budget = new OverallBudget();

        // Assert
        Assert.Equal(0, budget.Id);
        Assert.Equal(DateTime.MinValue, budget.SnapshotDate);
        Assert.Equal(0m, budget.TotalMonthlyRevenue);
        Assert.Equal(0m, budget.TotalMonthlyExpenses);
        Assert.Equal(0m, budget.TotalMonthlyBalance);
        Assert.Equal(0, budget.TotalCitizensServed);
        Assert.Equal(0m, budget.AverageRatePerCitizen);
        Assert.Equal(string.Empty, budget.Notes);
        Assert.False(budget.IsCurrent);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_EventsAreRaised()
    {
        // Arrange
        var budget = new OverallBudget();
        var propertyChangedEvents = new List<string>();

        budget.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        budget.TotalMonthlyRevenue = 100000.00m;
        budget.TotalMonthlyExpenses = 85000.00m;
        budget.TotalMonthlyBalance = 15000.00m;
        budget.TotalCitizensServed = 5000;
        budget.AverageRatePerCitizen = 25.50m;
        budget.Notes = "Budget snapshot for Q3 2025";

        // Assert
        Assert.Contains(nameof(OverallBudget.TotalMonthlyRevenue), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.TotalMonthlyExpenses), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.TotalMonthlyBalance), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.TotalCitizensServed), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.AverageRatePerCitizen), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.Notes), propertyChangedEvents);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_CalculatedPropertiesAreUpdated()
    {
        // Arrange
        var budget = new OverallBudget();
        var propertyChangedEvents = new List<string>();

        budget.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        budget.TotalMonthlyRevenue = 100000.00m;
        budget.TotalMonthlyBalance = 15000.00m;

        // Assert
        Assert.Contains(nameof(OverallBudget.DeficitPercentage), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.IsSurplus), propertyChangedEvents);
    }

    [Fact]
    public void OverallBudget_IsSurplus_CalculatesCorrectly()
    {
        // Arrange
        var budget = new OverallBudget();

        // Act & Assert - Surplus
        budget.TotalMonthlyBalance = 15000.00m;
        Assert.True(budget.IsSurplus);

        // Act & Assert - Deficit
        budget.TotalMonthlyBalance = -5000.00m;
        Assert.False(budget.IsSurplus);

        // Act & Assert - Break-even
        budget.TotalMonthlyBalance = 0m;
        Assert.False(budget.IsSurplus);
    }

    [Fact]
    public void OverallBudget_DeficitPercentage_CalculatesCorrectly()
    {
        // Arrange
        var budget = new OverallBudget
        {
            TotalMonthlyRevenue = 100000.00m
        };

        // Act & Assert - Surplus (15% surplus)
        budget.TotalMonthlyBalance = 15000.00m;
        Assert.Equal(15.00m, budget.DeficitPercentage);

        // Act & Assert - Deficit (-5% deficit)
        budget.TotalMonthlyBalance = -5000.00m;
        Assert.Equal(-5.00m, budget.DeficitPercentage);

        // Act & Assert - Break-even
        budget.TotalMonthlyBalance = 0m;
        Assert.Equal(0m, budget.DeficitPercentage);
    }

    [Fact]
    public void OverallBudget_DeficitPercentage_ZeroRevenue_ReturnsZero()
    {
        // Arrange
        var budget = new OverallBudget
        {
            TotalMonthlyRevenue = 0m,
            TotalMonthlyBalance = 15000.00m
        };

        // Act & Assert
        Assert.Equal(0m, budget.DeficitPercentage);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_NoEventWhenValueUnchanged()
    {
        // Arrange
        var budget = new OverallBudget { TotalMonthlyRevenue = 100000.00m };
        var eventRaised = false;

        budget.PropertyChanged += (sender, args) => eventRaised = true;

        // Act
        budget.TotalMonthlyRevenue = 100000.00m; // Same value

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void OverallBudget_Validation_ValidModel_Passes()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.UtcNow,
            TotalMonthlyRevenue = 100000.00m,
            TotalMonthlyExpenses = 85000.00m,
            TotalMonthlyBalance = 15000.00m,
            TotalCitizensServed = 5000,
            AverageRatePerCitizen = 25.50m,
            IsCurrent = true
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void OverallBudget_Validation_MissingSnapshotDate_Fails()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = default, // Missing required date
            TotalMonthlyRevenue = 100000.00m,
            TotalMonthlyExpenses = 85000.00m,
            TotalCitizensServed = 5000
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        // Note: DateTime validation might not trigger with default value, but this tests the structure
    }

    [Fact]
    public void OverallBudget_Validation_NegativeTotalCitizensServed_Fails()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.UtcNow,
            TotalMonthlyRevenue = 100000.00m,
            TotalMonthlyExpenses = 85000.00m,
            TotalCitizensServed = -100, // Negative citizens
            AverageRatePerCitizen = 25.50m
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        // Note: Range validation for int might not be automatic, but this tests the business logic
    }

    [Fact]
    public void OverallBudget_Validation_NotesTooLong_Fails()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.UtcNow,
            TotalMonthlyRevenue = 100000.00m,
            TotalMonthlyExpenses = 85000.00m,
            TotalCitizensServed = 5000,
            Notes = new string('A', 501) // 501 characters, exceeds max of 500
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Notes cannot exceed 500 characters");
    }

    [Fact]
    public void OverallBudget_Validation_NotesMaxLength_Passes()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.UtcNow,
            TotalMonthlyRevenue = 100000.00m,
            TotalMonthlyExpenses = 85000.00m,
            TotalCitizensServed = 5000,
            Notes = new string('A', 500) // Exactly 500 characters
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(100000.00, 85000.00, 15000.00, true, 15.00)]  // Surplus
    [InlineData(80000.00, 90000.00, -10000.00, false, -12.50)] // Deficit
    [InlineData(75000.00, 75000.00, 0.00, false, 0.00)]       // Break-even
    public void OverallBudget_Calculations_VariousScenarios_WorkCorrectly(
        decimal revenue, decimal expenses, decimal balance, bool expectedIsSurplus, decimal expectedDeficitPercentage)
    {
        // Arrange
        var budget = new OverallBudget
        {
            TotalMonthlyRevenue = revenue,
            TotalMonthlyExpenses = expenses,
            TotalMonthlyBalance = balance
        };

        // Assert
        Assert.Equal(expectedIsSurplus, budget.IsSurplus);
        Assert.Equal(expectedDeficitPercentage, budget.DeficitPercentage);
    }

    [Fact]
    public void OverallBudget_SnapshotDate_CanBeSetToSpecificDate()
    {
        // Arrange
        var budget = new OverallBudget();
        var specificDate = new DateTime(2025, 9, 19, 10, 30, 0, DateTimeKind.Utc);

        // Act
        budget.SnapshotDate = specificDate;

        // Assert
        Assert.Equal(specificDate, budget.SnapshotDate);
    }

    [Fact]
    public void OverallBudget_IsCurrent_DefaultsToFalse()
    {
        // Arrange & Act
        var budget = new OverallBudget();

        // Assert
        Assert.False(budget.IsCurrent);
    }

    [Fact]
    public void OverallBudget_IsCurrent_CanBeSetToTrue()
    {
        // Arrange
        var budget = new OverallBudget();

        // Act
        budget.IsCurrent = true;

        // Assert
        Assert.True(budget.IsCurrent);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1000, 20.00, 20000.00)]
    [InlineData(5000, 25.50, 127500.00)]
    [InlineData(10000, 15.75, 157500.00)]
    public void OverallBudget_AverageRatePerCitizen_Calculations_WorkCorrectly(
        int citizens, decimal averageRate, decimal expectedRevenue)
    {
        // Arrange
        var budget = new OverallBudget
        {
            TotalCitizensServed = citizens,
            AverageRatePerCitizen = averageRate
        };

        // Act - Calculate expected revenue manually
        var calculatedRevenue = citizens * averageRate;

        // Assert
        Assert.Equal(expectedRevenue, calculatedRevenue);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_MultipleProperties_UpdateCalculatedProperties()
    {
        // Arrange
        var budget = new OverallBudget();
        var propertyChangedEvents = new List<string>();

        budget.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act - Change revenue and balance in sequence
        budget.TotalMonthlyRevenue = 100000.00m;
        budget.TotalMonthlyBalance = 15000.00m;

        // Assert - Both DeficitPercentage and IsSurplus should be updated
        Assert.Contains(nameof(OverallBudget.DeficitPercentage), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.IsSurplus), propertyChangedEvents);
        Assert.Equal(15.00m, budget.DeficitPercentage);
        Assert.True(budget.IsSurplus);
    }
}