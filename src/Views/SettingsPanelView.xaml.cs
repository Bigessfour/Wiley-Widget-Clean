using System.Windows.Controls;
using WileyWidget.ViewModels;

namespace WileyWidget.Views;

/// <summary>
/// Settings panel view for embedding in docking layout
/// </summary>
public partial class SettingsPanelView : UserControl
{
    private readonly SettingsViewModel? _viewModel;

    public SettingsPanelView()
    {
        InitializeComponent();

        // Get the ViewModel from the service provider
        System.IServiceProvider? provider = null;
        try
        {
            provider = App.GetActiveServiceProvider();
        }
        catch (System.InvalidOperationException)
        {
            provider = null;
        }

        if (provider != null)
        {
            _viewModel = (SettingsViewModel?)provider.GetService(typeof(SettingsViewModel));
            if (_viewModel == null)
            {
                // Don't show modal dialogs - fall back to a lightweight DataContext
                Serilog.Log.Error("Settings ViewModel could not be loaded. Falling back to test-friendly DataContext.");
                DataContext = new { Title = "Settings" };
            }
            else
            {
                DataContext = _viewModel;
            }
        }
        else
        {
            // In test environments, ServiceProvider might not be available
            DataContext = new { Title = "Settings (Test Mode)" };
        }

        // Load settings when control loads
        Loaded += SettingsPanelView_Loaded;
    }

    private async void SettingsPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadSettingsAsync();
        }
    }
}