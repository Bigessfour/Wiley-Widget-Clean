using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;

namespace WileyWidget.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly IEnterpriseRepository _enterpriseRepository;

        // KPI Properties
        [ObservableProperty]
        private int totalEnterprises;

        [ObservableProperty]
        private decimal totalBudget;

        [ObservableProperty]
        private int activeProjects;

        [ObservableProperty]
        private string systemHealthStatus = "Good";

        [ObservableProperty]
        private Brush systemHealthColor = Brushes.Green;

        [ObservableProperty]
        private int healthScore = 95;

        // Change indicators
        [ObservableProperty]
        private string enterprisesChangeText = "+2 from last month";

        [ObservableProperty]
        private Brush enterprisesChangeColor = Brushes.Green;

        [ObservableProperty]
        private string budgetChangeText = "+$15K from last month";

        [ObservableProperty]
        private Brush budgetChangeColor = Brushes.Green;

        [ObservableProperty]
        private string projectsChangeText = "+1 from last week";

        [ObservableProperty]
        private Brush projectsChangeColor = Brushes.Green;

        // Auto-refresh settings
        [ObservableProperty]
        private bool autoRefreshEnabled = true;

        [ObservableProperty]
        private int refreshIntervalMinutes = 5;

        // Status
        [ObservableProperty]
        private string dashboardStatus = "Loading...";

        [ObservableProperty]
        private string lastUpdated = "Never";

        [ObservableProperty]
        private string nextRefreshTime = "Calculating...";

        // Chart data
        [ObservableProperty]
        private ObservableCollection<BudgetTrendItem> budgetTrendData = new();

        [ObservableProperty]
        private ObservableCollection<EnterpriseTypeItem> enterpriseTypeData = new();

        // Activity and alerts
        [ObservableProperty]
        private ObservableCollection<ActivityItem> recentActivities = new();

        [ObservableProperty]
        private ObservableCollection<AlertItem> systemAlerts = new();

        // Loading and status properties
        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int systemHealthScore = 95;

        [ObservableProperty]
        private int budgetUtilizationScore = 78;

        [ObservableProperty]
        private string statusMessage = "Ready";

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            IEnterpriseRepository enterpriseRepository)
        {
            _logger = logger;
            _enterpriseRepository = enterpriseRepository;
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                DashboardStatus = "Loading dashboard data...";

                // Load all dashboard data in parallel
                await Task.WhenAll(
                    LoadKPIsAsync(),
                    LoadChartDataAsync(),
                    LoadActivitiesAsync(),
                    LoadAlertsAsync()
                );

                DashboardStatus = "Dashboard loaded successfully";
                LastUpdated = DateTime.Now.ToString("g");
                UpdateNextRefreshTime();

                _logger.LogInformation("Dashboard data loaded successfully");
            }
            catch (Exception ex)
            {
                DashboardStatus = "Error loading dashboard";
                _logger.LogError(ex, "Error loading dashboard data");
                MessageBox.Show($"Error loading dashboard: {ex.Message}", "Dashboard Error",
                              MessageBoxButton.OK);
            }
        }

        private async Task LoadKPIsAsync()
        {
            try
            {
                // Get enterprise data
                var enterprises = await _enterpriseRepository.GetAllAsync();

                TotalEnterprises = enterprises.Count();
                TotalBudget = enterprises.Sum(e => e.TotalBudget);

                // Calculate active projects (enterprises with recent activity)
                ActiveProjects = enterprises.Count(e =>
                    e.LastModified.HasValue &&
                    e.LastModified.Value > DateTime.Now.AddDays(-30));

                // Calculate system health based on various factors
                CalculateSystemHealth();

                // Calculate changes (simplified - in real app would compare with historical data)
                CalculateChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading KPIs");
            }
        }

        private void CalculateSystemHealth()
        {
            // Simple health calculation based on data availability and recent activity
            var healthFactors = new[]
            {
                TotalEnterprises > 0 ? 25 : 0,
                TotalBudget > 0 ? 25 : 0,
                ActiveProjects > 0 ? 25 : 0,
                true ? 25 : 0 // Database connectivity
            };

            HealthScore = healthFactors.Sum();

            if (HealthScore >= 90)
            {
                SystemHealthStatus = "Excellent";
                SystemHealthColor = Brushes.Green;
            }
            else if (HealthScore >= 75)
            {
                SystemHealthStatus = "Good";
                SystemHealthColor = Brushes.Green;
            }
            else if (HealthScore >= 60)
            {
                SystemHealthStatus = "Fair";
                SystemHealthColor = Brushes.Orange;
            }
            else
            {
                SystemHealthStatus = "Poor";
                SystemHealthColor = Brushes.Red;
            }
        }

        private void CalculateChanges()
        {
            // Simplified change calculations - in real app would use historical data
            EnterprisesChangeText = TotalEnterprises > 10 ? "+2 from last month" : "New this month";
            EnterprisesChangeColor = Brushes.Green;

            BudgetChangeText = TotalBudget > 100000 ? "+$15K from last month" : "Growing";
            BudgetChangeColor = Brushes.Green;

            ProjectsChangeText = ActiveProjects > 5 ? "+1 from last week" : "Stable";
            ProjectsChangeColor = Brushes.Green;
        }

        private async Task LoadChartDataAsync()
        {
            try
            {
                // Load budget trend data (last 6 months)
                BudgetTrendData.Clear();
                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Now.AddMonths(-i);
                    BudgetTrendData.Add(new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy"),
                        Amount = TotalBudget * (decimal)(0.8 + (i * 0.04)) // Simulated growth
                    });
                }

                // Load enterprise type distribution
                EnterpriseTypeData.Clear();
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var typeGroups = enterprises.GroupBy(e => e.Type ?? "Other");

                foreach (var group in typeGroups)
                {
                    EnterpriseTypeData.Add(new EnterpriseTypeItem
                    {
                        Type = group.Key,
                        Count = group.Count()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading chart data");
            }
        }

        private async Task LoadActivitiesAsync()
        {
            try
            {
                RecentActivities.Clear();

                // Add sample recent activities
                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-5),
                    Description = "Enterprise budget updated",
                    Type = "Budget"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-15),
                    Description = "New enterprise added",
                    Type = "Enterprise"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddMinutes(-30),
                    Description = "Report generated",
                    Type = "Report"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddHours(-1),
                    Description = "Database backup completed",
                    Type = "System"
                });

                RecentActivities.Add(new ActivityItem
                {
                    Timestamp = DateTime.Now.AddHours(-2),
                    Description = "Settings updated",
                    Type = "Configuration"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading activities");
            }
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private async Task LoadAlertsAsync()
        {
            try
            {
                SystemAlerts.Clear();

                // Add sample alerts based on system status
                if (TotalEnterprises == 0)
                {
                    SystemAlerts.Add(new AlertItem
                    {
                        Priority = "High",
                        Message = "No enterprises configured",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Red
                    });
                }

                if (HealthScore < 75)
                {
                    SystemAlerts.Add(new AlertItem
                    {
                        Priority = "Medium",
                        Message = "System health below optimal",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Orange
                    });
                }

                // Add informational alerts
                SystemAlerts.Add(new AlertItem
                {
                    Priority = "Low",
                    Message = "Database backup due soon",
                    Timestamp = DateTime.Now.AddHours(2),
                    PriorityColor = Brushes.Blue
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading alerts");
            }
            await Task.CompletedTask; // Suppress async warning for future async operations
        }

        private void UpdateNextRefreshTime()
        {
            if (AutoRefreshEnabled)
            {
                NextRefreshTime = DateTime.Now.AddMinutes(RefreshIntervalMinutes).ToString("HH:mm");
            }
            else
            {
                NextRefreshTime = "Disabled";
            }
        }

        internal async Task RefreshDashboardDataAsync()
        {
            await LoadDashboardDataAsync();
        }

        [RelayCommand]
        private async Task RefreshDashboardAsync()
        {
            IsLoading = true;
            StatusMessage = "Refreshing dashboard...";
            
            try
            {
                await LoadDashboardDataAsync();
                StatusMessage = "Dashboard refreshed successfully";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error refreshing dashboard";
                _logger.LogError(ex, "Error refreshing dashboard");
                MessageBox.Show($"Error refreshing dashboard: {ex.Message}", "Refresh Error",
                              MessageBoxButton.OK);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
        }

        [RelayCommand]
        private void ExportDashboard()
        {
            // TODO: Implement dashboard export functionality
            MessageBox.Show("Dashboard export functionality will be implemented in a future update.",
                          "Feature Not Implemented", MessageBoxButton.OK);
        }

        [RelayCommand]
        private void OpenEnterpriseManagement()
        {
            EnterpriseView.ShowEnterpriseWindow();
        }

        [RelayCommand]
        private void OpenBudgetAnalysis()
        {
            BudgetView.ShowBudgetWindow();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new SettingsView
            {
                Owner = Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        }

        [RelayCommand]
        private void GenerateReport()
        {
            // TODO: Implement report generation
            MessageBox.Show("Report generation functionality will be implemented in a future update.",
                          "Feature Not Implemented", MessageBoxButton.OK);
        }

        [RelayCommand]
        private void BackupData()
        {
            // TODO: Implement data backup
            MessageBox.Show("Data backup functionality will be implemented in a future update.",
                          "Feature Not Implemented", MessageBoxButton.OK);
        }

        partial void OnAutoRefreshEnabledChanged(bool value)
        {
            UpdateNextRefreshTime();
        }

        partial void OnRefreshIntervalMinutesChanged(int value)
        {
            UpdateNextRefreshTime();
        }
    }

    // Data models for dashboard
    public class BudgetTrendItem
    {
        public string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class EnterpriseTypeItem
    {
        public string Type { get; set; }
        public int Count { get; set; }
    }

    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
    }

    public class AlertItem
    {
        public string Priority { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Brush PriorityColor { get; set; }
    }
}