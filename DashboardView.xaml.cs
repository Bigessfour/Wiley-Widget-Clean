using System;
using System.Windows;
using System.Windows.Threading;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;

namespace WileyWidget
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : Window
    {
        private readonly DashboardViewModel _viewModel;
        private DispatcherTimer _refreshTimer;

        public DashboardView()
        {
            InitializeComponent();

            // Apply current theme
            TryApplyTheme(SettingsService.Instance.Current.Theme);

            // Get the ViewModel from the service provider
            _viewModel = (DashboardViewModel)App.ServiceProvider.GetService(typeof(DashboardViewModel));
            if (_viewModel == null)
            {
                MessageBox.Show("Dashboard ViewModel could not be loaded. Please check the application configuration.",
                              "Configuration Error", MessageBoxButton.OK);
                Close();
                return;
            }

            DataContext = _viewModel;

            // Set up auto-refresh timer
            SetupAutoRefreshTimer();

            // Load dashboard data when window opens
            Loaded += DashboardView_Loaded;
            Closing += DashboardView_Closing;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadDashboardDataAsync();
        }

        private void DashboardView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Clean up timer
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }
        }

        private void SetupAutoRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += async (s, e) =>
            {
                if (_viewModel.AutoRefreshEnabled)
                {
                    await _viewModel.RefreshDashboardDataAsync();
                }
            };
            _refreshTimer.Interval = TimeSpan.FromMinutes(_viewModel.RefreshIntervalMinutes);
            _refreshTimer.Start();
        }

        public static void ShowDashboardWindow()
        {
            var dashboardWindow = new DashboardView();
            dashboardWindow.Show();
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