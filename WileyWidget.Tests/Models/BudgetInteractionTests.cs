using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for BudgetInteraction model
/// Tests validation, property constraints, and business rules
/// </summary>
public class BudgetInteractionTests
{
    [Fact]
    public void BudgetInteraction_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction();

        // Assert
        Assert.Equal(0, interaction.Id);
        Assert.Equal(0, interaction.PrimaryEnterpriseId);
        Assert.Null(interaction.SecondaryEnterpriseId);
        Assert.Equal(string.Empty, interaction.InteractionType);
        Assert.Equal(string.Empty, interaction.Description);
        Assert.Equal(0m, interaction.MonthlyAmount);
        Assert.True(interaction.IsCost);
        Assert.Equal(string.Empty, interaction.Notes);
        Assert.Null(interaction.PrimaryEnterprise);
        Assert.Null(interaction.SecondaryEnterprise);
    }

    [Fact]
    public void BudgetInteraction_PropertyAssignment_WorksCorrectly()
    {
        // Arrange
        var interaction = new BudgetInteraction();

        // Act
        interaction.Id = 1;
        interaction.PrimaryEnterpriseId = 10;
        interaction.SecondaryEnterpriseId = 20;
        interaction.InteractionType = "SharedCost";
        interaction.Description = "Shared IT infrastructure costs";
        interaction.MonthlyAmount = 5000.00m;
        interaction.IsCost = false;
        interaction.Notes = "Quarterly review required";

        // Assert
        Assert.Equal(1, interaction.Id);
        Assert.Equal(10, interaction.PrimaryEnterpriseId);
        Assert.Equal(20, interaction.SecondaryEnterpriseId);
        Assert.Equal("SharedCost", interaction.InteractionType);
        Assert.Equal("Shared IT infrastructure costs", interaction.Description);
        Assert.Equal(5000.00m, interaction.MonthlyAmount);
        Assert.False(interaction.IsCost);
        Assert.Equal("Quarterly review required", interaction.Notes);
    }

    [Fact]
    public void BudgetInteraction_Validation_ValidModel_Passes()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = 1000.00m,
            IsCost = true
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void BudgetInteraction_Validation_MissingPrimaryEnterpriseId_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Primary enterprise is required");
    }

    [Fact]
    public void BudgetInteraction_Validation_MissingInteractionType_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            Description = "Valid description",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Interaction type is required");
    }

    [Fact]
    public void BudgetInteraction_Validation_MissingDescription_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Description is required");
    }

    [Fact]
    public void BudgetInteraction_Validation_InteractionTypeTooLong_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = new string('A', 51), // 51 characters, exceeds max of 50
            Description = "Valid description",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Interaction type cannot exceed 50 characters");
    }

    [Fact]
    public void BudgetInteraction_Validation_DescriptionTooLong_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = new string('A', 201), // 201 characters, exceeds max of 200
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Description cannot exceed 200 characters");
    }

    [Fact]
    public void BudgetInteraction_Validation_NotesTooLong_Fails()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = 1000.00m,
            Notes = new string('A', 301) // 301 characters, exceeds max of 300
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Notes cannot exceed 300 characters");
    }

    [Fact]
    public void BudgetInteraction_Validation_InteractionTypeMaxLength_Passes()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = new string('A', 50), // Exactly 50 characters
            Description = "Valid description",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void BudgetInteraction_Validation_DescriptionMaxLength_Passes()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = new string('A', 200), // Exactly 200 characters
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void BudgetInteraction_Validation_NotesMaxLength_Passes()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = 1000.00m,
            Notes = new string('A', 300) // Exactly 300 characters
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("SharedCost")]
    [InlineData("Dependency")]
    [InlineData("Transfer")]
    [InlineData("Allocation")]
    [InlineData("Reimbursement")]
    public void BudgetInteraction_InteractionType_AcceptsValidTypes(string interactionType)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = interactionType,
            Description = "Valid description",
            MonthlyAmount = 1000.00m
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(1000.50)]
    [InlineData(-500)] // Negative amounts should be allowed (refunds/credits)
    [InlineData(999999.99)]
    public void BudgetInteraction_MonthlyAmount_AcceptsVariousValues(decimal amount)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = amount
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void BudgetInteraction_IsCost_DefaultsToTrue()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction();

        // Assert
        Assert.True(interaction.IsCost);
    }

    [Fact]
    public void BudgetInteraction_IsCost_CanBeSetToFalse()
    {
        // Arrange
        var interaction = new BudgetInteraction();

        // Act
        interaction.IsCost = false;

        // Assert
        Assert.False(interaction.IsCost);
    }

    [Fact]
    public void BudgetInteraction_SecondaryEnterpriseId_CanBeNull()
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Valid description",
            MonthlyAmount = 1000.00m,
            SecondaryEnterpriseId = null
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
        Assert.Null(interaction.SecondaryEnterpriseId);
    }

    [Fact]
    public void BudgetInteraction_NavigationProperties_CanBeSet()
    {
        // Arrange
        var primaryEnterprise = new Enterprise { Id = 1, Name = "Primary Corp" };
        var secondaryEnterprise = new Enterprise { Id = 2, Name = "Secondary Corp" };

        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            SecondaryEnterpriseId = 2,
            InteractionType = "SharedCost",
            Description = "Shared costs",
            MonthlyAmount = 5000.00m
        };

        // Act
        interaction.PrimaryEnterprise = primaryEnterprise;
        interaction.SecondaryEnterprise = secondaryEnterprise;

        // Assert
        Assert.Equal(primaryEnterprise, interaction.PrimaryEnterprise);
        Assert.Equal(secondaryEnterprise, interaction.SecondaryEnterprise);
    }
}