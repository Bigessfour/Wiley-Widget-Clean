using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.Services;
using WileyWidget.Services.Logging;
using WileyWidget.ViewModels.Messages;

namespace WileyWidget.ViewModels
{
    public partial class DashboardViewModel : ObservableObject, IDataErrorInfo
    {
    private readonly ILogger<DashboardViewModel> _logger;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IWhatIfScenarioEngine _whatIfScenarioEngine;
    private readonly IUtilityCustomerRepository _utilityCustomerRepository;
    private readonly IMunicipalAccountRepository _municipalAccountRepository;
    private readonly FiscalYearSettings _fiscalYearSettings;
    private readonly IEventAggregator _eventAggregator;
    private readonly IRegionManager _regionManager;        // KPI Properties
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
        private ObservableCollection<BudgetTrendItem> historicalData = new();

        [ObservableProperty]
        private ObservableCollection<RateTrendItem> rateTrendData = new();

        [ObservableProperty]
        private ObservableCollection<EnterpriseTypeItem> enterpriseTypeData = new();

        // Activity and alerts
        [ObservableProperty]
        private ObservableCollection<ActivityItem> recentActivities = new();

        [ObservableProperty]
        private ObservableCollection<AlertItem> systemAlerts = new();

        // Enterprise data for grids
        [ObservableProperty]
        private ObservableCollection<Enterprise> enterprises = new();

        // Loading and status properties
        [ObservableProperty]
        private bool isLoading = false;

        [ObservableProperty]
        private int systemHealthScore = 95;

        [ObservableProperty]
        private int budgetUtilizationScore = 78;

        [ObservableProperty]
        private decimal suggestedRate;

        [ObservableProperty]
        private string statusMessage = "Ready";

        // Missing properties for view bindings
        [ObservableProperty]
        private string currentTheme = "Light";

        [ObservableProperty]
        private ObservableCollection<BudgetUtilizationData> budgetUtilizationData = new();

        [ObservableProperty]
        private decimal progressPercentage;

        [ObservableProperty]
        private decimal remainingBudget;

        [ObservableProperty]
        private decimal spentAmount;

        // Growth scenario properties
        [ObservableProperty]
        private decimal payRaisePercentage;

        [ObservableProperty]
        private decimal benefitsIncreaseAmount;

        [ObservableProperty]
        private decimal equipmentPurchaseAmount;

        [ObservableProperty]
        private int equipmentFinancingYears = 5;

        [ObservableProperty]
        private decimal reservePercentage;

        [ObservableProperty]
        private ComprehensiveScenario currentScenario;

        [ObservableProperty]
        private bool isScenarioRunning;

        [ObservableProperty]
        private string scenarioStatus;

        // Search and filtering properties
        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Enterprise> filteredEnterprises = new();

        // Error handling
        [ObservableProperty]
        private string errorMessage = string.Empty;

        // IDataErrorInfo implementation for validation
        public string Error => string.Empty;

        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(TotalBudget):
                        if (TotalBudget < 0)
                            return "Total budget cannot be negative";
                        break;
                    case nameof(TotalEnterprises):
                        if (TotalEnterprises < 0)
                            return "Total enterprises cannot be negative";
                        break;
                    case nameof(ActiveProjects):
                        if (ActiveProjects < 0)
                            return "Active projects cannot be negative";
                        break;
                }
                return string.Empty;
            }
        }

    public DashboardViewModel(
        ILogger<DashboardViewModel> logger,
        IEnterpriseRepository enterpriseRepository,
        IWhatIfScenarioEngine whatIfScenarioEngine,
        IUtilityCustomerRepository utilityCustomerRepository,
        IMunicipalAccountRepository municipalAccountRepository,
        FiscalYearSettings fiscalYearSettings,
        IEventAggregator eventAggregator,
        IRegionManager regionManager)
    {
        _logger = logger;
        _enterpriseRepository = enterpriseRepository;
        _whatIfScenarioEngine = whatIfScenarioEngine;
        _utilityCustomerRepository = utilityCustomerRepository;
        _municipalAccountRepository = municipalAccountRepository;
        _fiscalYearSettings = fiscalYearSettings;
        _eventAggregator = eventAggregator;
        _regionManager = regionManager;

        // Subscribe to collection change events for detailed logging
        Enterprises.CollectionChanged += Enterprises_CollectionChanged;
        FilteredEnterprises.CollectionChanged += FilteredEnterprises_CollectionChanged;

        // Subscribe to events
        _eventAggregator.GetEvent<RefreshDataMessage>().Subscribe(OnRefreshDataRequested);
        _eventAggregator.GetEvent<EnterpriseChangedMessage>().Subscribe(OnEnterpriseChanged);
    }        public async Task LoadDashboardDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("LoadDashboard");
            var overallStopwatch = Stopwatch.StartNew();
            var hasErrors = false;
            
            try
            {
                DashboardStatus = "Loading dashboard data...";
                _logger.LogInformation("Starting dashboard data load - CorrelationId: {CorrelationId}", 
                    loggingContext.CorrelationId);

                // Load all dashboard data in parallel
                await Task.WhenAll(
                    LoadKPIsAsync(),
                    LoadEnterprisesAsync(),
                    LoadChartDataAsync(),
                    LoadActivitiesAsync(),
                    LoadAlertsAsync(),
                    LoadScenarioInputsAsync()
                );

                overallStopwatch.Stop();
                DashboardStatus = "Dashboard loaded successfully";
                LastUpdated = DateTime.Now.ToString("g");
                UpdateNextRefreshTime();

                _logger.LogInformation("Dashboard data loaded successfully in {ElapsedMs}ms - {TotalEnterprises} enterprises, ${TotalBudget} total budget - {LogContext}", 
                    overallStopwatch.ElapsedMilliseconds, TotalEnterprises, TotalBudget, loggingContext);
            }
            catch (Exception ex)
            {
                overallStopwatch.Stop();
                hasErrors = true;
                DashboardStatus = "Error loading dashboard";
                _logger.LogError(ex, "CRITICAL: Dashboard data load failed after {ElapsedMs}ms - {Message} - {LogContext}", 
                    overallStopwatch.ElapsedMilliseconds, ex.Message, loggingContext);
                
                // Don't show misleading success message
                MessageBox.Show($"Error loading dashboard: {ex.Message}\n\nPlease check the logs for details.", "Dashboard Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                
                throw; // Propagate exception to prevent misleading success logs
            }
            finally
            {
                if (!hasErrors)
                {
                    _logger.LogDebug("Dashboard load completed without errors in {ElapsedMs}ms - {LogContext}", 
                        overallStopwatch.ElapsedMilliseconds, loggingContext);
                }
            }
        }

        private async Task LoadKPIsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Get enterprise data - await directly, no Task.Run wrapper
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
                
                stopwatch.Stop();
                _logger.LogDebug("KPIs loaded successfully in {ElapsedMs}ms: {TotalEnterprises} enterprises, ${TotalBudget} budget", 
                    stopwatch.ElapsedMilliseconds, TotalEnterprises, TotalBudget);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading KPIs after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
            }
        }

        private async Task LoadEnterprisesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                ErrorMessage = string.Empty;
                Enterprises.Clear();
                
                // Await directly - repository already uses async/await properly
                var enterprises = await _enterpriseRepository.GetAllAsync();
                
                foreach (var enterprise in enterprises)
                {
                    Enterprises.Add(enterprise);
                }
                
                // Initialize filtered collection with all enterprises
                FilteredEnterprises.Clear();
                foreach (var enterprise in Enterprises)
                {
                    FilteredEnterprises.Add(enterprise);
                }
                
                stopwatch.Stop();
                _logger.LogDebug("Loaded {Count} enterprises successfully in {ElapsedMs}ms", 
                    enterprises.Count(), stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                ErrorMessage = $"Failed to load enterprises: {ex.Message}";
                _logger.LogError(ex, "Error loading enterprises after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
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
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Load budget trend data (last 6 months)
                BudgetTrendData.Clear();
                HistoricalData.Clear();
                RateTrendData.Clear();
                
                for (int i = 5; i >= 0; i--)
                {
                    var date = DateTime.Now.AddMonths(-i);
                    var trendItem = new BudgetTrendItem
                    {
                        Period = date.ToString("MMM yyyy"),
                        Amount = TotalBudget * (decimal)(0.8 + (i * 0.04)) // Simulated growth
                    };
                    
                    BudgetTrendData.Add(trendItem);
                    HistoricalData.Add(trendItem); // Also populate HistoricalData for binding

                    // Add rate trend data using the suggested rate
                    var rateItem = new RateTrendItem
                    {
                        Period = date.ToString("MMM yyyy"),
                        Rate = SuggestedRate
                    };
                    RateTrendData.Add(rateItem);
                }

                // Load enterprise type distribution - await directly
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
                
                stopwatch.Stop();
                _logger.LogDebug("Chart data loaded successfully in {ElapsedMs}ms - {BudgetPoints} budget points, {TypeGroups} type groups", 
                    stopwatch.ElapsedMilliseconds, BudgetTrendData.Count, EnterpriseTypeData.Count);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading chart data after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw; // Propagate error to caller
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
            using var loggingContext = LoggingContext.BeginOperation("RefreshDashboard");
            _logger.LogInformation("RefreshDashboard command invoked - IsLoading: {IsLoading} - {LogContext}", 
                IsLoading, loggingContext);
            
            IsLoading = true;
            StatusMessage = "Refreshing dashboard...";
            
            try
            {
                await LoadDashboardDataAsync();
                StatusMessage = "Dashboard refreshed successfully";
                _logger.LogInformation("RefreshDashboard command completed successfully - {LogContext}", loggingContext);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error refreshing dashboard";
                _logger.LogError(ex, "RefreshDashboard command failed: {Message} - {LogContext}", 
                    ex.Message, loggingContext);
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
            _logger.LogInformation("ToggleAutoRefresh command - New state: {AutoRefreshEnabled}, Interval: {RefreshIntervalMinutes} minutes", 
                AutoRefreshEnabled, RefreshIntervalMinutes);
        }

        [RelayCommand]
        private async Task ExportDashboardAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("ExportDashboard");
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("ExportDashboard command invoked - {LogContext}", loggingContext);
            
            try
            {
                IsLoading = true;
                StatusMessage = "Exporting dashboard data...";
                
                // Create export data
                var exportData = new
                {
                    ExportDate = DateTime.Now,
                    DashboardData = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        AutoRefreshEnabled = AutoRefreshEnabled,
                        RefreshIntervalMinutes = RefreshIntervalMinutes,
                        LastUpdated = LastUpdated
                    },
                    Enterprises = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description
                    }).ToList(),
                    HistoricalData = HistoricalData.ToList(),
                    RateTrendData = RateTrendData.ToList(),
                    EnterpriseTypeData = EnterpriseTypeData.ToList()
                };
                
                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export Dashboard Data",
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    FileName = $"WileyWidget_Dashboard_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, json);
                    StatusMessage = $"Dashboard exported to {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Dashboard data exported successfully to {FilePath} - {LogContext}", 
                        saveFileDialog.FileName, loggingContext);
                    
                    MessageBox.Show($"Dashboard data exported successfully to {System.IO.Path.GetFileName(saveFileDialog.FileName)}",
                                  "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "Dashboard export cancelled";
                    _logger.LogInformation("Dashboard export cancelled by user - {LogContext}", loggingContext);
                }
                
                stopwatch.Stop();
                _logger.LogInformation("ExportDashboard completed in {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error exporting dashboard";
                _logger.LogError(ex, "ExportDashboard failed after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error exporting dashboard: {ex.Message}", "Export Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenBudgetAnalysis()
        {
            _logger.LogInformation("OpenBudgetAnalysis command invoked");
            BudgetView.ShowBudgetWindow();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            _logger.LogInformation("OpenSettings command invoked");
            var settingsWindow = new SettingsView
            {
                Owner = Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        }

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("GenerateReport");
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("GenerateReport command invoked - {LogContext}", loggingContext);
            
            try
            {
                IsLoading = true;
                StatusMessage = "Generating dashboard report...";
                
                // Create report data
                var reportData = new
                {
                    ReportTitle = "Wiley Widget Dashboard Report",
                    GeneratedDate = DateTime.Now,
                    ReportPeriod = $"{DateTime.Now.AddDays(-30):yyyy-MM-dd} to {DateTime.Now:yyyy-MM-dd}",
                    Summary = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        EnterpriseChangeText = EnterprisesChangeText,
                        BudgetChangeText = BudgetChangeText
                    },
                    EnterpriseDetails = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description
                    }).ToList(),
                    PerformanceMetrics = new
                    {
                        DataPoints = HistoricalData.Count,
                        RateTrends = RateTrendData.Count,
                        EnterpriseTypes = EnterpriseTypeData.Count
                    },
                    Recommendations = new[]
                    {
                        "Monitor system health score above 80%",
                        "Review budget utilization trends",
                        "Check enterprise performance metrics",
                        "Consider rate adjustments based on scenario analysis"
                    }
                };
                
                // Generate HTML report
                var htmlReport = GenerateHtmlReport(reportData);
                
                // Show save file dialog
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Save Dashboard Report",
                    Filter = "HTML files (*.html)|*.html|All files (*.*)|*.*",
                    DefaultExt = ".html",
                    FileName = $"WileyWidget_Dashboard_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
                };
                
                if (saveFileDialog.ShowDialog() == true)
                {
                    await System.IO.File.WriteAllTextAsync(saveFileDialog.FileName, htmlReport);
                    StatusMessage = $"Report generated: {System.IO.Path.GetFileName(saveFileDialog.FileName)}";
                    _logger.LogInformation("Dashboard report generated successfully to {FilePath} - {LogContext}", 
                        saveFileDialog.FileName, loggingContext);
                    
                    // Ask if user wants to open the report
                    var result = MessageBox.Show($"Report generated successfully. Would you like to open it now?\n\nFile: {System.IO.Path.GetFileName(saveFileDialog.FileName)}",
                                               "Report Generated", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = saveFileDialog.FileName,
                            UseShellExecute = true
                        });
                    }
                }
                else
                {
                    StatusMessage = "Report generation cancelled";
                    _logger.LogInformation("Report generation cancelled by user - {LogContext}", loggingContext);
                }
                
                stopwatch.Stop();
                _logger.LogInformation("GenerateReport completed in {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error generating report";
                _logger.LogError(ex, "GenerateReport failed after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error generating report: {ex.Message}", "Report Generation Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private string GenerateHtmlReport(object reportData)
        {
            // Cast to dynamic for HTML generation
            dynamic data = reportData;
            return $@"
<!DOCTYPE html>
<html>
<head>
    <title>{data.ReportTitle}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 40px; }}
        .header {{ background: #2196F3; color: white; padding: 20px; border-radius: 8px; }}
        .section {{ margin: 20px 0; padding: 15px; border: 1px solid #ddd; border-radius: 5px; }}
        .summary {{ background: #f5f5f5; }}
        .metric {{ display: inline-block; margin: 10px; padding: 10px; background: white; border-radius: 5px; min-width: 150px; }}
        .recommendations {{ background: #e8f5e8; }}
        table {{ width: 100%; border-collapse: collapse; margin: 10px 0; }}
        th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #ddd; }}
        th {{ background: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{data.ReportTitle}</h1>
        <p>Generated: {data.GeneratedDate:yyyy-MM-dd HH:mm:ss}</p>
        <p>Report Period: {data.ReportPeriod}</p>
    </div>
    
    <div class='section summary'>
        <h2>Executive Summary</h2>
        <div class='metric'>
            <strong>Total Enterprises:</strong> {data.Summary.TotalEnterprises}
        </div>
        <div class='metric'>
            <strong>Total Budget:</strong> {data.Summary.TotalBudget:C0}
        </div>
        <div class='metric'>
            <strong>System Health:</strong> {data.Summary.SystemHealthScore}% ({data.Summary.SystemHealthStatus})
        </div>
        <p><strong>Enterprise Changes:</strong> {data.Summary.EnterpriseChangeText}</p>
        <p><strong>Budget Changes:</strong> {data.Summary.BudgetChangeText}</p>
    </div>
    
    <div class='section'>
        <h2>Enterprise Details</h2>
        <table>
            <thead>
                <tr>
                    <th>ID</th>
                    <th>Name</th>
                    <th>Type</th>
                    <th>Description</th>
                </tr>
            </thead>
            <tbody>
                {GenerateEnterpriseTableRows(data.EnterpriseDetails)}
            </tbody>
        </table>
    </div>
    
    <div class='section'>
        <h2>Performance Metrics</h2>
        <p>Data Points: {data.PerformanceMetrics.DataPoints}</p>
        <p>Rate Trends: {data.PerformanceMetrics.RateTrends}</p>
        <p>Enterprise Types: {data.PerformanceMetrics.EnterpriseTypes}</p>
    </div>
    
    <div class='section recommendations'>
        <h2>Recommendations</h2>
        <ul>
            {GenerateRecommendationsList(data.Recommendations)}
        </ul>
    </div>
</body>
</html>";
        }
        
        private string GenerateEnterpriseTableRows(dynamic enterpriseDetails)
        {
            var rows = new System.Collections.Generic.List<string>();
            foreach (dynamic enterprise in enterpriseDetails)
            {
                rows.Add($"<tr><td>{enterprise.Id}</td><td>{enterprise.Name}</td><td>{enterprise.Type}</td><td>{enterprise.Description}</td></tr>");
            }
            return string.Join("", rows);
        }
        
        private string GenerateRecommendationsList(dynamic recommendations)
        {
            var items = new System.Collections.Generic.List<string>();
            foreach (string recommendation in recommendations)
            {
                items.Add($"<li>{recommendation}</li>");
            }
            return string.Join("", items);
        }

        [RelayCommand]
        private async Task BackupDataAsync()
        {
            using var loggingContext = LoggingContext.BeginOperation("BackupData");
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("BackupData command invoked - {LogContext}", loggingContext);
            
            try
            {
                IsLoading = true;
                StatusMessage = "Creating data backup...";
                
                // Create backup directory if it doesn't exist
                var backupDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "WileyWidget",
                    "Backups"
                );
                
                System.IO.Directory.CreateDirectory(backupDir);
                
                // Create backup filename with timestamp
                var backupFileName = $"WileyWidget_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var backupFilePath = System.IO.Path.Combine(backupDir, backupFileName);
                
                // Create backup data
                var backupData = new
                {
                    BackupDate = DateTime.Now,
                    Version = "1.0",
                    Application = "Wiley Widget",
                    DashboardData = new
                    {
                        TotalEnterprises = TotalEnterprises,
                        TotalBudget = TotalBudget,
                        SystemHealthScore = HealthScore,
                        SystemHealthStatus = SystemHealthStatus,
                        AutoRefreshEnabled = AutoRefreshEnabled,
                        RefreshIntervalMinutes = RefreshIntervalMinutes,
                        LastUpdated = LastUpdated
                    },
                    Enterprises = Enterprises.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.Type,
                        e.Description,
                        e.CreatedDate,
                        e.ModifiedDate
                    }).ToList(),
                    HistoricalData = HistoricalData.ToList(),
                    RateTrendData = RateTrendData.ToList(),
                    EnterpriseTypeData = EnterpriseTypeData.ToList(),
                    Settings = new
                    {
                        FiscalYearSettings = _fiscalYearSettings,
                        ApplicationSettings = "Default"
                    }
                };
                
                // Serialize to JSON
                var json = System.Text.Json.JsonSerializer.Serialize(backupData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                // Write backup file
                await System.IO.File.WriteAllTextAsync(backupFilePath, json);
                
                // Create a compressed archive if possible
                var zipFilePath = System.IO.Path.Combine(backupDir, $"WileyWidget_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                try
                {
                    using (var archive = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        archive.CreateEntryFromFile(backupFilePath, backupFileName);
                    }
                    
                    // Delete the uncompressed file
                    System.IO.File.Delete(backupFilePath);
                    backupFilePath = zipFilePath;
                    backupFileName = System.IO.Path.GetFileName(zipFilePath);
                }
                catch
                {
                    // If compression fails, keep the JSON file
                    _logger.LogWarning("Could not create compressed backup, keeping JSON file");
                }
                
                StatusMessage = $"Backup created: {backupFileName}";
                _logger.LogInformation("Data backup created successfully at {FilePath} - {LogContext}", 
                    backupFilePath, loggingContext);
                
                MessageBox.Show($"Data backup created successfully!\n\nFile: {backupFileName}\nLocation: {backupDir}",
                              "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                
                stopwatch.Stop();
                _logger.LogInformation("BackupData completed in {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                StatusMessage = "Error creating backup";
                _logger.LogError(ex, "BackupData failed after {ElapsedMs}ms - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, loggingContext);
                MessageBox.Show($"Error creating backup: {ex.Message}", "Backup Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void Search()
        {
            _logger.LogDebug("Search command invoked - SearchText: '{SearchText}'", SearchText);
            FilterEnterprises();
        }

        private void FilterEnterprises()
        {
            FilteredEnterprises.Clear();
            
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                // No search text, show all enterprises
                foreach (var enterprise in Enterprises)
                {
                    FilteredEnterprises.Add(enterprise);
                }
            }
            else
            {
                // Filter enterprises based on search text
                var filtered = Enterprises.Where(e =>
                    (e.Name?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Type?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
                
                foreach (var enterprise in filtered)
                {
                    FilteredEnterprises.Add(enterprise);
                }
            }
        }

        private async Task LoadScenarioInputsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Initialize default values for growth scenario inputs
                PayRaisePercentage = 3.0m;     // 3% default pay raise
                BenefitsIncreaseAmount = 50m;  // $50/month benefits increase
                EquipmentPurchaseAmount = 0m;  // No equipment by default
                EquipmentFinancingYears = 5;   // 5-year financing
                ReservePercentage = 5.0m;      // 5% reserve increase

                // Calculate initial suggested rate
                var enterpriseId = await GetCurrentEnterpriseIdAsync();
                if (enterpriseId > 0)
                {
                    SuggestedRate = await CalculateSuggestedRateAsync(enterpriseId);
                }

                stopwatch.Stop();
                ScenarioStatus = "Scenario inputs loaded";
                _logger.LogDebug("Scenario inputs loaded in {ElapsedMs}ms - EnterpriseId: {EnterpriseId}, SuggestedRate: ${SuggestedRate}", 
                    stopwatch.ElapsedMilliseconds, enterpriseId, SuggestedRate);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Error loading scenario inputs after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                ScenarioStatus = "Error loading scenario inputs";
            }
        }

        private async Task<int> GetCurrentEnterpriseIdAsync()
        {
            try
            {
                // Get the first enterprise as current (you may want to implement proper selection logic)
                var enterprises = await Task.Run(() => _enterpriseRepository.GetAllAsync());
                return enterprises.FirstOrDefault()?.Id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task<decimal> CalculateSuggestedRateAsync(int enterpriseId)
        {
            try
            {
                // Get customer count
                var customerCount = await _utilityCustomerRepository.GetCountAsync();
                if (customerCount == 0) return 0;

                // Get enterprise to determine fund type
                var enterprise = await Task.Run(() => _enterpriseRepository.GetByIdAsync(enterpriseId));
                if (enterprise == null) return 0;

                // Map enterprise type to fund type (same logic as WhatIfScenarioEngine)
                var fundType = enterprise.Type switch
                {
                    "Water" => MunicipalFundType.Water,
                    "Sewer" => MunicipalFundType.Sewer,
                    "Trash" => MunicipalFundType.Trash,
                    _ => MunicipalFundType.Enterprise
                };

                // Get expense accounts for this fund
                var expenseAccounts = await _municipalAccountRepository.GetByFundAsync(fundType);

                // Calculate aggregated expenses (sum of balances from expense accounts)
                var aggregatedExpenses = expenseAccounts.Sum(account => account.Balance);

                // Add growth buffer (10% of expenses)
                var growthBuffer = aggregatedExpenses * 0.10m;

                // Calculate suggested rate
                var suggestedRate = (aggregatedExpenses + growthBuffer) / customerCount;

                return Math.Round(suggestedRate, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating suggested rate");
                return 0;
            }
        }

        [RelayCommand]
        private async Task RunGrowthScenarioAsync(int enterpriseId)
        {
            using var loggingContext = LoggingContext.BeginOperation("RunGrowthScenario");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                IsScenarioRunning = true;
                ScenarioStatus = "Running growth scenario analysis...";
                
                _logger.LogInformation("Starting growth scenario for EnterpriseId: {EnterpriseId} - {LogContext}", 
                    enterpriseId, loggingContext);

                // Create scenario parameters from user inputs
                var parameters = new ScenarioParameters
                {
                    PayRaisePercentage = PayRaisePercentage / 100m, // Convert percentage to decimal
                    BenefitsIncreaseAmount = BenefitsIncreaseAmount,
                    EquipmentPurchaseAmount = EquipmentPurchaseAmount,
                    EquipmentFinancingYears = EquipmentFinancingYears,
                    ReservePercentage = ReservePercentage / 100m // Convert percentage to decimal
                };

                // Generate comprehensive scenario
                var scenario = await _whatIfScenarioEngine.GenerateComprehensiveScenarioAsync(enterpriseId, parameters);

                // Store the scenario results
                CurrentScenario = scenario;

                // Recalculate suggested rate with new scenario data
                SuggestedRate = await CalculateSuggestedRateAsync(enterpriseId);

                stopwatch.Stop();
                ScenarioStatus = $"Scenario '{scenario.ScenarioName}' completed successfully";

                _logger.LogInformation("Growth scenario completed in {ElapsedMs}ms for enterprise {EnterpriseId} - New Rate: ${SuggestedRate} - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, enterpriseId, SuggestedRate, loggingContext);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                ScenarioStatus = $"Error running scenario: {ex.Message}";
                _logger.LogError(ex, "Error running growth scenario after {ElapsedMs}ms for enterprise {EnterpriseId} - {LogContext}", 
                    stopwatch.ElapsedMilliseconds, enterpriseId, loggingContext);
            }
            finally
            {
                IsScenarioRunning = false;
            }
        }

        partial void OnAutoRefreshEnabledChanged(bool value)
        {
            _logger.LogDebug("AutoRefreshEnabled changed to: {Value}", value);
            UpdateNextRefreshTime();
        }

        partial void OnRefreshIntervalMinutesChanged(int value)
        {
            _logger.LogDebug("RefreshIntervalMinutes changed to: {Value}", value);
            UpdateNextRefreshTime();
        }

        partial void OnSearchTextChanged(string value)
        {
            _logger.LogTrace("SearchText changed to: '{Value}'", value);
            FilterEnterprises();
        }
        
        partial void OnTotalEnterprisesChanged(int value)
        {
            _logger.LogInformation("TotalEnterprises changed to: {Value}", value);
        }
        
        partial void OnTotalBudgetChanged(decimal value)
        {
            _logger.LogInformation("TotalBudget changed to: ${Value:N2}", value);
        }
        
        partial void OnSystemHealthStatusChanged(string value)
        {
            _logger.LogInformation("SystemHealthStatus changed to: {Value}, HealthScore: {HealthScore}", value, HealthScore);
        }
        
        partial void OnHealthScoreChanged(int value)
        {
            _logger.LogDebug("HealthScore changed to: {Value}", value);
        }
        
        partial void OnActiveProjectsChanged(int value)
        {
            _logger.LogDebug("ActiveProjects changed to: {Value}", value);
        }
        
        partial void OnIsLoadingChanged(bool value)
        {
            _logger.LogTrace("IsLoading changed to: {Value}", value);
        }
        
        partial void OnDashboardStatusChanged(string value)
        {
            _logger.LogDebug("DashboardStatus changed to: '{Value}'", value);
        }
        
        partial void OnSuggestedRateChanged(decimal value)
        {
            _logger.LogInformation("SuggestedRate changed to: ${Value:N2}", value);
        }
        
        partial void OnIsScenarioRunningChanged(bool value)
        {
            _logger.LogDebug("IsScenarioRunning changed to: {Value}", value);
        }

        [RelayCommand]
        private void ClearSearch()
        {
            _logger.LogDebug("ClearSearch command invoked - Previous SearchText: '{SearchText}'", SearchText);
            SearchText = string.Empty;
        }

        // Navigation commands for testing journaling
        [RelayCommand]
        private void NavigateToAccounts()
        {
            _logger.LogInformation("NavigateToAccounts command invoked - Navigating to MunicipalAccountView");
            _regionManager.RequestNavigate("MainRegion", "MunicipalAccountView");
        }

        [RelayCommand]
        private void NavigateBack()
        {
            var region = _regionManager.Regions["MainRegion"];
            var canGoBack = region.NavigationService.Journal.CanGoBack;
            _logger.LogDebug("NavigateBack command invoked - CanGoBack: {CanGoBack}", canGoBack);
            
            if (canGoBack)
            {
                region.NavigationService.Journal.GoBack();
            }
        }

        [RelayCommand]
        private void NavigateForward()
        {
            var region = _regionManager.Regions["MainRegion"];
            var canGoForward = region.NavigationService.Journal.CanGoForward;
            _logger.LogDebug("NavigateForward command invoked - CanGoForward: {CanGoForward}", canGoForward);
            
            if (canGoForward)
            {
                region.NavigationService.Journal.GoForward();
            }
        }

        [RelayCommand]
        private void OpenEnterpriseManagement()
        {
            _logger.LogInformation("OpenEnterpriseManagement command invoked");
            try
            {
                // Navigate to enterprise management view
                _regionManager.RequestNavigate("EnterpriseRegion", "EnterpriseView");
                _logger.LogInformation("Successfully navigated to Enterprise management view");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open Enterprise management view");
                MessageBox.Show($"Error opening Enterprise management: {ex.Message}", "Navigation Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event Handlers for EventAggregator
        private void OnRefreshDataRequested(RefreshDataMessage message)
        {
            _logger.LogInformation("Refresh data requested for view: {ViewName}", message.ViewName);
            
            if (string.IsNullOrEmpty(message.ViewName) || message.ViewName == "Dashboard")
            {
                _ = LoadDashboardDataAsync();
            }
        }

        private void OnEnterpriseChanged(EnterpriseChangedMessage message)
        {
            _logger.LogInformation("Enterprise changed: {EnterpriseName} ({ChangeType})", 
                message.EnterpriseName, message.ChangeType);
            
            // Refresh dashboard data when enterprise changes
            _ = LoadDashboardDataAsync();
        }

        // Collection change event handlers
        private void Enterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _logger.LogDebug("Enterprises collection - Added {Count} items at index {Index}", 
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    if (e.NewItems != null)
                    {
                        foreach (Enterprise enterprise in e.NewItems)
                        {
                            _logger.LogTrace("Added Enterprise: Id={Id}, Name={Name}, Type={Type}", 
                                enterprise.Id, enterprise.Name, enterprise.Type);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    _logger.LogDebug("Enterprises collection - Removed {Count} items at index {Index}", 
                        e.OldItems?.Count ?? 0, e.OldStartingIndex);
                    if (e.OldItems != null)
                    {
                        foreach (Enterprise enterprise in e.OldItems)
                        {
                            _logger.LogTrace("Removed Enterprise: Id={Id}, Name={Name}", 
                                enterprise.Id, enterprise.Name);
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    _logger.LogDebug("Enterprises collection - Replaced {Count} items at index {Index}", 
                        e.NewItems?.Count ?? 0, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _logger.LogDebug("Enterprises collection - Reset (cleared or major change)");
                    break;

                case NotifyCollectionChangedAction.Move:
                    _logger.LogDebug("Enterprises collection - Moved item from index {OldIndex} to {NewIndex}", 
                        e.OldStartingIndex, e.NewStartingIndex);
                    break;
            }

            _logger.LogDebug("Enterprises collection now has {Count} items", Enterprises.Count);
        }

        private void FilteredEnterprises_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    _logger.LogTrace("FilteredEnterprises - Added {Count} items", e.NewItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    _logger.LogTrace("FilteredEnterprises - Removed {Count} items", e.OldItems?.Count ?? 0);
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _logger.LogTrace("FilteredEnterprises - Reset (filter applied)");
                    break;
            }

            _logger.LogTrace("FilteredEnterprises now has {Count} items", FilteredEnterprises.Count);
        }

        // Prism Navigation Implementation
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _logger.LogInformation("DashboardViewModel navigated to with context: {Context}", navigationContext?.ToString() ?? "null");
            
            // Handle navigation parameters
            if (navigationContext?.Parameters != null)
            {
                // Check for refresh parameter
                if (navigationContext.Parameters.ContainsKey("refresh"))
                {
                    var refresh = navigationContext.Parameters["refresh"];
                    if (refresh is bool refreshBool && refreshBool)
                    {
                        _ = LoadDashboardDataAsync();
                    }
                }

                // Check for filter parameter
                if (navigationContext.Parameters.ContainsKey("filter"))
                {
                    var filter = navigationContext.Parameters["filter"];
                    if (filter is string filterString)
                    {
                        // SearchText = filterString; // Will be available once properties are generated
                        _logger.LogInformation("Filter parameter received: {Filter}", filterString);
                    }
                }
            }
            else
            {
                // Default behavior - load dashboard data
                _ = LoadDashboardDataAsync();
            }
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _logger.LogInformation("DashboardViewModel navigated from");
            
            // Cleanup if needed
            // Cancel any ongoing operations, save state, etc.
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // Always allow navigation to dashboard
            return true;
        }
    }

    // Data models for dashboard
    public class BudgetTrendItem
    {
        public string Period { get; set; }
        public decimal Amount { get; set; }
    }

    public class RateTrendItem
    {
        public string Period { get; set; }
        public decimal Rate { get; set; }
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

    public class BudgetUtilizationData
    {
        public string Category { get; set; }
        public decimal Budgeted { get; set; }
        public decimal Actual { get; set; }
        public decimal UtilizationPercent { get; set; }
    }
}