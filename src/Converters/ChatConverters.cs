using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget;

/// <summary>
/// Converter that returns Visible when value is 0, Collapsed otherwise
/// Used for empty state display in collections
/// </summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for chat message background color based on author name
/// </summary>
public class AuthorBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string authorName)
        {
            // "You" = user message (right side, gray), others = AI message (left side, blue)
            return authorName == "You" ? "#F5F5F5" : "#E3F2FD";
        }
        return "#F5F5F5";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for chat message alignment based on author name
/// </summary>
public class AuthorAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string authorName)
        {
            // "You" = user message (right side), others = AI message (left side)
            return authorName == "You" ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

