using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the MunicipalAccount model validation, business logic, and calculated properties
/// </summary>
public class MunicipalAccountTests
{
    [Fact]
    public void MunicipalAccount_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash - General Fund",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 50000.00m,
            BudgetAmount = 60000.00m,
            IsActive = true
        };

        // Assert
        Assert.Equal("1010-100", account.AccountNumber.Value);
        Assert.Equal("Cash - General Fund", account.Name);
        Assert.Equal(AccountType.Asset, account.Type);
        Assert.Equal(MunicipalFundType.General, account.Fund);
        Assert.Equal(50000.00m, account.Balance);
        Assert.Equal(60000.00m, account.BudgetAmount);
        Assert.True(account.IsActive);
        Assert.Equal(10000.00m, account.Variance); // 60000 - 50000
        Assert.Equal(16.67m, Math.Round(account.VariancePercent, 2)); // (10000/60000)*100
        Assert.Equal("$50,000.00", account.FormattedBalance);
        Assert.Equal("1010-100 - Cash - General Fund", account.DisplayName);
        Assert.Equal("Asset", account.TypeDescription);
        Assert.Equal("General Fund", account.FundDescription);
    }

    [Theory]
    [InlineData("", false)]              // Empty account number
    [InlineData(null, false)]           // Null account number
    [InlineData("1010-100", true)]      // Valid account number
    [InlineData("12345678901234567890", true)] // Max length (20 chars)
    [InlineData("123456789012345678901", false)] // Too long (21 chars)
    public void MunicipalAccount_AccountNumber_Validation(string accountNumber, bool shouldBeValid)
    {
        // Arrange & Act & Assert
        if (shouldBeValid)
        {
            // Should not throw
            var accountNumberObj = new AccountNumber(accountNumber);
            Assert.NotNull(accountNumberObj);
        }
        else
        {
            // Should throw exception
            Assert.Throws<ArgumentException>(() => new AccountNumber(accountNumber));
        }
    }

    [Theory]
    [InlineData("", false)]              // Empty name
    [InlineData(null, false)]           // Null name
    [InlineData("Valid Name", true)]    // Valid name
    [InlineData("A", true)]             // Minimum valid name
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", true)] // Max length (100 chars)
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", false)] // Too long (101 chars)
    public void MunicipalAccount_Name_Validation(string name, bool shouldBeValid)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = name,
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 1000.00m,
            BudgetAmount = 1000.00m
        };

        // Act
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(account, new ValidationContext(account), validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, vr => vr.ErrorMessage.Contains("Account name"));
        }
    }

    [Theory]
    [InlineData(AccountType.Asset, "Asset")]
    [InlineData(AccountType.Payables, "Liability")]
    [InlineData(AccountType.RetainedEarnings, "Equity")]
    [InlineData(AccountType.Revenue, "Revenue")]
    [InlineData(AccountType.Expense, "Expense")]
    public void MunicipalAccount_TypeDescription_ReturnsCorrectValue(AccountType type, string expectedDescription)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Test Account",
            Type = type,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 1000.00m,
            BudgetAmount = 1000.00m
        };

        // Act & Assert
        Assert.Equal(expectedDescription, account.TypeDescription);
    }

    [Theory]
    [InlineData(MunicipalFundType.General, "General Fund")]
    [InlineData(MunicipalFundType.Water, "Water Fund")]
    [InlineData(MunicipalFundType.Sewer, "Sewer Fund")]
    [InlineData(MunicipalFundType.Trash, "Trash Fund")]
    [InlineData(MunicipalFundType.Enterprise, "Enterprise Fund")]
    public void MunicipalAccount_FundDescription_ReturnsCorrectValue(MunicipalFundType fund, string expectedDescription)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Test Account",
            Type = AccountType.Asset,
            Fund = fund,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 1000.00m,
            BudgetAmount = 1000.00m
        };

        // Act & Assert
        Assert.Equal(expectedDescription, account.FundDescription);
    }

    [Theory]
    [InlineData(1000.00, 1000.00, 0.00, 0.00)]     // Balanced budget
    [InlineData(800.00, 1000.00, 200.00, 20.00)]   // Under budget
    [InlineData(1200.00, 1000.00, -200.00, -20.00)] // Over budget
    [InlineData(1000.00, 0.00, -1000.00, 0.00)]    // Zero budget (division by zero)
    public void MunicipalAccount_Variance_Calculations(decimal balance, decimal budget, decimal expectedVariance, decimal expectedPercent)
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Test Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = balance,
            BudgetAmount = budget
        };

        // Act & Assert
        Assert.Equal(expectedVariance, account.Variance);
        Assert.Equal(expectedPercent, Math.Round(account.VariancePercent, 2));
    }

    [Fact]
    public void MunicipalAccount_PropertyChanged_Events_Raised()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Test Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 1000.00m,
            BudgetAmount = 1000.00m
        };

        var propertyChangedEvents = new List<string>();
        account.PropertyChanged += (sender, e) => propertyChangedEvents.Add(e.PropertyName);

        // Act
        account.AccountNumber = new AccountNumber("2020-200");
        account.Name = "Updated Account";
        account.Type = AccountType.Payables;
        account.Fund = MunicipalFundType.Water;
        account.Balance = 2000.00m;
        account.BudgetAmount = 1500.00m;
        account.IsActive = false;

        // Assert
        Assert.Contains("AccountNumber", propertyChangedEvents);
        Assert.Contains("DisplayName", propertyChangedEvents); // Should be raised when AccountNumber or Name changes
        Assert.Contains("Name", propertyChangedEvents);
        Assert.Contains("Type", propertyChangedEvents);
        Assert.Contains("TypeDescription", propertyChangedEvents);
        Assert.Contains("Fund", propertyChangedEvents);
        Assert.Contains("FundDescription", propertyChangedEvents);
        Assert.Contains("Balance", propertyChangedEvents);
        Assert.Contains("FormattedBalance", propertyChangedEvents);
        Assert.Contains("BudgetAmount", propertyChangedEvents);
        Assert.Contains("Variance", propertyChangedEvents);
        Assert.Contains("VariancePercent", propertyChangedEvents);
        Assert.Contains("IsActive", propertyChangedEvents);
    }

    [Fact]
    public void MunicipalAccount_DisplayName_Combines_AccountNumber_And_Name()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Cash Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1
        };

        // Act & Assert
        Assert.Equal("1010-100 - Cash Account", account.DisplayName);

        // Test changes propagate to DisplayName
        account.AccountNumber = new AccountNumber("2020-200");
        Assert.Equal("2020-200 - Cash Account", account.DisplayName);

        account.Name = "Petty Cash";
        Assert.Equal("2020-200 - Petty Cash", account.DisplayName);
    }

    [Fact]
    public void MunicipalAccount_FormattedBalance_Includes_Currency_Symbol()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("1010-100"),
            Name = "Test Account",
            Type = AccountType.Asset,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            DepartmentId = 1,
            BudgetPeriodId = 1,
            Balance = 1234.56m
        };

        // Act & Assert
        Assert.Equal("$1,234.56", account.FormattedBalance);

        // Test negative balance
        account.Balance = -500.00m;
        Assert.Equal("($500.00)", account.FormattedBalance);
    }
}
