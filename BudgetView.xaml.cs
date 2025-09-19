using System.Windows;
using System.Windows.Media;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace WileyWidget;

/// <summary>
/// Budget Analysis and Reporting Window
/// Provides comprehensive financial analysis and reporting capabilities
/// </summary>
public partial class BudgetView : Window
{
    public BudgetView()
    {
        InitializeComponent();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        // Get the BudgetViewModel from DI container
        var enterpriseRepository = App.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
        DataContext = new BudgetViewModel(enterpriseRepository);

        // Load budget data when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is BudgetViewModel vm)
            {
                await vm.RefreshBudgetDataAsync();
            }
        };
    }

    /// <summary>
    /// Show the Budget Analysis window
    /// </summary>
    public static void ShowBudgetWindow()
    {
        var window = new BudgetView();
        window.Show();
    }

    /// <summary>
    /// Show the Budget Analysis window as dialog
    /// </summary>
    public static bool? ShowBudgetDialog()
    {
        var window = new BudgetView();
        return window.ShowDialog();
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails.
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
            var canonical = NormalizeTheme(themeName);
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
            SfSkinManager.SetTheme(this, new Theme(canonical));
#pragma warning restore CA2000 // Dispose objects before losing scope
        }
        catch
        {
            if (themeName != "FluentLight")
            {
                // Fallback
#pragma warning disable CA2000 // Dispose objects before losing scope - Theme objects are managed by SfSkinManager
                try { SfSkinManager.SetTheme(this, new Theme("FluentLight")); } catch { /* ignore */ }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
        }
    }

    private string NormalizeTheme(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "FluentDark";
        raw = raw.Replace(" ", string.Empty); // allow "Fluent Dark" legacy
        return raw switch
        {
            "FluentDark" => "FluentDark",
            "FluentLight" => "FluentLight",
            _ => "FluentDark" // default
        };
    }
}

/// <summary>
/// Converter for balance color display (positive = green, negative = red)
/// </summary>
public class BalanceColorConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is decimal balance)
        {
            return balance >= 0 ? Brushes.Green : Brushes.Red;
        }
        return Brushes.Black;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new System.NotImplementedException();
    }
}