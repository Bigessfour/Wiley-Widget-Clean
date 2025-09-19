using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace WileyWidget;

/// <summary>
/// Converter for scaling budget amounts to progress bar values (0-100)
/// </summary>
public class BudgetProgressConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal budgetAmount)
        {
            // Scale the budget amount to a 0-100 range for progress bar
            // Assuming max budget is around $100,000, scale accordingly
            const decimal maxBudget = 100000m;
            var scaledValue = (budgetAmount / maxBudget) * 100;
            return Math.Min(Math.Max(scaledValue, 0), 100);
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}