using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using WileyWidget.ViewModels.Messages;
using System.Threading.Tasks;
using System.Linq;
using Serilog;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for budget analysis and reporting
/// Provides comprehensive budget insights and financial analysis
/// Implements messaging, busy states, input validation, and IDataErrorInfo
/// </summary>
public partial class BudgetViewModel : ObservableObject, IDisposable, IDataErrorInfo
{
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly DispatcherTimer _refreshTimer;
    private bool _disposed;

    /// <summary>
    /// Collection of budget details for each enterprise
    /// </summary>
    public ObservableCollection<BudgetDetailItem> BudgetDetails { get; } = new();

    /// <summary>
    /// Total revenue across all enterprises
    /// </summary>
    [ObservableProperty]
    private decimal totalRevenue;

    /// <summary>
    /// Total expenses across all enterprises
    /// </summary>
    [ObservableProperty]
    private decimal totalExpenses;

    /// <summary>
    /// Net balance (revenue - expenses)
    /// </summary>
    [ObservableProperty]
    private decimal netBalance;

    /// <summary>
    /// Total citizens served across all enterprises
    /// </summary>
    [ObservableProperty]
    private int totalCitizens;

    /// <summary>
    /// Break-even analysis text
    /// </summary>
    [ObservableProperty]
    private string breakEvenAnalysisText = "Click 'Break-even Analysis' to generate insights";

    /// <summary>
    /// Trend analysis text
    /// </summary>
    [ObservableProperty]
    private string trendAnalysisText = "Click 'Trend Analysis' to view budget trends";

    /// <summary>
    /// Recommendations text
    /// </summary>
    [ObservableProperty]
    private string recommendationsText = "Click 'Refresh' to load budget data and generate recommendations";

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [ObservableProperty]
    private string lastUpdated = "Never";

    /// <summary>
    /// Analysis status
    /// </summary>
    [ObservableProperty]
    private string analysisStatus = "Ready";

    /// <summary>
    /// Whether there's an error
    /// </summary>
    [ObservableProperty]
    private bool hasError;

    /// <summary>
    /// Error message if any
    /// </summary>
    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>
    /// Busy state for long-running operations
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Progress text for user feedback during operations
    /// </summary>
    [ObservableProperty]
    private string progressText = string.Empty;

    /// <summary>
    /// Budget items collection
    /// </summary>
    public ObservableCollection<BudgetDetailItem> BudgetItems { get; } = new();

    /// <summary>
    /// Budget performance data
    /// </summary>
    public ObservableCollection<BudgetPerformanceData> BudgetPerformanceData { get; } = new();

    /// <summary>
    /// Budget variance data
    /// </summary>
    [ObservableProperty]
    private decimal budgetVariance;

    /// <summary>
    /// Loading state
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Net income
    /// </summary>
    [ObservableProperty]
    private decimal netIncome;

    /// <summary>
    /// Projected rate data
    /// </summary>
    public ObservableCollection<ProjectedRateData> ProjectedRateData { get; } = new();

    /// <summary>
    /// Rate trend data
    /// </summary>
    public ObservableCollection<RateTrendData> RateTrendData { get; } = new();

    /// <summary>
    /// Hierarchical collection of budget accounts with parent-child relationships
    /// </summary>
    public ObservableCollection<BudgetAccount> BudgetAccounts { get; } = new();

    /// <summary>
    /// Collection of available fund types for dropdown editors
    /// </summary>
    public ObservableCollection<BudgetFundType> FundTypes { get; } = BudgetFundType.GetStandardFundTypes();

    /// <summary>
    /// Collection of fiscal years for selection
    /// </summary>
    public ObservableCollection<string> FiscalYears { get; } = new()
    {
        "FY 2023", "FY 2024", "FY 2025", "FY 2026"
    };

    /// <summary>
    /// Currently selected fiscal year
    /// </summary>
    [ObservableProperty]
    private string selectedFiscalYear = "FY 2025";

    /// <summary>
    /// Total budgeted amount across all accounts
    /// </summary>
    [ObservableProperty]
    private decimal totalBudget;

    /// <summary>
    /// Total actual expenses across all accounts
    /// </summary>
    [ObservableProperty]
    private decimal totalActual;

    /// <summary>
    /// Total variance (Budget - Actual)
    /// </summary>
    [ObservableProperty]
    private decimal totalVariance;

    /// <summary>
    /// Budget distribution data for pie chart
    /// </summary>
    public ObservableCollection<BudgetDistributionData> BudgetDistributionData { get; } = new();

    /// <summary>
    /// Budget comparison data for bar chart
    /// </summary>
    public ObservableCollection<BudgetComparisonData> BudgetComparisonData { get; } = new();

    /// <summary>
    /// Foreground color (for UI binding)
    /// </summary>
    [ObservableProperty]
    private string foreground = "#000000";

    /// <summary>
    /// Whether budget is over budget
    /// </summary>
    [ObservableProperty]
    private bool isOverBudget;

    /// <summary>
    /// Percentage value
    /// </summary>
    [ObservableProperty]
    private decimal percentage;

    /// <summary>
    /// Self-reference for DataContext binding
    /// </summary>
    public BudgetViewModel ViewModel => this;

    /// <summary>
    /// Constructor with dependency injection
    /// Subscribes to enterprise change messages for automatic refresh
    /// </summary>
    public BudgetViewModel(IEnterpriseRepository enterpriseRepository, IBudgetRepository budgetRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _budgetRepository = budgetRepository ?? throw new ArgumentNullException(nameof(budgetRepository));

        // Initialize live update timer (refresh every 5 minutes)
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(5)
        };
        _refreshTimer.Tick += async (s, e) => await RefreshBudgetsAsync();

        // Subscribe to enterprise change messages
        WeakReferenceMessenger.Default.Register<EnterpriseChangedMessage>(this, (recipient, message) =>
        {
            // Automatically refresh budget data when enterprises change
            Log.Information("Received EnterpriseChangedMessage: {EnterpriseName} ({ChangeType})", 
                message.EnterpriseName, message.ChangeType);
            _ = RefreshBudgetDataAsync();
        });
    }

    /// <summary>
    /// Starts the live update timer
    /// </summary>
    public void StartLiveUpdates()
    {
        if (!_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
            Log.Information("Started budget live updates with 5-minute interval");
        }
    }

    /// <summary>
    /// Stops the live update timer
    /// </summary>
    public void StopLiveUpdates()
    {
        if (_refreshTimer.IsEnabled)
        {
            _refreshTimer.Stop();
            Log.Information("Stopped budget live updates");
        }
    }

    /// <summary>
    /// Async load budgets for selected fiscal year using Task.Run
    /// </summary>
    private async Task LoadBudgetsAsync()
    {
        try
        {
            IsBusy = true;
            ProgressText = $"Loading budgets for {SelectedFiscalYear}...";

            // Extract year from "FY 2025" format
            var yearStr = SelectedFiscalYear.Replace("FY", "").Trim();
            if (!int.TryParse(yearStr, out var fiscalYear))
            {
                throw new InvalidOperationException($"Invalid fiscal year format: {SelectedFiscalYear}");
            }

            // Use Task.Run for async data loading to avoid UI thread blocking
            var budgets = await Task.Run(() => _budgetRepository.GetBudgetHierarchyAsync(fiscalYear));

            // Update UI on dispatcher thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                BudgetAccounts.Clear();
                foreach (var budget in budgets)
                {
                    // Convert BudgetEntry to BudgetAccount
                    var account = new BudgetAccount
                    {
                        Id = budget.Id,
                        AccountNumber = budget.AccountNumber,
                        Description = budget.Description,
                        FundType = budget.FundType.ToString(),
                        BudgetAmount = budget.BudgetedAmount,
                        ActualAmount = budget.ActualAmount,
                        ParentId = budget.ParentId ?? -1
                    };
                    BudgetAccounts.Add(account);
                }

                UpdateTotalsAndCharts();
            });

            ProgressText = $"Loaded {BudgetAccounts.Count} budget accounts";
            Log.Information("Successfully loaded {Count} budget accounts for {Year}", BudgetAccounts.Count, fiscalYear);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load budgets: {ex.Message}";
            HasError = true;
            AnalysisStatus = $"Error: {ex.Message}";
            Log.Error(ex, "Failed to load budgets for {Year}", SelectedFiscalYear);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Refreshes budget data (called by timer)
    /// </summary>
    private async Task RefreshBudgetsAsync()
    {
        await LoadBudgetsAsync();
    }

    /// <summary>
    /// Toggle fiscal year command
    /// </summary>
    [RelayCommand]
    private async Task ToggleFiscalYearAsync()
    {
        // Find next year in the list
        var currentIndex = FiscalYears.IndexOf(SelectedFiscalYear);
        var nextIndex = (currentIndex + 1) % FiscalYears.Count;
        SelectedFiscalYear = FiscalYears[nextIndex];

        await LoadBudgetsAsync();
        Log.Information("Toggled fiscal year to {Year}", SelectedFiscalYear);
    }

    /// <summary>
    /// Save confirmation command with MessageBox
    /// </summary>
    [RelayCommand]
    private async Task SaveConfirmationAsync()
    {
        if (BudgetAccounts.Any(a => a.IsOverBudget))
        {
            var result = MessageBox.Show(
                "Some accounts are over budget. Are you sure you want to save?",
                "Budget Overrun Warning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                Log.Information("User canceled save due to budget overruns");
                return;
            }
        }

        try
        {
            IsBusy = true;
            ProgressText = "Saving budget changes...";

            // Save logic here (update repository)
            await Task.Run(async () =>
            {
                foreach (var account in BudgetAccounts)
                {
                    // Convert back to BudgetEntry and update
                    var entry = new BudgetEntry
                    {
                        Id = account.Id,
                        AccountNumber = account.AccountNumber,
                        Description = account.Description,
                        FundType = Enum.TryParse<WileyWidget.Models.Entities.FundType>(account.FundType, out var fundType) 
                            ? fundType 
                            : WileyWidget.Models.Entities.FundType.GeneralFund,
                        BudgetedAmount = account.BudgetAmount,
                        ActualAmount = account.ActualAmount,
                        ParentId = account.ParentId == -1 ? null : account.ParentId
                    };
                    await _budgetRepository.UpdateAsync(entry);
                }
            });

            MessageBox.Show("Budget saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ProgressText = "Budget saved successfully";
            Log.Information("Budget saved successfully");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save budget: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ErrorMessage = $"Failed to save: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to save budget");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigate to Municipal Account View
    /// </summary>
    [RelayCommand]
    private void NavigateToMunicipalAccount()
    {
        // Send navigation message to MainViewModel
        WeakReferenceMessenger.Default.Send(new NavigationMessage
        {
            TargetView = "MunicipalAccountView"
        });
        Log.Information("Navigating to MunicipalAccountView");
    }

    /// <summary>
    /// Updates totals and chart data
    /// </summary>
    private void UpdateTotalsAndCharts()
    {
        TotalBudget = BudgetAccounts.Sum(a => a.BudgetAmount);
        TotalActual = BudgetAccounts.Sum(a => a.ActualAmount);
        TotalVariance = TotalBudget - TotalActual;

        // Update distribution data
        BudgetDistributionData.Clear();
        var fundGroups = BudgetAccounts.GroupBy(a => a.FundType);
        foreach (var group in fundGroups)
        {
            var amount = group.Sum(a => a.BudgetAmount);
            BudgetDistributionData.Add(new BudgetDistributionData
            {
                FundType = group.Key,
                Amount = amount,
                Percentage = TotalBudget > 0 ? (double)(amount / TotalBudget) : 0
            });
        }

        // Update comparison data
        BudgetComparisonData.Clear();
        foreach (var group in fundGroups)
        {
            BudgetComparisonData.Add(new BudgetComparisonData
            {
                Category = group.Key,
                BudgetAmount = group.Sum(a => a.BudgetAmount),
                ActualAmount = group.Sum(a => a.ActualAmount)
            });
        }
    }

    #region IDataErrorInfo Implementation

    /// <summary>
    /// Gets the error message for the entire object
    /// </summary>
    public string Error
    {
        get
        {
            if (BudgetAccounts.Any(a => a.IsOverBudget))
            {
                return "One or more accounts are over budget";
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the error message for a specific property
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            switch (columnName)
            {
                case nameof(TotalBudget):
                    if (TotalBudget <= 0)
                        return "Total budget must be greater than zero";
                    break;
                case nameof(TotalActual):
                    if (TotalActual > TotalBudget)
                        return "Total actual expenses exceed total budget";
                    break;
            }
            return string.Empty;
        }
    }

    #endregion

    /// <summary>
    /// Constructor with dependency injection (original signature for backward compatibility)
    /// </summary>
    public BudgetViewModel(IEnterpriseRepository enterpriseRepository) 
        : this(enterpriseRepository, null!)
    {
        // Fallback constructor - budget repository will be null
        // This maintains compatibility with existing tests
        Log.Warning("BudgetViewModel created without IBudgetRepository - some features will be unavailable");
    }

    /// <summary>
    /// Refreshes all budget data from the database
    /// Includes busy state management and error handling
    /// </summary>
    [RelayCommand]
    public async Task RefreshBudgetDataAsync()
    {
        if (IsBusy) return; // Prevent concurrent refreshes

        try
        {
            IsBusy = true;
            ProgressText = "Loading budget data...";
            AnalysisStatus = "Loading...";
            HasError = false;
            ErrorMessage = string.Empty;

            var enterprises = await _enterpriseRepository.GetAllAsync();

            BudgetDetails.Clear();

            foreach (var enterprise in enterprises)
            {
                var budgetDetail = new BudgetDetailItem
                {
                    EnterpriseName = enterprise.Name,
                    CitizenCount = enterprise.CitizenCount,
                    CurrentRate = enterprise.CurrentRate,
                    MonthlyRevenue = enterprise.MonthlyRevenue,
                    MonthlyExpenses = enterprise.MonthlyExpenses,
                    MonthlyBalance = enterprise.MonthlyBalance,
                    BreakEvenRate = enterprise.BreakEvenRate,
                    Status = enterprise.MonthlyBalance >= 0 ? "Surplus" : "Deficit"
                };

                BudgetDetails.Add(budgetDetail);
            }

            // Calculate totals
            TotalRevenue = BudgetDetails.Sum(b => b.MonthlyRevenue);
            TotalExpenses = BudgetDetails.Sum(b => b.MonthlyExpenses);
            NetBalance = TotalRevenue - TotalExpenses;
            TotalCitizens = BudgetDetails.Sum(b => b.CitizenCount);

            LastUpdated = DateTime.Now.ToString("g");
            ProgressText = "Data loaded successfully";
            AnalysisStatus = "Data loaded successfully";

            // Generate initial recommendations
            GenerateRecommendations();

            Log.Information("Successfully refreshed budget data for {Count} enterprises", enterprises.Count());

            // Send refresh complete message
            WeakReferenceMessenger.Default.Send(new BudgetUpdatedMessage
            {
                Context = "BudgetViewModel.RefreshBudgetDataAsync"
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh budget data: {ex.Message}";
            HasError = true;
            // Tests expect the status to begin with "Error:" and include the exception message
            AnalysisStatus = $"Error: {ex.Message}";
            ProgressText = "Error loading data";
            Log.Error(ex, "Failed to refresh budget data");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Performs break-even analysis
    /// </summary>
    [RelayCommand]
    private void BreakEvenAnalysis()
    {
        if (!BudgetDetails.Any())
        {
            BreakEvenAnalysisText = "No budget data available. Please refresh data first.";
            return;
        }

        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("BREAK-EVEN ANALYSIS");
        analysis.AppendLine("===================");
        analysis.AppendLine();

        foreach (var detail in BudgetDetails.OrderByDescending(b => b.MonthlyBalance))
        {
            analysis.AppendLine($"Enterprise: {detail.EnterpriseName}");
            analysis.AppendLine($"  Current Rate: ${detail.CurrentRate:F2}");
            analysis.AppendLine($"  Break-even Rate: ${detail.BreakEvenRate:F2}");
            analysis.AppendLine($"  Current Balance: ${detail.MonthlyBalance:F2}");

            if (detail.CurrentRate > detail.BreakEvenRate)
            {
                analysis.AppendLine($"  Status: PROFITABLE (Rate exceeds break-even by ${(detail.CurrentRate - detail.BreakEvenRate):F2})");
            }
            else if (detail.CurrentRate < detail.BreakEvenRate)
            {
                analysis.AppendLine($"  Status: LOSS (Need ${(detail.BreakEvenRate - detail.CurrentRate):F2} increase to break-even)");
            }
            else
            {
                analysis.AppendLine("  Status: AT BREAK-EVEN");
            }
            analysis.AppendLine();
        }

        BreakEvenAnalysisText = analysis.ToString();
    }

    /// <summary>
    /// Performs trend analysis
    /// </summary>
    [RelayCommand]
    private void TrendAnalysis()
    {
        if (!BudgetDetails.Any())
        {
            TrendAnalysisText = "No budget data available. Please refresh data first.";
            return;
        }

        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("BUDGET TREND ANALYSIS");
        analysis.AppendLine("====================");
        analysis.AppendLine();

        var profitableEnterprises = BudgetDetails.Count(b => b.MonthlyBalance > 0);
        var deficitEnterprises = BudgetDetails.Count(b => b.MonthlyBalance < 0);
        var breakEvenEnterprises = BudgetDetails.Count(b => b.MonthlyBalance == 0);

        analysis.AppendLine($"Portfolio Overview:");
        analysis.AppendLine($"  Profitable Enterprises: {profitableEnterprises}");
        analysis.AppendLine($"  Deficit Enterprises: {deficitEnterprises}");
        analysis.AppendLine($"  Break-even Enterprises: {breakEvenEnterprises}");
        analysis.AppendLine();

        analysis.AppendLine($"Revenue Distribution:");
        var avgRevenue = BudgetDetails.Average(b => b.MonthlyRevenue);
        var maxRevenue = BudgetDetails.Max(b => b.MonthlyRevenue);
        var minRevenue = BudgetDetails.Min(b => b.MonthlyRevenue);

        analysis.AppendLine($"  Average Revenue: ${avgRevenue:F2}");
        analysis.AppendLine($"  Highest Revenue: ${maxRevenue:F2}");
        analysis.AppendLine($"  Lowest Revenue: ${minRevenue:F2}");
        analysis.AppendLine();

        analysis.AppendLine($"Expense Analysis:");
        var avgExpense = BudgetDetails.Average(b => b.MonthlyExpenses);
        var maxExpense = BudgetDetails.Max(b => b.MonthlyExpenses);
        var minExpense = BudgetDetails.Min(b => b.MonthlyExpenses);

        analysis.AppendLine($"  Average Expenses: ${avgExpense:F2}");
        analysis.AppendLine($"  Highest Expenses: ${maxExpense:F2}");
        analysis.AppendLine($"  Lowest Expenses: ${minExpense:F2}");

        TrendAnalysisText = analysis.ToString();
    }

    /// <summary>
    /// Export budget report to file
    /// </summary>
    [RelayCommand]
    private void ExportReport()
    {
        // Simple implementation - in real app this would export to Excel/CSV
        Log.Information("ExportReport command executed");
        MessageBox.Show("Budget report export functionality would be implemented here.",
                       "Export Report",
                       MessageBoxButton.OK,
                       MessageBoxImage.Information);
    }

    /// <summary>
    /// Generates budget recommendations
    /// </summary>
    private void GenerateRecommendations()
    {
        if (!BudgetDetails.Any())
        {
            RecommendationsText = "No budget data available for recommendations.";
            return;
        }

        var recommendations = new System.Text.StringBuilder();
        recommendations.AppendLine("BUDGET RECOMMENDATIONS");
        recommendations.AppendLine("=====================");
        recommendations.AppendLine();

        // Check overall portfolio health
        if (NetBalance < 0)
        {
            recommendations.AppendLine("⚠️  CRITICAL: Overall portfolio is operating at a loss");
            recommendations.AppendLine("   Consider rate increases or expense reductions");
            recommendations.AppendLine();
        }

        // Identify deficit enterprises
        var deficitEnterprises = BudgetDetails.Where(b => b.MonthlyBalance < 0).ToList();
        if (deficitEnterprises.Any())
        {
            recommendations.AppendLine("Enterprises requiring attention:");
            foreach (var enterprise in deficitEnterprises.OrderBy(b => b.MonthlyBalance))
            {
                recommendations.AppendLine($"  • {enterprise.EnterpriseName}: Loss of ${Math.Abs(enterprise.MonthlyBalance):F2}");
                recommendations.AppendLine($"    Suggested rate increase: ${(enterprise.BreakEvenRate - enterprise.CurrentRate):F2}");
            }
            recommendations.AppendLine();
        }

        // Identify high performers
        var highPerformers = BudgetDetails.Where(b => b.MonthlyBalance > 100).ToList();
        if (highPerformers.Any())
        {
            recommendations.AppendLine("High-performing enterprises:");
            foreach (var enterprise in highPerformers.OrderByDescending(b => b.MonthlyBalance))
            {
                recommendations.AppendLine($"  • {enterprise.EnterpriseName}: Profit of ${enterprise.MonthlyBalance:F2}");
            }
            recommendations.AppendLine();
        }

        // General recommendations
        recommendations.AppendLine("General Recommendations:");
        recommendations.AppendLine("  • Monitor enterprises with low citizen counts for potential consolidation");
        recommendations.AppendLine("  • Consider seasonal rate adjustments for utilities");
        recommendations.AppendLine("  • Review expense patterns quarterly for optimization opportunities");

        RecommendationsText = recommendations.ToString();
    }

    /// <summary>
    /// Clears any error state
    /// </summary>
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        AnalysisStatus = "Ready";
        Log.Information("Error cleared by user");
    }

    /// <summary>
    /// Dispose pattern implementation
    /// Unsubscribes from messenger to prevent memory leaks
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Unregister from messenger to prevent memory leaks
                WeakReferenceMessenger.Default.Unregister<EnterpriseChangedMessage>(this);
                Log.Debug("BudgetViewModel disposed and unregistered from messenger");
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Data model for budget detail items
/// </summary>
public class BudgetDetailItem
{
    public string EnterpriseName { get; set; } = string.Empty;
    public int CitizenCount { get; set; }
    public decimal CurrentRate { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal MonthlyBalance { get; set; }
    public decimal BreakEvenRate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Data model for budget performance data
/// </summary>
public class BudgetPerformanceData
{
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

/// <summary>
/// Data model for projected rate data
/// </summary>
public class ProjectedRateData
{
    public string Period { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

/// <summary>
/// Data model for rate trend data
/// </summary>
public class RateTrendData
{
    public string Period { get; set; } = string.Empty;
    public decimal Trend { get; set; }
}