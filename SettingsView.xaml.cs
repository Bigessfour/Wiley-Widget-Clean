using System;
using System.Windows;
using System.Windows.Media;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;

namespace WileyWidget
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : Window
    {
        private readonly SettingsViewModel _viewModel;

        public SettingsView()
        {
            InitializeComponent();

            // Apply current theme
            TryApplyTheme(SettingsService.Instance.Current.Theme);

            // Get the ViewModel from the service provider
            _viewModel = (SettingsViewModel)App.ServiceProvider.GetService(typeof(SettingsViewModel));
            if (_viewModel == null)
            {
                MessageBox.Show("Settings ViewModel could not be loaded. Please check the application configuration.",
                              "Configuration Error", MessageBoxButton.OK);
                Close();
                return;
            }

            DataContext = _viewModel;

            // Load settings when window opens
            Loaded += SettingsView_Loaded;
        }

        private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadSettingsAsync();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Optionally prompt to save changes
            if (_viewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Do you want to save them before closing?",
                                           "Unsaved Changes", MessageBoxButton.YesNoCancel);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        // Save changes synchronously to avoid async issues during closing
                        _viewModel.SaveSettingsCommand.Execute(null);
                        break;
                    case MessageBoxResult.Cancel:
                        e.Cancel = true;
                        return;
                }
            }

            base.OnClosing(e);
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
}