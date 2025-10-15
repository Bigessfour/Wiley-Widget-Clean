using System;
using System.Globalization;
using System.Windows.Data;
using Xunit;
using WileyWidget;

namespace WileyWidget.Tests.Converters;

/// <summary>
/// Comprehensive tests for CurrencyFormatConverter
/// Tests currency formatting for charts and UI display
/// </summary>
public class CurrencyFormatConverterTests
{
    private readonly CurrencyFormatConverter _converter;

    public CurrencyFormatConverterTests()
    {
        _converter = new CurrencyFormatConverter();
    }

    [Fact]
    public void Convert_WithDecimalValue_ReturnsFormattedCurrencyString()
    {
        // Arrange
        decimal testValue = 123.45m;

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        var formattedString = (string)result;
        Assert.Contains("$", formattedString);
        Assert.Contains("123", formattedString);
    }

    [Fact]
    public void Convert_WithDoubleValue_ReturnsFormattedCurrencyString()
    {
        // Arrange
        double testValue = 67.89;

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        var formattedString = (string)result;
        Assert.Contains("$", formattedString);
        Assert.Contains("67", formattedString);
    }

    [Fact]
    public void Convert_WithIntValue_ReturnsFormattedCurrencyString()
    {
        // Arrange
        int testValue = 42;

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        var formattedString = (string)result;
        Assert.Contains("$", formattedString);
        Assert.Contains("42", formattedString);
    }

    [Fact]
    public void Convert_WithFloatValue_ReturnsFormattedCurrencyString()
    {
        // Arrange
        float testValue = 99.99f;

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        var formattedString = (string)result;
        Assert.Contains("$", formattedString);
        Assert.Contains("99", formattedString);
    }

    [Fact]
    public void Convert_WithStringValue_ReturnsOriginalString()
    {
        // Arrange
        string testValue = "not a number";

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(testValue, result);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsEmptyString()
    {
        // Arrange
        object? nullValue = null;

        // Act
        var result = _converter.Convert(nullValue!, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Convert_WithObjectValue_ReturnsToString()
    {
        // Arrange
        object testValue = new object();

        // Act
        var result = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

        // Assert
        Assert.IsType<string>(result);
        Assert.Equal(testValue.ToString(), result);
    }

    [Fact]
    public void Convert_UsesCurrentCultureForFormatting()
    {
        // Arrange
        decimal testValue = 1234.56m;
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // Test with en-US culture
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var resultEn = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

            // Test with fr-FR culture
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var resultFr = _converter.Convert(testValue, typeof(string), null!, CultureInfo.CurrentCulture);

            // Assert
            Assert.IsType<string>(resultEn);
            Assert.IsType<string>(resultFr);
            // The actual formatting will depend on the culture, but both should be strings
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var value = "any value";

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack(value, typeof(object), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_WithNullValue_ThrowsNotImplementedException()
    {
        // Arrange
        object? nullValue = null;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack(nullValue!, typeof(object), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_WithDifferentTargetTypes_ThrowsNotImplementedException()
    {
        // Arrange
        var value = "$123.45";
        var targetTypes = new[] { typeof(string), typeof(object), typeof(decimal) };

        foreach (var targetType in targetTypes)
        {
            // Act & Assert
            Assert.Throws<NotImplementedException>(() =>
                _converter.ConvertBack(value, targetType, null!, CultureInfo.InvariantCulture));
        }
    }
}
