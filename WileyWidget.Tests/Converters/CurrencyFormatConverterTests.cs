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
    public void Convert_WithAnyValue_ReturnsEnUsCulture()
    {
        // Arrange
        object testValue = "test";

        // Act
        var result = _converter.Convert(testValue, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<CultureInfo>(result);
        var cultureInfo = (CultureInfo)result;
        Assert.Equal("en-US", cultureInfo.Name);
    }

    [Fact]
    public void Convert_WithNullValue_ReturnsEnUsCulture()
    {
        // Arrange
        object? nullValue = null;

        // Act
        var result = _converter.Convert(nullValue!, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<CultureInfo>(result);
        var cultureInfo = (CultureInfo)result;
        Assert.Equal("en-US", cultureInfo.Name);
    }

    [Fact]
    public void Convert_WithStringValue_ReturnsEnUsCulture()
    {
        // Arrange
        string stringValue = "currency";

        // Act
        var result = _converter.Convert(stringValue, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<CultureInfo>(result);
        var cultureInfo = (CultureInfo)result;
        Assert.Equal("en-US", cultureInfo.Name);
    }

    [Fact]
    public void Convert_WithNumericValue_ReturnsEnUsCulture()
    {
        // Arrange
        decimal numericValue = 123.45m;

        // Act
        var result = _converter.Convert(numericValue, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<CultureInfo>(result);
        var cultureInfo = (CultureInfo)result;
        Assert.Equal("en-US", cultureInfo.Name);
    }

    [Fact]
    public void Convert_WithDifferentTargetTypes_ReturnsEnUsCulture()
    {
        // Arrange
        object testValue = "test";
        var targetTypes = new[] { typeof(string), typeof(object), typeof(CultureInfo) };

        foreach (var targetType in targetTypes)
        {
            // Act
            var result = _converter.Convert(testValue, targetType, null!, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<CultureInfo>(result);
            var cultureInfo = (CultureInfo)result;
            Assert.Equal("en-US", cultureInfo.Name);
        }
    }

    [Fact]
    public void Convert_WithDifferentParameterValues_ReturnsEnUsCulture()
    {
        // Arrange
        object testValue = "test";
        var parameters = new object?[] { null, "param", 123, new object() };

        foreach (var parameter in parameters)
        {
            // Act
            var result = _converter.Convert(testValue, typeof(CultureInfo), parameter!, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<CultureInfo>(result);
            var cultureInfo = (CultureInfo)result;
            Assert.Equal("en-US", cultureInfo.Name);
        }
    }

    [Fact]
    public void Convert_WithDifferentCultureInfo_ReturnsEnUsCulture()
    {
        // Arrange
        object testValue = "test";
        var cultures = new[]
        {
            CultureInfo.InvariantCulture,
            CultureInfo.CurrentCulture,
            new CultureInfo("fr-FR"),
            new CultureInfo("de-DE"),
            new CultureInfo("ja-JP")
        };

        foreach (var culture in cultures)
        {
            // Act
            var result = _converter.Convert(testValue, typeof(CultureInfo), null!, culture);

            // Assert
            Assert.IsType<CultureInfo>(result);
            var cultureInfo = (CultureInfo)result;
            Assert.Equal("en-US", cultureInfo.Name);
        }
    }

    [Fact]
    public void Convert_ReturnsConsistentCultureInfo()
    {
        // Arrange
        object testValue = "test";

        // Act
        var result1 = _converter.Convert(testValue, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);
        var result2 = _converter.Convert(testValue, typeof(CultureInfo), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<CultureInfo>(result1);
        Assert.IsType<CultureInfo>(result2);
        var cultureInfo1 = (CultureInfo)result1;
        var cultureInfo2 = (CultureInfo)result2;
        Assert.Equal(cultureInfo1.Name, cultureInfo2.Name);
        Assert.Equal("en-US", cultureInfo1.Name);
    }

    [Fact]
    public void ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var cultureInfo = new CultureInfo("en-US");

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            _converter.ConvertBack(cultureInfo, typeof(object), null!, CultureInfo.InvariantCulture));
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
        var cultureInfo = new CultureInfo("en-US");
        var targetTypes = new[] { typeof(string), typeof(object), typeof(CultureInfo) };

        foreach (var targetType in targetTypes)
        {
            // Act & Assert
            Assert.Throws<NotImplementedException>(() =>
                _converter.ConvertBack(cultureInfo, targetType, null!, CultureInfo.InvariantCulture));
        }
    }
}
