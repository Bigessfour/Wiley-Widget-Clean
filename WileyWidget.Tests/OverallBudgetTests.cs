using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the OverallBudget model validation, business logic, and property changes
/// </summary>
public class OverallBudgetTests
{
    [Fact]
    public void OverallBudget_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 50000.00m,
            TotalMonthlyExpenses = 45000.00m,
            TotalMonthlyBalance = 5000.00m,
            TotalCitizensServed = 25000,
            AverageRatePerCitizen = 2.00m,
            Notes = "Test budget snapshot",
            IsCurrent = true
        };

        // Assert
        Assert.Equal(50000.00m, budget.TotalMonthlyRevenue);
        Assert.Equal(45000.00m, budget.TotalMonthlyExpenses);
        Assert.Equal(5000.00m, budget.TotalMonthlyBalance);
        Assert.Equal(25000, budget.TotalCitizensServed);
        Assert.Equal(2.00m, budget.AverageRatePerCitizen);
        Assert.Equal("Test budget snapshot", budget.Notes);
        Assert.True(budget.IsCurrent);
        Assert.True(budget.IsSurplus);
        Assert.Equal(10.0m, budget.DeficitPercentage); // 5000 / 50000 * 100
        Assert.True(budget.SnapshotDate <= DateTime.Now);
    }

    [Fact]
    public void OverallBudget_WithDeficit_CalculatesCorrectly()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 40000.00m,
            TotalMonthlyExpenses = 45000.00m,
            TotalMonthlyBalance = -5000.00m,
            TotalCitizensServed = 20000,
            AverageRatePerCitizen = 2.00m,
            IsCurrent = true
        };

        // Assert
        Assert.False(budget.IsSurplus);
        Assert.Equal(-12.5m, budget.DeficitPercentage); // -5000 / 40000 * 100
    }

    [Fact]
    public void OverallBudget_WithZeroRevenue_HandlesDeficitPercentage()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 0.00m,
            TotalMonthlyExpenses = 10000.00m,
            TotalMonthlyBalance = -10000.00m,
            TotalCitizensServed = 10000,
            IsCurrent = true
        };

        // Assert
        Assert.False(budget.IsSurplus);
        Assert.Equal(0m, budget.DeficitPercentage); // Division by zero protection
    }

    [Theory]
    [InlineData(10000.00, 8000.00, 2000.00, true, 20.0)]   // Surplus
    [InlineData(10000.00, 10000.00, 0.00, false, 0.0)]    // Break-even
    [InlineData(10000.00, 12000.00, -2000.00, false, -20.0)] // Deficit
    public void OverallBudget_IsSurplus_And_DeficitPercentage_CalculatedCorrectly(
        decimal revenue, decimal expenses, decimal balance, bool expectedIsSurplus, decimal expectedDeficitPercentage)
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = revenue,
            TotalMonthlyExpenses = expenses,
            TotalMonthlyBalance = balance,
            TotalCitizensServed = 10000,
            IsCurrent = true
        };

        // Assert
        Assert.Equal(expectedIsSurplus, budget.IsSurplus);
        Assert.Equal(expectedDeficitPercentage, budget.DeficitPercentage);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_Events_Are_Raised()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 50000.00m,
            TotalMonthlyExpenses = 45000.00m,
            TotalMonthlyBalance = 5000.00m,
            TotalCitizensServed = 25000,
            AverageRatePerCitizen = 2.00m,
            IsCurrent = true
        };

        var propertyChangedEvents = new List<string>();

        budget.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                propertyChangedEvents.Add(args.PropertyName);
            }
        };

        // Act
        budget.TotalMonthlyRevenue = 55000.00m;
        budget.TotalMonthlyBalance = 10000.00m;
        budget.TotalCitizensServed = 26000;
        budget.AverageRatePerCitizen = 2.10m;
        budget.Notes = "Updated notes";

        // Assert
        Assert.Contains(nameof(OverallBudget.TotalMonthlyRevenue), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.DeficitPercentage), propertyChangedEvents); // Should be raised when revenue changes
        Assert.Contains(nameof(OverallBudget.TotalMonthlyBalance), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.IsSurplus), propertyChangedEvents); // Should be raised when balance changes
        Assert.Contains(nameof(OverallBudget.TotalCitizensServed), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.AverageRatePerCitizen), propertyChangedEvents);
        Assert.Contains(nameof(OverallBudget.Notes), propertyChangedEvents);
    }

    [Theory]
    [InlineData("", true)]                    // Empty notes (optional)
    [InlineData("Valid notes", true)]        // Valid notes
    [InlineData("A very long note that exceeds the maximum allowed length for notes in the budget snapshot and should fail validation when checked against the string length constraint. " + 
                "This is additional text to make the string longer than 500 characters. " +
                "We need to ensure that the validation properly catches strings that are too long. " +
                "The StringLength attribute should prevent notes from exceeding the maximum length. " +
                "This test verifies that the validation works correctly for the Notes property. " +
                "By making this string sufficiently long, we can confirm that the validation attribute is functioning as expected. " +
                "The OverallBudget model should reject any notes that are longer than 500 characters. " +
                "This helps maintain data integrity and prevents potential issues with storage or display of overly long notes. " +
                "The validation should trigger an error message when the length exceeds the specified limit.", false)] // Too long
    public void OverallBudget_Notes_Validation(string notes, bool shouldBeValid)
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 50000.00m,
            TotalMonthlyExpenses = 45000.00m,
            TotalCitizensServed = 25000,
            Notes = notes,
            IsCurrent = true
        };

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid);
            Assert.Empty(validationResults);
        }
        else
        {
            Assert.False(isValid);
            Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(OverallBudget.Notes)));
        }
    }

    [Fact]
    public void OverallBudget_RequiredFields_Validation()
    {
        // Arrange
        var budget = new OverallBudget(); // All properties will have default values

        // Act
        var validationContext = new ValidationContext(budget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(budget, validationContext, validationResults, true);

        // Also call IValidatableObject.Validate manually
        var customResults = ((IValidatableObject)budget).Validate(validationContext);
        validationResults.AddRange(customResults);

        // Assert
        Assert.False(isValid);
        // Should have validation errors for required fields
        Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(OverallBudget.SnapshotDate)));
        Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(OverallBudget.TotalMonthlyRevenue)));
        Assert.Contains(validationResults, vr => vr.MemberNames.Contains(nameof(OverallBudget.TotalCitizensServed)));
        // Note: IsCurrent has a valid default value of false, so no validation error expected
    }

    [Fact]
    public void OverallBudget_Default_Values_Are_Set_Correctly()
    {
        // Arrange
        var budget = new OverallBudget();

        // Assert
        Assert.Equal(0m, budget.TotalMonthlyRevenue);
        Assert.Equal(0m, budget.TotalMonthlyExpenses);
        Assert.Equal(0m, budget.TotalMonthlyBalance);
        Assert.Equal(0, budget.TotalCitizensServed);
        Assert.Equal(0m, budget.AverageRatePerCitizen);
        Assert.Equal(string.Empty, budget.Notes);
        Assert.False(budget.IsCurrent);
        Assert.False(budget.IsSurplus);
        Assert.Equal(0m, budget.DeficitPercentage);
        Assert.Equal(default(DateTime), budget.SnapshotDate);
    }

    [Fact]
    public void OverallBudget_CalculatedProperties_Update_When_Dependencies_Change()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 10000.00m,
            TotalMonthlyExpenses = 8000.00m,
            TotalMonthlyBalance = 2000.00m,
            TotalCitizensServed = 10000,
            IsCurrent = true
        };

        // Initially surplus
        Assert.True(budget.IsSurplus);
        Assert.Equal(20.0m, budget.DeficitPercentage);

        // Act - Change to deficit
        budget.TotalMonthlyBalance = -1000.00m;

        // Assert
        Assert.False(budget.IsSurplus);
        Assert.Equal(-10.0m, budget.DeficitPercentage);
    }

    [Fact]
    public void OverallBudget_PropertyChanged_Not_Raised_For_Same_Value()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 50000.00m,
            TotalCitizensServed = 25000,
            IsCurrent = true
        };

        var propertyChangedEvents = new List<string>();

        budget.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != null)
            {
                propertyChangedEvents.Add(args.PropertyName);
            }
        };

        // Act - Set same values
        budget.TotalMonthlyRevenue = 50000.00m;
        budget.TotalCitizensServed = 25000;
        budget.IsCurrent = true;

        // Assert - No events should be raised
        Assert.Empty(propertyChangedEvents);
    }

    [Fact]
    public void OverallBudget_DeficitPercentage_Handles_Zero_Revenue_Correctly()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = 0.00m,
            TotalMonthlyBalance = 1000.00m, // Positive balance but zero revenue
            TotalCitizensServed = 10000,
            IsCurrent = true
        };

        // Assert
        Assert.Equal(0m, budget.DeficitPercentage); // Should return 0 to avoid division by zero
    }

    [Fact]
    public void OverallBudget_DeficitPercentage_Handles_Negative_Revenue_Correctly()
    {
        // Arrange
        var budget = new OverallBudget
        {
            SnapshotDate = DateTime.Now,
            TotalMonthlyRevenue = -1000.00m, // Negative revenue (edge case)
            TotalMonthlyBalance = 500.00m,
            TotalCitizensServed = 10000,
            IsCurrent = true
        };

        // Assert
        Assert.Equal(0m, budget.DeficitPercentage); // Should return 0 for negative revenue
    }
}