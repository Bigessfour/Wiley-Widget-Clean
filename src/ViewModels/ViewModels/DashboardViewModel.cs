using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels
{
    public partial class DashboardViewModel : AsyncViewModelBase
    {
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
        private ThreadSafeObservableCollection<BudgetTrendItem> budgetTrendData = new();

        [ObservableProperty]
        private ThreadSafeObservableCollection<EnterpriseTypeItem> enterpriseTypeData = new();

        // Progress bar values (0-100)
        [ObservableProperty]
        private double systemHealthScore;

        [ObservableProperty]
        private double budgetUtilizationScore;

        // Activity and alerts
        [ObservableProperty]
        private ThreadSafeObservableCollection<ActivityItem> recentActivities = new();

        [ObservableProperty]
        private ThreadSafeObservableCollection<AlertItem> systemAlerts = new();

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            IEnterpriseRepository enterpriseRepository,
            IDispatcherHelper dispatcherHelper)
            : base(dispatcherHelper, logger)
        {
            _enterpriseRepository = enterpriseRepository;
        }

        public async Task LoadDashboardDataAsync()
        {
            try
            {
                await ExecuteAsyncOperation(async (cancellationToken) =>
                {
                await Task.WhenAll(
                        LoadKPIsAsync(),
                        LoadChartDataAsync(),
                        LoadGaugeDataAsync(),
                        LoadActivitiesAsync(),
                        LoadAlertsAsync()
                    );

                    LastUpdated = DateTime.Now.ToString("g");
                    UpdateNextRefreshTime();

                    Logger.LogInformation("Dashboard data loaded successfully");
                }, statusMessage: "Loading dashboard data...");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading dashboard data");

                // Use error reporting service for structured error handling and UI feedback
                var correlationId = Guid.NewGuid().ToString("N")[..8];
                Services.ErrorReportingService.Instance.ReportError(
                    ex,
                    "Dashboard_Data_Load",
                    showToUser: true,
                    level: LogEventLevel.Error,
                    correlationId: correlationId);
            }
        }

        private async Task LoadKPIsAsync()
        {
            try
            {
                // Simulate additional processing delay for testing timing
                await Task.Delay(500);

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
                Logger.LogError(ex, "Error loading KPIs");
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
                var budgetTrendItems = new System.Collections.Generic.List<BudgetTrendItem>();
                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Now.AddMonths(-i);
                    budgetTrendItems.Add(new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy"),
                        Amount = TotalBudget * (decimal)(0.8 + (i * 0.04)) // Simulated growth
                    });
                }
                await BudgetTrendData.ReplaceAllAsync(budgetTrendItems);

                // Load enterprise type distribution
                var enterprises = await _enterpriseRepository.GetAllAsync();
                var typeGroups = enterprises.GroupBy(e => e.Type ?? "Other");

                var enterpriseTypeItems = typeGroups.Select(group => new EnterpriseTypeItem
                {
                    Type = group.Key,
                    Count = group.Count(),
                    AverageRate = group.Any() ? group.Average(e => e.CurrentRate) : 0M
                }).ToList();

                await EnterpriseTypeData.ReplaceAllAsync(enterpriseTypeItems);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading chart data");
            }
        }

        private Task LoadGaugeDataAsync()
        {
            try
            {
                // System Health Score (simplified calculation)
                SystemHealthScore = HealthScore;

                // Budget Utilization Score (simplified calculation)
                var utilizationPercentage = TotalBudget > 0 ? (double)(TotalBudget / 1000000m) * 100 : 0;
                BudgetUtilizationScore = Math.Min(utilizationPercentage, 100);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading gauge data");
            }

            return Task.CompletedTask;
        }

        private async Task LoadActivitiesAsync()
        {
            try
            {
                var activities = new System.Collections.Generic.List<ActivityItem>
                {
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-5),
                        Description = "Enterprise budget updated",
                        Type = "Budget"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-15),
                        Description = "New enterprise added",
                        Type = "Enterprise"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddMinutes(-30),
                        Description = "Report generated",
                        Type = "Report"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-1),
                        Description = "Database backup completed",
                        Type = "System"
                    },
                    new ActivityItem
                    {
                        Timestamp = DateTime.Now.AddHours(-2),
                        Description = "Settings updated",
                        Type = "Configuration"
                    }
                };

                await RecentActivities.ReplaceAllAsync(activities);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading activities");
            }
        }

        private async Task LoadAlertsAsync()
        {
            try
            {
                var alerts = new System.Collections.Generic.List<AlertItem>();

                // Add sample alerts based on system status
                if (TotalEnterprises == 0)
                {
                    alerts.Add(new AlertItem
                    {
                        Priority = "High",
                        Message = "No enterprises configured",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Red
                    });
                }

                if (HealthScore < 75)
                {
                    alerts.Add(new AlertItem
                    {
                        Priority = "Medium",
                        Message = "System health below optimal",
                        Timestamp = DateTime.Now,
                        PriorityColor = Brushes.Orange
                    });
                }

                // Add informational alerts
                alerts.Add(new AlertItem
                {
                    Priority = "Low",
                    Message = "Database backup due soon",
                    Timestamp = DateTime.Now.AddHours(2),
                    PriorityColor = Brushes.Blue
                });

                await SystemAlerts.ReplaceAllAsync(alerts);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading alerts");
            }
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

        [RelayCommand]
        private async Task RefreshDashboardAsync()
        {
            await LoadDashboardDataAsync();
        }

        [RelayCommand]
        internal async Task RefreshDashboardDataAsync()
        {
            await LoadDashboardDataAsync();
        }

        [RelayCommand]
        private void ToggleAutoRefresh()
        {
            AutoRefreshEnabled = !AutoRefreshEnabled;
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

        partial void OnHealthScoreChanged(int value)
        {
            UpdateGaugePointers();
        }

        partial void OnTotalBudgetChanged(decimal value)
        {
            UpdateGaugePointers();
        }

        private void UpdateGaugePointers()
        {
            // Update system health score
            SystemHealthScore = HealthScore;

            // Update budget utilization score
            var utilizationPercentage = TotalBudget > 0 ? (double)(TotalBudget / 1000000m) * 100 : 0;
            BudgetUtilizationScore = Math.Min(utilizationPercentage, 100);
        }
    }

    // Data models for dashboard
    public class BudgetTrendItem
    {
        public required string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class EnterpriseTypeItem
    {
        public required string Type { get; set; }
        public int Count { get; set; }
        public decimal AverageRate { get; set; }
    }

    public class ActivityItem
    {
        public DateTime Timestamp { get; set; }
        public required string Description { get; set; }
        public required string Type { get; set; }
    }

    public class AlertItem
    {
        public string? Priority { get; set; }
        public string? Message { get; set; }
        public DateTime Timestamp { get; set; }
        public Brush? PriorityColor { get; set; }
    }
}