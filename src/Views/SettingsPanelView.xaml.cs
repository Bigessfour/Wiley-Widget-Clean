using System.Windows.Controls;
using WileyWidget.ViewModels;

namespace WileyWidget.Views;

/// <summary>
/// Settings panel view for embedding in docking layout
/// </summary>
public partial class SettingsPanelView : UserControl
{
    public SettingsPanelView()
    {
        InitializeComponent();

        // DataContext will be auto-wired by Prism ViewModelLocator

        // Load settings when control loads
        Loaded += SettingsPanelView_Loaded;
    }

    private async void SettingsPanelView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            await viewModel.LoadSettingsAsync();
        }
    }
}