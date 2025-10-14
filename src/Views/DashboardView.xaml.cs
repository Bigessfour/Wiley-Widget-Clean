using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WileyWidget.ViewModels;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Services;
using Serilog;

#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8622, CS8625 // Suppress nullability warnings in WPF application

namespace WileyWidget
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private DispatcherTimer _refreshTimer;

        public DashboardView()
        {
            InitializeComponent();

            // Note: ViewModel is now auto-wired by Prism ViewModelLocator
            // Theme is applied declaratively in XAML to prevent loading crashes

            // Set up auto-refresh timer if ViewModel is available
            if (DataContext is DashboardViewModel viewModel)
            {
                SetupAutoRefreshTimer(viewModel);
            }

            // Load dashboard data when control loads
            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DashboardViewModel viewModel)
            {
                await viewModel.LoadDashboardDataAsync();
            }
        }

        private void SetupAutoRefreshTimer(DashboardViewModel viewModel)
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Tick += async (s, e) =>
            {
                if (viewModel.AutoRefreshEnabled)
                {
                    await viewModel.RefreshDashboardDataAsync();
                }
            };
            _refreshTimer.Interval = TimeSpan.FromMinutes(viewModel.RefreshIntervalMinutes);
            _refreshTimer.Start();
        }
    }
}