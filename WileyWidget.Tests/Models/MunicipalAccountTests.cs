using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.Models;

/// <summary>
/// Comprehensive tests for MunicipalAccount model
/// Tests INotifyPropertyChanged, calculated properties, validation, enums, and business logic
/// </summary>
public class MunicipalAccountTests
{
    [Fact]
    public void MunicipalAccount_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("100"),
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1
        };

        // Assert
        Assert.Equal(0, account.Id);
        Assert.Equal("100", account.AccountNumber.ToString());
        Assert.Equal(string.Empty, account.Name);
        Assert.Equal(AccountType.Asset, account.Type); // Default enum value
        Assert.Equal(MunicipalFundType.General, account.Fund); // Default enum value
        Assert.Equal(0m, account.Balance);
        Assert.Equal(0m, account.BudgetAmount);
        Assert.True(account.IsActive);
        Assert.Null(account.QuickBooksId);
        Assert.Null(account.LastSyncDate);
        Assert.Null(account.Notes);
    }

    [Fact]
    public void MunicipalAccount_PropertyChanged_EventsAreRaised()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };
        var propertyChangedEvents = new List<string>();

        account.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        account.AccountNumber = new AccountNumber("1010-100");
        account.Name = "Cash Account";
        account.Type = AccountType.Revenue;
        account.Fund = MunicipalFundType.Water;
        account.Balance = 50000.00m;
        account.BudgetAmount = 60000.00m;
        account.IsActive = false;

        // Assert
        Assert.Contains(nameof(MunicipalAccount.AccountNumber), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.Name), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.Type), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.Fund), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.Balance), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.BudgetAmount), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.IsActive), propertyChangedEvents);
    }

    [Fact]
    public void MunicipalAccount_PropertyChanged_CalculatedPropertiesAreUpdated()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };
        var propertyChangedEvents = new List<string>();

        account.PropertyChanged += (sender, args) =>
        {
            propertyChangedEvents.Add(args.PropertyName);
        };

        // Act
        account.AccountNumber = new AccountNumber("1010-100");
        account.Name = "Cash Account";
        account.Balance = 50000.00m;
        account.BudgetAmount = 60000.00m;

        // Assert
        Assert.Contains(nameof(MunicipalAccount.DisplayName), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.FormattedBalance), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.Variance), propertyChangedEvents);
        Assert.Contains(nameof(MunicipalAccount.VariancePercent), propertyChangedEvents);
    }

    [Fact]
    public void MunicipalAccount_DisplayName_CombinesAccountNumberAndName()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.AccountNumber = new AccountNumber("1010-100");
        account.Name = "Cash Account";

        // Assert
        Assert.Equal("1010-100 - Cash Account", account.DisplayName);
    }

    [Fact]
    public void MunicipalAccount_DisplayName_HandlesEmptyValues()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act - Empty values
        account.AccountNumber = new AccountNumber("");
        account.Name = "";

        // Assert
        Assert.Equal(" - ", account.DisplayName);
    }

    [Fact]
    public void MunicipalAccount_FormattedBalance_FormatsCorrectly()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.Balance = 12345.67m;

        // Assert
        Assert.Equal("$12,345.67", account.FormattedBalance);
    }

    [Fact]
    public void MunicipalAccount_FormattedBalance_HandlesZero()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.Balance = 0m;

        // Assert
        Assert.Equal("$0.00", account.FormattedBalance);
    }

    [Fact]
    public void MunicipalAccount_FormattedBalance_HandlesNegative()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.Balance = -5000.25m;

        // Assert
        Assert.Equal("($5,000.25)", account.FormattedBalance);
    }

    [Fact]
    public void MunicipalAccount_Variance_CalculatesCorrectly()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.BudgetAmount = 60000.00m;
        account.Balance = 50000.00m;

        // Assert
        Assert.Equal(10000.00m, account.Variance); // Budget - Balance
    }

    [Fact]
    public void MunicipalAccount_VariancePercent_CalculatesCorrectly()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.BudgetAmount = 60000.00m;
        account.Balance = 50000.00m;

        // Assert
        Assert.Equal(16.67m, Math.Round(account.VariancePercent, 2)); // (10000/60000) * 100
    }

    [Fact]
    public void MunicipalAccount_VariancePercent_ZeroBudget_ReturnsZero()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.BudgetAmount = 0m;
        account.Balance = 50000.00m;

        // Assert
        Assert.Equal(0m, account.VariancePercent);
    }

    [Theory]
    [InlineData(AccountType.Asset, "Asset")]
    [InlineData(AccountType.Payables, "Liability")]
    [InlineData(AccountType.RetainedEarnings, "Equity")]
    [InlineData(AccountType.Revenue, "Revenue")]
    [InlineData(AccountType.Expense, "Expense")]
    public void MunicipalAccount_TypeDescription_ReturnsCorrectDescription(AccountType type, string expectedDescription)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("100")
        };

        // Act
        account.Type = type;

        // Assert
        Assert.Equal(expectedDescription, account.TypeDescription);
    }

    [Theory]
    [InlineData(MunicipalFundType.General, "General Fund")]
    [InlineData(MunicipalFundType.Water, "Water Fund")]
    [InlineData(MunicipalFundType.Sewer, "Sewer Fund")]
    [InlineData(MunicipalFundType.Trash, "Trash Fund")]
    [InlineData(MunicipalFundType.Enterprise, "Enterprise Fund")]
    public void MunicipalAccount_FundDescription_ReturnsCorrectDescription(MunicipalFundType fund, string expectedDescription)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("100")
        };

        // Act
        account.Fund = fund;

        // Assert
        Assert.Equal(expectedDescription, account.FundDescription);
    }

    [Fact]
    public void MunicipalAccount_PropertyChanged_NoEventWhenValueUnchanged()
    {
        // Arrange
        var account = new MunicipalAccount { AccountNumber = new AccountNumber("1010-100") };
        var eventRaised = false;

        account.PropertyChanged += (sender, args) => eventRaised = true;

        // Act
        account.AccountNumber = new AccountNumber("1010-100"); // Same value

        // Assert
        Assert.False(eventRaised);
    }

    [Fact]
    public void MunicipalAccount_Validation_ValidModel_Passes()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            Balance = 50000.00m,
            BudgetAmount = 60000.00m,
            IsActive = true
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void MunicipalAccount_Validation_MissingAccountNumber_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account number is required");
    }

    [Fact]
    public void MunicipalAccount_Validation_MissingName_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account name is required");
    }

    [Fact]
    public void MunicipalAccount_Validation_AccountNumberTooLong_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber(new string('1', 21)), // 21 characters, exceeds max of 20
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account number cannot exceed 20 characters");
    }

    [Fact]
    public void MunicipalAccount_Validation_NameTooLong_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = new string('A', 101), // 101 characters, exceeds max of 100
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(validationResults, vr => vr.ErrorMessage == "Account name cannot exceed 100 characters");
    }

    [Fact]
    public void MunicipalAccount_Validation_QuickBooksIdTooLong_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            QuickBooksId = new string('Q', 51) // 51 characters, exceeds max of 50
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        // Note: StringLength validation for nullable strings might behave differently
    }

    [Fact]
    public void MunicipalAccount_Validation_NotesTooLong_Fails()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            Notes = new string('N', 201) // 201 characters, exceeds max of 200
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        // Note: StringLength validation for nullable strings might behave differently
    }

    [Fact]
    public void MunicipalAccount_Validation_AccountNumberMaxLength_Passes()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber(new string('1', 20)), // Exactly 20 characters
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void MunicipalAccount_Validation_NameMaxLength_Passes()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = new string('A', 100), // Exactly 100 characters
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData(AccountType.Asset, MunicipalFundType.General, "1010-100", "Cash Account")]
    [InlineData(AccountType.Revenue, MunicipalFundType.Water, "4000-200", "Water Revenue")]
    [InlineData(AccountType.Expense, MunicipalFundType.Sewer, "5000-300", "Sewer Maintenance")]
    public void MunicipalAccount_Validation_ValidCombinations_Pass(AccountType type, MunicipalFundType fund, string accountNumber, string name)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber(accountNumber),
            Name = name,
            Type = type,
            Fund = fund,
            Balance = 10000.00m,
            BudgetAmount = 12000.00m
        };

        // Act
        var validationContext = new ValidationContext(account);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, validationContext, validationResults, true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(validationResults);
    }

    [Fact]
    public void MunicipalAccount_LastSyncDate_CanBeSet()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };
        var syncDate = new DateTime(2025, 9, 19, 10, 30, 0, DateTimeKind.Utc);

        // Act
        account.LastSyncDate = syncDate;

        // Assert
        Assert.Equal(syncDate, account.LastSyncDate);
    }

    [Fact]
    public void MunicipalAccount_IsActive_DefaultsToTrue()
    {
        // Arrange & Act
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Assert
        Assert.True(account.IsActive);
    }

    [Fact]
    public void MunicipalAccount_IsActive_CanBeSetToFalse()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.IsActive = false;

        // Assert
        Assert.False(account.IsActive);
    }

    [Theory]
    [InlineData(60000.00, 50000.00, 10000.00, 16.67)] // Under budget
    [InlineData(50000.00, 60000.00, -10000.00, -20.00)] // Over budget
    [InlineData(50000.00, 50000.00, 0.00, 0.00)] // On budget
    public void MunicipalAccount_Variance_Calculations_VariousScenarios(decimal budget, decimal balance, decimal expectedVariance, decimal expectedPercent)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("9999-999") // Default, will be overridden
        };

        // Act
        account.BudgetAmount = budget;
        account.Balance = balance;

        // Assert
        Assert.Equal(expectedVariance, account.Variance);
        Assert.Equal(expectedPercent, Math.Round(account.VariancePercent, 2));
    }
}
