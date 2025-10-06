using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WileyWidget;

namespace WileyWidget.ThemeResource.Tests;

public sealed class MessageConverterTests
{
    [Fact]
    public void User_message_background_converter_returns_expected_brushes()
    {
        StaTestRunner.Run(() =>
        {
            var converter = new UserMessageBackgroundConverter();
            var userBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), null!, CultureInfo.InvariantCulture));
            var assistantBrush = Assert.IsType<SolidColorBrush>(converter.Convert(false, typeof(Brush), null!, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(25, 118, 210), userBrush.Color);
            Assert.Equal(Color.FromRgb(224, 224, 224), assistantBrush.Color);
        });
    }

    [Fact]
    public void Message_alignment_converter_supports_background_avatar_and_alignment()
    {
        StaTestRunner.Run(() =>
        {
            var converter = new MessageAlignmentConverter();

            var rightAlignment = converter.Convert(true, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);
            Assert.Equal(HorizontalAlignment.Right, rightAlignment);

            var leftAlignment = converter.Convert(false, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);
            Assert.Equal(HorizontalAlignment.Left, leftAlignment);

            var backgroundBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), "background", CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(25, 118, 210), backgroundBrush.Color);

            var avatarLabel = converter.Convert(false, typeof(string), "avatar", CultureInfo.InvariantCulture);
            Assert.Equal("AI", avatarLabel);
        });
    }

    [Fact]
    public void Message_foreground_converter_uses_parameter_tokens()
    {
        StaTestRunner.Run(() =>
        {
            var converter = new MessageForegroundConverter();
            var parameter = "#FFFFFFFF|#FF000000";

            var userBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), parameter, CultureInfo.InvariantCulture));
            Assert.True(userBrush.IsFrozen);
            Assert.Equal(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), userBrush.Color);

            var assistantBrush = Assert.IsType<SolidColorBrush>(converter.Convert(false, typeof(Brush), parameter, CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromArgb(0xFF, 0x00, 0x00, 0x00), assistantBrush.Color);

            // Invalid parameter should fall back to defaults
            var fallbackBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), "invalid", CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(255, 255, 255), fallbackBrush.Color);
        });
    }

    public static IEnumerable<object[]> ProfitLossConverterCases()
    {
        yield return new object[] { 10m, "Monthly Profit" };
        yield return new object[] { -5m, "Monthly Loss" };
        yield return new object[] { "N/A", "Monthly Position" };
    }

    [Theory]
    [MemberData(nameof(ProfitLossConverterCases))]
    public void Profit_loss_text_converter_returns_expected_labels(object input, string expected)
    {
        var converter = new ProfitLossTextConverter();
        var result = converter.Convert(input, typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Profit_brush_converters_return_expected_colors()
    {
        StaTestRunner.Run(() =>
        {
            var brushConverter = new ProfitBrushConverter();
            var borderConverter = new ProfitBorderBrushConverter();
            var textConverter = new ProfitTextBrushConverter();

            var profitBrush = Assert.IsType<SolidColorBrush>(brushConverter.Convert(5m, typeof(Brush), null!, CultureInfo.InvariantCulture));
            var lossBrush = Assert.IsType<SolidColorBrush>(brushConverter.Convert(-5m, typeof(Brush), null!, CultureInfo.InvariantCulture));
            var neutralBrush = Assert.IsType<SolidColorBrush>(brushConverter.Convert("unknown", typeof(Brush), null!, CultureInfo.InvariantCulture));

            Assert.Equal(Color.FromRgb(232, 245, 232), profitBrush.Color);
            Assert.Equal(Color.FromRgb(255, 243, 224), lossBrush.Color);
            Assert.Equal(Color.FromRgb(245, 245, 245), neutralBrush.Color);

            var borderBrush = Assert.IsType<SolidColorBrush>(borderConverter.Convert(-1m, typeof(Brush), null!, CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(245, 124, 0), borderBrush.Color);

            var textBrush = Assert.IsType<SolidColorBrush>(textConverter.Convert(1m, typeof(Brush), null!, CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(56, 142, 60), textBrush.Color);
        });
    }

    [Fact]
    public void Bool_to_background_converter_supports_parameter_customization()
    {
        StaTestRunner.Run(() =>
        {
            var converter = new BoolToBackgroundConverter();
            var parameter = "#FFFF0000|#FF00FF00";

            var errorBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), parameter, CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(255, 0, 0), errorBrush.Color);

            var successBrush = Assert.IsType<SolidColorBrush>(converter.Convert(false, typeof(Brush), parameter, CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(0, 255, 0), successBrush.Color);

            var fallbackBrush = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), "not-a-color", CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(255, 235, 238), fallbackBrush.Color);
        });
    }

    [Fact]
    public void Bool_to_foreground_converter_handles_invalid_parameters()
    {
        StaTestRunner.Run(() =>
        {
            var converter = new BoolToForegroundConverter();
            var result = Assert.IsType<SolidColorBrush>(converter.Convert(true, typeof(Brush), "bad|input", CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(211, 47, 47), result.Color);

            var falseResult = Assert.IsType<SolidColorBrush>(converter.Convert(false, typeof(Brush), "", CultureInfo.InvariantCulture));
            Assert.Equal(Color.FromRgb(56, 142, 60), falseResult.Color);
        });
    }

    [Theory]
    [InlineData(true, null, Visibility.Visible)]
    [InlineData(false, null, Visibility.Collapsed)]
    [InlineData("text", "invert", Visibility.Collapsed)]
    [InlineData("", "empty", Visibility.Visible)]
    [InlineData("value", "notempty", Visibility.Visible)]
    [InlineData(0, "!", Visibility.Visible)]
    [InlineData(3, "3", Visibility.Visible)]
    [InlineData(2, "3", Visibility.Collapsed)]
    [InlineData(null, null, Visibility.Collapsed)]
    public void Bool_to_visibility_converter_evaluates_parameters(object? input, string? parameter, Visibility expected)
    {
        var converter = new BoolToVisibilityConverter();
    var result = converter.Convert(input!, typeof(Visibility), parameter!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Bool_to_visibility_converter_convert_back_not_supported()
    {
        var converter = new BoolToVisibilityConverter();
    Assert.Throws<NotImplementedException>(() => converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("", Visibility.Visible)]
    [InlineData("text", Visibility.Collapsed)]
    [InlineData(null, Visibility.Collapsed)]
    public void Empty_string_to_visibility_converter_handles_input(string? input, Visibility expected)
    {
        var converter = new EmptyStringToVisibilityConverter();
    var result = converter.Convert(input!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    Assert.Throws<NotImplementedException>(() => converter.ConvertBack(Visibility.Visible, typeof(string), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(5, "5", Visibility.Visible)]
    [InlineData(3, "5", Visibility.Collapsed)]
    [InlineData(null, "5", Visibility.Collapsed)]
    public void Count_to_visibility_converter_requires_matching_target(object? value, string parameter, Visibility expected)
    {
        var converter = new CountToVisibilityConverter();
    var result = converter.Convert(value!, typeof(Visibility), parameter, CultureInfo.InvariantCulture);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Inverse_boolean_converter_inverts_values()
    {
        var converter = new InverseBooleanConverter();
    Assert.False((bool)converter.Convert(true, typeof(bool), null!, CultureInfo.InvariantCulture));
    Assert.True((bool)converter.Convert(false, typeof(bool), null!, CultureInfo.InvariantCulture));
    Assert.True((bool)converter.Convert("non-bool", typeof(bool), null!, CultureInfo.InvariantCulture));

    Assert.False((bool)converter.ConvertBack(true, typeof(bool), null!, CultureInfo.InvariantCulture));
    Assert.True((bool)converter.ConvertBack("text", typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Boolean_to_font_weight_converter_supports_parameter_overrides()
    {
        var converter = new BooleanToFontWeightConverter();
    var defaultResult = converter.Convert(true, typeof(FontWeight), null!, CultureInfo.InvariantCulture);
        Assert.Equal(FontWeights.Bold, defaultResult);

        var resultTrue = converter.Convert(true, typeof(FontWeight), "Bold|Light", CultureInfo.InvariantCulture);
        Assert.Equal(FontWeights.Bold, resultTrue);

        var resultFalse = converter.Convert(false, typeof(FontWeight), "Bold|Invalid", CultureInfo.InvariantCulture);
        Assert.Equal(FontWeights.Normal, resultFalse);

    Assert.Equal(Binding.DoNothing, converter.ConvertBack(FontWeights.Bold, typeof(bool), null!, CultureInfo.InvariantCulture));
    }
}
