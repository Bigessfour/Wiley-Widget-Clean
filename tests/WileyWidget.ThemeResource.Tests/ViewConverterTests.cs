using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Views;

namespace WileyWidget.ThemeResource.Tests;

public sealed class ViewConverterTests
{
    [Fact]
    public void Boolean_to_visibility_converter_handles_true_and_false()
    {
        var converter = new BooleanToVisibilityConverter();
        Assert.Equal(Visibility.Visible, converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Boolean_to_visibility_converter_convert_back_roundtrips_visibility()
    {
        var converter = new BooleanToVisibilityConverter();
        Assert.True((bool)converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture));
        Assert.False((bool)converter.ConvertBack(Visibility.Hidden, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(10, "5", 1)]
    [InlineData(5, "5", 0)]
    [InlineData(2, "5", -1)]
    [InlineData("invalid", "5", -1)]
    [InlineData(5, null, 0)]
    public void Comparison_converter_returns_expected_result(object? value, string? parameter, int expected)
    {
        var converter = new ComparisonConverter();
        var result = converter.Convert(value!, typeof(int), parameter!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Comparison_converter_convert_back_not_supported()
    {
        var converter = new ComparisonConverter();
        Assert.Throws<NotImplementedException>(() => converter.ConvertBack(0, typeof(object), null!, CultureInfo.InvariantCulture));
    }

    public static IEnumerable<object[]> StatusToColorConverterCases()
    {
        yield return new object[] { "Error connecting to server", Colors.Red };
        yield return new object[] { "Warning: something odd", Colors.Orange };
        yield return new object[] { "Operation completed successfully", Colors.Green };
        yield return new object[] { "Unknown", Colors.Black };
    }

    [Theory]
    [MemberData(nameof(StatusToColorConverterCases))]
    public void Status_to_color_converter_maps_keywords(string message, Color expectedColor)
    {
        StaTestRunner.Run(() =>
        {
            var converter = new StatusToColorConverter();
            var brush = Assert.IsType<SolidColorBrush>(converter.Convert(message, typeof(Brush), null!, CultureInfo.InvariantCulture));
            Assert.Equal(expectedColor, brush.Color);

            var convertBackResult = converter.ConvertBack(brush, typeof(string), null!, CultureInfo.InvariantCulture);
            Assert.Same(DependencyProperty.UnsetValue, convertBackResult);
        });
    }
}
