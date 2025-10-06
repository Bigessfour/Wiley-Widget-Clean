using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WileyWidget.Views;

/// <summary>
/// Converts boolean values to Visibility.
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a boolean to Visibility.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The visibility value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    /// <summary>
    /// Converts back from Visibility to boolean.
    /// </summary>
    /// <param name="value">The visibility value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The boolean value.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts values for comparison operations.
/// </summary>
public class ComparisonConverter : IValueConverter
{
    /// <summary>
    /// Converts a value by comparing it to a parameter.
    /// Returns 1 if value > parameter, -1 if value < parameter, 0 if equal.
    /// </summary>
    /// <param name="value">The value to compare.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The comparison parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>1 for greater than, -1 for less than, 0 for equal.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return 0;

        // Try to parse the parameter
        if (!decimal.TryParse(parameter.ToString(), out var compareValue))
            return 0;

        // Convert value to decimal for comparison
        decimal actualValue = 0;
        if (value is decimal dec)
            actualValue = dec;
        else if (decimal.TryParse(value.ToString(), out var parsed))
            actualValue = parsed;

        return actualValue.CompareTo(compareValue);
    }

    /// <summary>
    /// Not implemented.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>Not implemented.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts status messages to appropriate colors.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    /// <summary>
    /// Converts a status message string to a color based on its content.
    /// </summary>
    /// <param name="value">The status message string.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The appropriate color brush.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string message)
        {
            if (message.Contains("Error") || message.Contains("Failed") || message.Contains("failed"))
                return Brushes.Red;
            if (message.Contains("Warning") || message.Contains("warning"))
                return Brushes.Orange;
            if (message.Contains("Success") || message.Contains("completed successfully"))
                return Brushes.Green;
        }

        return Brushes.Black; // Default color
    }

    /// <summary>
    /// ConvertBack is not supported for this converter.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="parameter">The parameter.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>DependencyProperty.UnsetValue to indicate conversion back is not supported.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}