using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget;

/// <summary>
/// Converter for user message background color
/// </summary>
public class UserMessageBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUser && isUser)
        {
            return new SolidColorBrush(Color.FromRgb(25, 118, 210)); // Blue for user
        }
        return new SolidColorBrush(Color.FromRgb(224, 224, 224)); // Gray for AI
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for message alignment
/// </summary>
public class MessageAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool isUser && isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for message text color
/// </summary>
public class MessageForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUser && isUser)
        {
            return Brushes.White;
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for profit/loss display
/// </summary>
public class ProfitLossTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? "Monthly Profit" : "Monthly Loss";
        }
        return "Monthly Position";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for profit/loss background color
/// </summary>
public class ProfitBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(232, 245, 232)) : new SolidColorBrush(Color.FromRgb(255, 243, 224));
        }
        return new SolidColorBrush(Color.FromRgb(245, 245, 245));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for profit/loss border color
/// </summary>
public class ProfitBorderBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(56, 142, 60)) : new SolidColorBrush(Color.FromRgb(245, 124, 0));
        }
        return new SolidColorBrush(Color.FromRgb(189, 189, 189));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for profit/loss text color
/// </summary>
public class ProfitTextBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal profit)
        {
            return profit >= 0 ? new SolidColorBrush(Color.FromRgb(56, 142, 60)) : new SolidColorBrush(Color.FromRgb(245, 124, 0));
        }
        return new SolidColorBrush(Color.FromRgb(33, 33, 33));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to background color
/// </summary>
public class BoolToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasError && hasError)
        {
            return new SolidColorBrush(Color.FromRgb(255, 235, 238));
        }
        return new SolidColorBrush(Color.FromRgb(232, 245, 232));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to visibility (inverse)
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for empty string to visibility
/// </summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for count to visibility
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count && parameter is string param)
        {
            int targetCount = int.Parse(param);
            return count == targetCount ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to foreground color
/// </summary>
public class BoolToForegroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool hasError && hasError)
        {
            return new SolidColorBrush(Color.FromRgb(211, 47, 47));
        }
        return new SolidColorBrush(Color.FromRgb(56, 142, 60));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}