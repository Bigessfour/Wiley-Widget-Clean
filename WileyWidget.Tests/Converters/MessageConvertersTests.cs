using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Xunit;
using WileyWidget;

#nullable enable

namespace WileyWidget.Tests.Converters;

/// <summary>
/// Comprehensive tests for all MessageConverters
/// Tests message styling, alignment, and color logic for chat interface
/// </summary>
public class MessageConvertersTests
{
    #region UserMessageBackgroundConverter Tests

    [Fact]
    public void UserMessageBackgroundConverter_WithTrueValue_ReturnsBlueBrush()
    {
        // Arrange
        var converter = new UserMessageBackgroundConverter();
        bool isUser = true;
        var expectedColor = Color.FromRgb(0, 123, 255); // Blue

        // Act
        var result = converter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(expectedColor, brush.Color);
    }

    [Fact]
    public void UserMessageBackgroundConverter_WithFalseValue_ReturnsGrayBrush()
    {
        // Arrange
        var converter = new UserMessageBackgroundConverter();
        bool isUser = false;
        var expectedColor = Color.FromRgb(224, 224, 224); // Gray

        // Act
        var result = converter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(expectedColor, brush.Color);
    }

    [Fact]
#pragma warning disable CS8604, CS8625
    public void UserMessageBackgroundConverter_WithNullValue_ReturnsGrayBrush()
    {
        // Arrange
        var converter = new UserMessageBackgroundConverter();
        object? nullValue = null;
        var expectedColor = Color.FromRgb(224, 224, 224); // Gray (default for non-user)

        // Act
        var result = converter.Convert(nullValue, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(expectedColor, brush.Color);
    }
#pragma warning restore CS8604, CS8625

    [Fact]
    public void UserMessageBackgroundConverter_WithNonBoolValue_ReturnsGrayBrush()
    {
        // Arrange
        var converter = new UserMessageBackgroundConverter();
        string nonBoolValue = "invalid";
        var expectedColor = Color.FromRgb(224, 224, 224); // Gray (default for non-user)

        // Act
        var result = converter.Convert(nonBoolValue, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(expectedColor, brush.Color);
    }

    [Fact]
    public void UserMessageBackgroundConverter_ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new UserMessageBackgroundConverter();
        var brush = new SolidColorBrush(Colors.Blue);

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(brush, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    #endregion

    #region MessageAlignmentConverter Tests

    [Fact]
    public void MessageAlignmentConverter_WithTrueValue_ReturnsRightAlignment()
    {
        // Arrange
        var converter = new MessageAlignmentConverter();
        bool isUser = true;

        // Act
        var result = converter.Convert(isUser, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<HorizontalAlignment>(result);
        Assert.Equal(HorizontalAlignment.Right, (HorizontalAlignment)result);
    }

    [Fact]
    public void MessageAlignmentConverter_WithFalseValue_ReturnsLeftAlignment()
    {
        // Arrange
        var converter = new MessageAlignmentConverter();
        bool isUser = false;

        // Act
        var result = converter.Convert(isUser, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<HorizontalAlignment>(result);
        Assert.Equal(HorizontalAlignment.Left, (HorizontalAlignment)result);
    }

    [Fact]
#pragma warning disable CS8604, CS8625
    public void MessageAlignmentConverter_WithNullValue_ReturnsLeftAlignment()
    {
        // Arrange
        var converter = new MessageAlignmentConverter();
        object? nullValue = null;

        // Act
        var result = converter.Convert(nullValue, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<HorizontalAlignment>(result);
        Assert.Equal(HorizontalAlignment.Left, (HorizontalAlignment)result);
    }
#pragma warning restore CS8604, CS8625

    [Fact]
    public void MessageAlignmentConverter_WithNonBoolValue_ReturnsLeftAlignment()
    {
        // Arrange
        var converter = new MessageAlignmentConverter();
        string nonBoolValue = "invalid";

        // Act
        var result = converter.Convert(nonBoolValue, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<HorizontalAlignment>(result);
        Assert.Equal(HorizontalAlignment.Left, (HorizontalAlignment)result);
    }

    [Fact]
    public void MessageAlignmentConverter_ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new MessageAlignmentConverter();
        var alignment = HorizontalAlignment.Right;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(alignment, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    #endregion

    #region MessageForegroundConverter Tests

    [Fact]
    public void MessageForegroundConverter_WithTrueValue_ReturnsWhiteBrush()
    {
        // Arrange
        var converter = new MessageForegroundConverter();
        bool isUser = true;

        // Act
        var result = converter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Brushes.White.Color, brush.Color);
    }

    [Fact]
    public void MessageForegroundConverter_WithFalseValue_ReturnsBlackBrush()
    {
        // Arrange
        var converter = new MessageForegroundConverter();
        bool isUser = false;

        // Act
        var result = converter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Brushes.Black.Color, brush.Color);
    }

    [Fact]
#pragma warning disable CS8604, CS8625
    public void MessageForegroundConverter_WithNullValue_ReturnsBlackBrush()
    {
        // Arrange
        var converter = new MessageForegroundConverter();
        object? nullValue = null;

        // Act
        var result = converter.Convert(nullValue, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Brushes.Black.Color, ((SolidColorBrush)result).Color);
    }
#pragma warning restore CS8604, CS8625

    [Fact]
    public void MessageForegroundConverter_WithNonBoolValue_ReturnsBlackBrush()
    {
        // Arrange
        var converter = new MessageForegroundConverter();
        string nonBoolValue = "invalid";

        // Act
        var result = converter.Convert(nonBoolValue, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(result);
        var brush = (SolidColorBrush)result;
        Assert.Equal(Brushes.Black.Color, brush.Color);
    }

    [Fact]
    public void MessageForegroundConverter_ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var converter = new MessageForegroundConverter();
        var brush = Brushes.White;

        // Act & Assert
        Assert.Throws<NotImplementedException>(() =>
            converter.ConvertBack(brush, typeof(bool), null!, CultureInfo.InvariantCulture));
    }

    #endregion

    #region Integration Tests

    [Theory]
    [InlineData(true, 25, 118, 210, 255, 255, 255)] // User: Blue background, White text
    [InlineData(false, 224, 224, 224, 0, 0, 0)]    // AI: Gray background, Black text
    public void MessageConverters_Integration_UserVsAiStyling(
        bool isUser,
        byte expectedBgR, byte expectedBgG, byte expectedBgB,
        byte expectedFgR, byte expectedFgG, byte expectedFgB)
    {
        // Arrange
        var backgroundConverter = new UserMessageBackgroundConverter();
        var foregroundConverter = new MessageForegroundConverter();
        var alignmentConverter = new MessageAlignmentConverter();

        // Act
        var background = backgroundConverter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);
        var foreground = foregroundConverter.Convert(isUser, typeof(SolidColorBrush), null!, CultureInfo.InvariantCulture);
        var alignment = alignmentConverter.Convert(isUser, typeof(HorizontalAlignment), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.IsType<SolidColorBrush>(background);
        Assert.IsType<SolidColorBrush>(foreground);
        Assert.IsType<HorizontalAlignment>(alignment);

        var bgBrush = (SolidColorBrush)background;
        var fgBrush = (SolidColorBrush)foreground;
        var hAlignment = (HorizontalAlignment)alignment;

        Assert.Equal(expectedBgR, bgBrush.Color.R);
        Assert.Equal(expectedBgG, bgBrush.Color.G);
        Assert.Equal(expectedBgB, bgBrush.Color.B);

        Assert.Equal(expectedFgR, fgBrush.Color.R);
        Assert.Equal(expectedFgG, fgBrush.Color.G);
        Assert.Equal(expectedFgB, fgBrush.Color.B);

        Assert.Equal(isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left, hAlignment);
    }

    #endregion
}