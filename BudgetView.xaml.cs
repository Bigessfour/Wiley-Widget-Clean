using System.Windows;
using System.Windows.Media;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Budget Analysis and Reporting Window
/// Provides comprehensive financial analysis and reporting capabilities
/// </summary>
public partial class BudgetView : Window
{
    private readonly IServiceScope _viewScope;

    public BudgetView()
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

    // Create a scope for the view and resolve the repository from the scope
    var provider = App.ServiceProvider ?? Application.Current.Properties["ServiceProvider"] as IServiceProvider;
    if (provider == null) throw new InvalidOperationException("ServiceProvider is not available for BudgetView");
    _viewScope = provider.CreateScope();
    var enterpriseRepository = _viewScope.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
    DataContext = new BudgetViewModel(enterpriseRepository);

    // Dispose the scope when the window is closed
    this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };

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