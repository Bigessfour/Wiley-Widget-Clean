using System;
using System.Windows;
using System.Windows.Threading;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using Serilog;

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
            if (App.ServiceProvider != null)
            {
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
            }
            else
            {
                // For testing purposes, allow view to load without ViewModel
                _viewModel = null;
                DataContext = null;
            }

            // Load dashboard data when window opens
            Loaded += DashboardView_Loaded;
            Closing += DashboardView_Closing;
            StateChanged += DashboardView_StateChanged;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
                await _viewModel.LoadDashboardDataAsync();
        }

        private void DashboardView_StateChanged(object sender, EventArgs e)
        {
            if (_refreshTimer == null) return;

            if (WindowState == WindowState.Minimized)
            {
                // Pause timer when window is minimized to save resources
                _refreshTimer.Stop();
                Log.Debug("Dashboard auto-refresh paused due to window minimization");
            }
            else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
            {
                // Resume timer when window is restored
                if (_viewModel.AutoRefreshEnabled)
                {
                    _refreshTimer.Start();
                    Log.Debug("Dashboard auto-refresh resumed after window restoration");
                }
            }
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
            Services.ThemeUtility.TryApplyTheme(this, themeName);
        }
    }
}