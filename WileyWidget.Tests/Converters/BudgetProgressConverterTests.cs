using System;
using System.Globalization;
using System.Windows.Data;
using Xunit;
using WileyWidget;

namespace WileyWidget.Tests.Converters
{
/// <summary>
/// Comprehensive tests for BudgetProgressConverter
/// Tests budget amount scaling to progress bar values (0-100)
/// </summary>
public class BudgetProgressConverterTests
{
    private readonly BudgetProgressConverter _converter;

    public BudgetProgressConverterTests()
    {
        _converter = new BudgetProgressConverter();
    }

    [Fact]
    public void Convert_WithValidDecimal_ReturnsScaledValue()
    {
        // Arrange
        decimal budgetAmount = 50000m; // 50% of max budget (100,000)
        const double expectedProgress = 50.0;

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(expectedProgress, (double)result);
    }

    [Fact]
    public void Convert_WithZero_ReturnsZero()
    {
        // Arrange
        decimal budgetAmount = 0m;

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void Convert_WithMaxBudget_Returns100()
    {
        // Arrange
        decimal budgetAmount = 100000m; // Max budget

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(100.0, (double)result);
    }

    [Fact]
    public void Convert_WithOverMaxBudget_Returns100()
    {
        // Arrange
        decimal budgetAmount = 150000m; // Over max budget

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(100.0, (double)result);
    }

    [Fact]
    public void Convert_WithNegativeValue_ReturnsZero()
    {
        // Arrange
        decimal budgetAmount = -5000m; // Negative budget

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void Convert_WithSmallAmount_ReturnsProportionalValue()
    {
        // Arrange
        decimal budgetAmount = 1000m; // 1% of max budget
        const double expectedProgress = 1.0;

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(expectedProgress, (double)result);
    }

    [Fact]
    public void Convert_WithFractionalAmount_ReturnsRoundedValue()
    {
        // Arrange
        decimal budgetAmount = 33333.33m; // Should be ~33.33%

        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        var actualProgress = (double)result;
        Assert.True(actualProgress >= 33.3 && actualProgress <= 33.4);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsZero()
    {
        // Arrange
        object? nullValue = null;

        // Act
        var result = _converter.Convert(nullValue!, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void Convert_WithNonDecimalValue_ReturnsZero()
    {
        // Arrange
        string invalidValue = "invalid";

        // Act
        var result = _converter.Convert(invalidValue, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void Convert_WithIntValue_ReturnsZero()
    {
        // Arrange
        int intValue = 50000;

        // Act
        var result = _converter.Convert(intValue, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void Convert_WithFloatValue_ReturnsZero()
    {
        // Arrange
        float floatValue = 50000.0f;

        // Act
        var result = _converter.Convert(floatValue, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(0.0, (double)result);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        double progressValue = 50.0;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack(progressValue, typeof(decimal), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(25000.0, 25.0)]
    [InlineData(50000.0, 50.0)]
    [InlineData(75000.0, 75.0)]
    [InlineData(100000.0, 100.0)]
    [InlineData(125000.0, 100.0)] // Over max should cap at 100
    public void Convert_WithVariousAmounts_ReturnsExpectedProgress(double budgetAmount, double expectedProgress)
    {
        // Act
        var result = _converter.Convert(budgetAmount, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<double>(result);
        Assert.Equal(expectedProgress, (double)result);
    }
}
}
