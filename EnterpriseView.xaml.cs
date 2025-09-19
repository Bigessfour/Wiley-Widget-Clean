using System.Windows;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;

namespace WileyWidget;

/// <summary>
/// Enterprise Management Window - Provides full CRUD interface for municipal enterprises
/// </summary>
public partial class EnterpriseView : Window
{
    public EnterpriseView()
    {
        InitializeComponent();

        // Apply current theme
        TryApplyTheme(SettingsService.Instance.Current.Theme);

        // Get the EnterpriseViewModel from DI container
        var enterpriseRepository = App.ServiceProvider.GetRequiredService<IEnterpriseRepository>();
        DataContext = new EnterpriseViewModel(enterpriseRepository);

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