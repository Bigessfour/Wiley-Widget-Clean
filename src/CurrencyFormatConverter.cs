using System;
using System.Globalization;
using System.Windows.Data;

namespace WileyWidget;

/// <summary>
/// Converter for formatting currency values in charts
/// </summary>
public class CurrencyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return new CultureInfo("en-US");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}