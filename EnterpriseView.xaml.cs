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
/// Enterprise Management Window - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : Window
{
    private readonly IServiceScope _viewScope;

    public EnterpriseView()
    {
        InitializeComponent();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        // Create a scope for the view and resolve the repository from the scope
        var provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        if (provider != null)
        {
            _viewScope = provider.CreateScope();
            var enterpriseRepository = _viewScope.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
            DataContext = new EnterpriseViewModel(enterpriseRepository);

            // Dispose the scope when the window is closed
            this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };
        }
        else
        {
            // For testing purposes, allow view to load without ViewModel
            _viewScope = null;
            DataContext = null;
        }

        // Load enterprises when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is EnterpriseViewModel vm)
            {
                await vm.LoadEnterprisesAsync();
            }
        };
    }

    /// <summary>
    /// Show the Enterprise Management window
    /// </summary>
    public static void ShowEnterpriseWindow()
    {
        var window = new EnterpriseView();
        window.Show();
    }

    /// <summary>
    /// Show the Enterprise Management window as dialog
    /// </summary>
    public static bool? ShowEnterpriseDialog()
    {
        var window = new EnterpriseView();
        return window.ShowDialog();
    }

    /// <summary>
    /// Attempt to apply a Syncfusion theme; falls back to Fluent Light if requested theme fails.
    /// </summary>
    private void TryApplyTheme(string themeName)
    {
        Services.ThemeUtility.TryApplyTheme(this, themeName);
    }
}