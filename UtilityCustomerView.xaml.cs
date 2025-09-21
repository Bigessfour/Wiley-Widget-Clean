using System.Windows;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace WileyWidget;

/// <summary>
/// Customer Management Window - Provides full CRUD interface for utility customers
/// </summary>
public partial class UtilityCustomerView : Window
{
    public UtilityCustomerView()
    {
        InitializeComponent();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        // Get the UtilityCustomerViewModel from DI container
        var customerRepository = App.ServiceProvider.GetRequiredService<IUtilityCustomerRepository>();
        DataContext = new UtilityCustomerViewModel(customerRepository);

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

    /// <summary>
    /// Attempts to apply the specified theme to the window
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        try
        {
#pragma warning disable CA2000 // Call System.IDisposable.Dispose on object created by 'new Theme(themeName)' before all references to it are out of scope
            SfSkinManager.SetTheme(this, new Theme(themeName));
#pragma warning restore CA2000
        }
        catch
        {
            // Ignore theme application errors
        }
    }
}