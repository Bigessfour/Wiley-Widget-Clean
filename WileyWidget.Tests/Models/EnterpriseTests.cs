using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for Enterprise model
/// Tests INotifyPropertyChanged, calculated properties, validation, and business logic
/// </summary>
public class EnterpriseTests
{
    [Fact]
    public void Enterprise_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var enterprise = new Enterprise();

        // Assert
        Assert.Equal(0, enterprise.Id);
        Assert.Equal(string.Empty, enterprise.Name);
        Assert.Equal(0m, enterprise.CurrentRate);
        Assert.Equal(0m, enterprise.MonthlyExpenses);
        Assert.Equal(0, enterprise.CitizenCount);
        Assert.Equal(0m, enterprise.TotalBudget);
        Assert.Null(enterprise.LastModified);
        Assert.Equal(string.Empty, enterprise.Type);
        Assert.Equal(string.Empty, enterprise.Notes);
        Assert.NotNull(enterprise.BudgetInteractions);
        Assert.Empty(enterprise.BudgetInteractions);
    }

    [Fact]
    public void Enterprise_PropertyChanged_EventsAreRaised()
    {
        // Arrange
        var enterprise = new Enterprise();
        var propertyChangedEvents = new List<string>();

        enterprise.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        enterprise.Name = "Water Department";
        enterprise.CurrentRate = 25.50m;
        enterprise.MonthlyExpenses = 15000.00m;
        enterprise.CitizenCount = 1000;
        enterprise.TotalBudget = 200000.00m;
        enterprise.Type = "Utility";
        enterprise.Notes = "City water utility";

        // Assert
        Assert.Contains(nameof(Enterprise.Name), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.CurrentRate), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.MonthlyExpenses), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.CitizenCount), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.TotalBudget), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.Type), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.Notes), propertyChangedEvents);
    }

    [Fact]
    public void Enterprise_PropertyChanged_CalculatedPropertiesAreUpdated()
    {
        // Arrange
        var enterprise = new Enterprise();
        var propertyChangedEvents = new List<string>();

        enterprise.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        enterprise.CitizenCount = 1000;
        enterprise.CurrentRate = 25.50m;
        enterprise.MonthlyExpenses = 15000.00m;

        // Assert
        Assert.Contains(nameof(Enterprise.MonthlyRevenue), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.MonthlyBalance), propertyChangedEvents);
        Assert.Contains(nameof(Enterprise.BreakEvenRate), propertyChangedEvents);
    }

    [Fact]
    public void Enterprise_MonthlyRevenue_CalculatesCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise();

        // Act
        enterprise.CitizenCount = 1000;
        enterprise.CurrentRate = 25.50m;

        // Assert
        Assert.Equal(25500.00m, enterprise.MonthlyRevenue);
    }

    [Fact]
    public void Enterprise_MonthlyBalance_CalculatesCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            CitizenCount = 1000,
            CurrentRate = 25.50m,
            MonthlyExpenses = 20000.00m
        };

        // Assert
        Assert.Equal(5500.00m, enterprise.MonthlyBalance); // 25500 - 20000
    }

    [Fact]
    public void Enterprise_BreakEvenRate_CalculatesCorrectly()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            CitizenCount = 1000,
            MonthlyExpenses = 20000.00m
        };

        // Assert
        Assert.Equal(20.00m, enterprise.BreakEvenRate); // 20000 / 1000
    }

    [Fact]
    public void Enterprise_BreakEvenRate_ZeroCitizenCount_ReturnsZero()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            CitizenCount = 0,
            MonthlyExpenses = 20000.00m
        };

        // Assert
        Assert.Equal(0m, enterprise.BreakEvenRate);
    }

    [Fact]
    public void Enterprise_PropertyChanged_NoEventWhenValueUnchanged()
    {
        // Arrange
        var enterprise = new Enterprise { Name = "Test Enterprise" };
        var eventRaised = false;

        enterprise.PropertyChanged += (sender, args) => eventRaised = true;

        // Act
        enterprise.Name = "Test Enterprise"; // Same value

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void Enterprise_Validation_ValidModel_Passes()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void Enterprise_Validation_MissingName_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Enterprise name is required");
    }

    [Fact]
    public void Enterprise_Validation_NameTooLong_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = new string('A', 101), // 101 characters, exceeds max of 100
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Enterprise name cannot exceed 100 characters");
    }

    [Fact]
    public void Enterprise_Validation_ZeroCurrentRate_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 0m, // Below minimum of 0.01
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Rate must be between 0.01 and 9999.99");
    }

    [Fact]
    public void Enterprise_Validation_CurrentRateTooHigh_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 10000.00m, // Above maximum of 9999.99
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Rate must be between 0.01 and 9999.99");
    }

    [Fact]
    public void Enterprise_Validation_NegativeMonthlyExpenses_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 25.50m,
            MonthlyExpenses = -1000.00m, // Negative expenses
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Monthly expenses cannot be negative");
    }

    [Fact]
    public void Enterprise_Validation_ZeroCitizenCount_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 0 // Below minimum of 1
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Citizen count must be at least 1");
    }

    [Fact]
    public void Enterprise_Validation_TypeTooLong_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000,
            Type = new string('A', 51) // 51 characters, exceeds max of 50
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Type cannot exceed 50 characters");
    }

    [Fact]
    public void Enterprise_Validation_NotesTooLong_Fails()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Water Department",
            CurrentRate = 25.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 1000,
            Notes = new string('A', 501) // 501 characters, exceeds max of 500
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Notes cannot exceed 500 characters");
    }

    [Theory]
    [InlineData("Water Department", 25.50, 15000.00, 1000)]
    [InlineData("Sewer Services", 15.75, 8000.00, 500)]
    [InlineData("Trash Collection", 12.00, 12000.00, 2000)]
    [InlineData("Apartments", 45.00, 25000.00, 300)]
    public void Enterprise_Validation_ValidScenarios_Pass(string name, decimal rate, decimal expenses, int citizens)
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = name,
            CurrentRate = rate,
            MonthlyExpenses = expenses,
            CitizenCount = citizens
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void Enterprise_BudgetInteractions_CollectionCanBeModified()
    {
        // Arrange
        var enterprise = new Enterprise();
        var interaction1 = new BudgetInteraction { Id = 1, InteractionType = "SharedCost" };
        var interaction2 = new BudgetInteraction { Id = 2, InteractionType = "Dependency" };

        // Act
        enterprise.BudgetInteractions.Add(interaction1);
        enterprise.BudgetInteractions.Add(interaction2);

        // Assert
        Assert.Equal(2, enterprise.BudgetInteractions.Count);
        Assert.Contains(interaction1, enterprise.BudgetInteractions);
        Assert.Contains(interaction2, enterprise.BudgetInteractions);
    }

    [Fact]
    public void Enterprise_LastModified_CanBeSet()
    {
        // Arrange
        var enterprise = new Enterprise();
        var testDate = new DateTime(2025, 9, 19, 10, 30, 0);

        // Act
        enterprise.LastModified = testDate;

        // Assert
        Assert.Equal(testDate, enterprise.LastModified);
    }

    [Fact]
    public void Enterprise_CalculatedProperties_UpdateWhenDependenciesChange()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            CitizenCount = 1000,
            CurrentRate = 20.00m,
            MonthlyExpenses = 15000.00m
        };

        // Initial calculations
        Assert.Equal(20000.00m, enterprise.MonthlyRevenue);
        Assert.Equal(5000.00m, enterprise.MonthlyBalance);
        Assert.Equal(15.00m, enterprise.BreakEvenRate);

        // Act - Change citizen count
        enterprise.CitizenCount = 1200;

        // Assert - Revenue and break-even rate should update
        Assert.Equal(24000.00m, enterprise.MonthlyRevenue);
        Assert.Equal(9000.00m, enterprise.MonthlyBalance);
        Assert.Equal(12.50m, enterprise.BreakEvenRate); // 15000 / 1200
    }
}