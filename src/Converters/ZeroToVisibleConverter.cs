using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WileyWidget.Converters;

/// <summary>
/// Converts integer count to Visibility - shows element when count is zero
/// Used for empty state messages in data grids
/// </summary>
public class ZeroToVisibleConverter : IValueConverter
{
    /// <summary>
    /// Converts integer count to Visibility
    /// </summary>
    /// <param name="value">Integer count value</param>
    /// <param name="targetType">Target type (Visibility)</param>
    /// <param name="parameter">Optional parameter</param>
    /// <param name="culture">Culture info</param>
    /// <returns>Visible if count is 0, Collapsed otherwise</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    /// <summary>
    /// Not implemented for one-way binding
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("ZeroToVisibleConverter does not support two-way binding");
    }
}
