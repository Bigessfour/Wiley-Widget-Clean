using System;
using System.Globalization;
using System.Windows.Media;

namespace WileyWidget.ThemeResource.Tests;

public sealed class CurrencyBalanceBudgetConverterTests
{
    [Fact]
    public void Currency_format_converter_returns_en_us_culture()
    {
        var converter = new CurrencyFormatConverter();
    var result = converter.Convert(123.45m, typeof(string), null!, CultureInfo.InvariantCulture);

        var culture = Assert.IsType<CultureInfo>(result);
        Assert.Equal("en-US", culture.Name);
    }

    [Fact]
    public void Currency_format_converter_convert_back_not_supported()
    {
        var converter = new CurrencyFormatConverter();
    Assert.Throws<NotImplementedException>(() => converter.ConvertBack("$1.00", typeof(decimal), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Balance_color_converter_handles_numeric_inputs()
    {
        var converter = new BalanceColorConverter();

    Assert.Same(Brushes.Green, converter.Convert(10m, typeof(Brush), null!, CultureInfo.InvariantCulture));
    Assert.Same(Brushes.Red, converter.Convert(-5.0, typeof(Brush), null!, CultureInfo.InvariantCulture));
    Assert.Same(Brushes.Gray, converter.Convert(0, typeof(Brush), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Balance_color_converter_defaults_to_gray_for_unknown_types()
    {
        var converter = new BalanceColorConverter();
    Assert.Same(Brushes.Gray, converter.Convert("not-a-number", typeof(Brush), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Balance_color_converter_convert_back_not_supported()
    {
        var converter = new BalanceColorConverter();
    Assert.Throws<NotImplementedException>(() => converter.ConvertBack(Brushes.Green, typeof(decimal), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50000, 50)]
    [InlineData(100000, 100)]
    [InlineData(150000, 100)]
    [InlineData(-10000, 0)]
    public void Budget_progress_converter_scales_decimal(decimal input, double expected)
    {
        var converter = new BudgetProgressConverter();
    var result = converter.Convert(input, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(25000.0, 25.0)]
    [InlineData(200000.0, 100.0)]
    [InlineData(-5000.0, 0.0)]
    public void Budget_progress_converter_scales_double(double input, double expected)
    {
        var converter = new BudgetProgressConverter();
    var result = converter.Convert(input, typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Budget_progress_converter_defaults_to_zero_for_unknown_types()
    {
        var converter = new BudgetProgressConverter();
    var result = converter.Convert("unknown", typeof(double), null!, CultureInfo.InvariantCulture);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Budget_progress_converter_convert_back_not_supported()
    {
        var converter = new BudgetProgressConverter();
    Assert.Throws<NotImplementedException>(() => converter.ConvertBack(50.0, typeof(decimal), null!, CultureInfo.InvariantCulture));
    }
}
