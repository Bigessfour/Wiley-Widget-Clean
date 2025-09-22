using System.Windows;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;

namespace WileyWidget;

/// <summary>
/// Customer Management Window - Provides full CRUD interface for utility customers
/// </summary>
public partial class UtilityCustomerView : Window
{
    private readonly IServiceScope _viewScope;

    public UtilityCustomerView()
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

    // Create a scope for the view and resolve the repository from the scope
    var provider = App.ServiceProvider ?? Application.Current.Properties["ServiceProvider"] as IServiceProvider;
    if (provider == null) throw new InvalidOperationException("ServiceProvider is not available for UtilityCustomerView");
    _viewScope = provider.CreateScope();
    var customerRepository = _viewScope.ServiceProvider.GetRequiredService<IUtilityCustomerRepository>();
    DataContext = new UtilityCustomerViewModel(customerRepository);

    // Dispose the scope when the window is closed
    this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };

        // Load customers when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is UtilityCustomerViewModel vm)
            {
                await vm.LoadCustomersAsync();
            }
        };
    }

    /// <summary>
    /// Show the Customer Management window
    /// </summary>
    public static void ShowCustomerWindow()
    {
        var window = new UtilityCustomerView();
        window.Show();
    }

    /// <summary>
    /// Show the Customer Management window as dialog
    /// </summary>
    public static bool? ShowCustomerDialog()
    {
        var window = new UtilityCustomerView();
        return window.ShowDialog();
    }
}