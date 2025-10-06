using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Data;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using WileyWidget.Services;

namespace WileyWidget.ViewModels;

/// <summary>
/// Chart data point for rate trend visualization
/// </summary>
public class RateChartDataPoint
{
    public string Period { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}

/// <summary>
/// Chart data point for budget performance visualization
/// </summary>
public class BudgetChartDataPoint
{
    public string Enterprise { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Expenses { get; set; }
}

/// <summary>
/// View model for budget analysis and reporting
/// Provides comprehensive budget insights and financial analysis
/// </summary>
public partial class BudgetViewModel : AsyncViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Collection of budget details for each enterprise
    /// </summary>
    public ThreadSafeObservableCollection<BudgetDetailItem> BudgetItems { get; } = new();

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
    /// Constructor with dependency injection
    /// </summary>
    public BudgetViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        BudgetItems.CollectionChanged += OnBudgetDetailsCollectionChanged;
    }

    /// <summary>
    /// Average rate increase across all enterprises
    /// </summary>
    public double AverageRateIncrease => BudgetItems.Any() ? BudgetItems.Average(b => b.RateIncrease) : 0.0;

    /// <summary>
    /// Rate trend data for charts
    /// </summary>
    public ObservableCollection<RateChartDataPoint> RateTrendData { get; } = new();

    /// <summary>
    /// Projected rate data for charts
    /// </summary>
    public ObservableCollection<RateChartDataPoint> ProjectedRateData { get; } = new();

    /// <summary>
    /// Budget performance data for charts
    /// </summary>
    public ObservableCollection<BudgetChartDataPoint> BudgetPerformanceData { get; } = new();

    /// <summary>
    /// Calculator fixed costs input
    /// </summary>
    [ObservableProperty]
    private double calculatorFixedCosts;

    /// <summary>
    /// Calculator variable cost per unit input
    /// </summary>
    [ObservableProperty]
    private double calculatorVariableCost;

    /// <summary>
    /// Calculator price per unit input
    /// </summary>
    [ObservableProperty]
    private double calculatorPricePerUnit;

    /// <summary>
    /// Revenue growth rate for trend analysis
    /// </summary>
    [ObservableProperty]
    private double revenueGrowthRate;

    /// <summary>
    /// Expense growth rate for trend analysis
    /// </summary>
    [ObservableProperty]
    private double expenseGrowthRate;

    /// <summary>
    /// Profit margin for trend analysis
    /// </summary>
    [ObservableProperty]
    private double profitMargin;

    /// <summary>
    /// Forecast period in months
    /// </summary>
    [ObservableProperty]
    private int forecastPeriodMonths = 12;

    /// <summary>
    /// Expected inflation rate for forecasting
    /// </summary>
    [ObservableProperty]
    private double inflationRate = 0.02;

    /// <summary>
    /// Population growth rate for forecasting
    /// </summary>
    [ObservableProperty]
    private double populationGrowthRate = 0.01;

    /// <summary>
    /// Scenario rate increase percentage
    /// </summary>
    [ObservableProperty]
    private double scenarioRateIncrease = 0.05;

    /// <summary>
    /// Scenario cost reduction percentage
    /// </summary>
    [ObservableProperty]
    private double scenarioCostReduction = 0.03;

    /// <summary>
    /// Scenario new citizens count
    /// </summary>
    [ObservableProperty]
    private int scenarioNewCitizens = 100;

    /// <summary>
    /// Refreshes all budget data from the database
    /// </summary>
    [RelayCommand]
    public async Task RefreshBudgetDataAsync()
    {
        try
        {
            HasError = false;
            ErrorMessage = string.Empty;

            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                var enterprises = (await _enterpriseRepository.GetAllAsync()).ToList();

                if (!enterprises.Any())
                {
                    Logger.LogWarning("No enterprises returned from repository. Falling back to sample data for budget analysis.");
                    await LoadBudgetDetailsAsync(
                        SampleDataFactory.CreateSampleEnterprises(),
                        "Sample data loaded (no database records)",
                        isSampleData: true);
                    return;
                }

                await LoadBudgetDetailsAsync(enterprises, "Data loaded successfully", isSampleData: false);
            }, statusMessage: "Refreshing budget data...");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh budget data: {ex.Message}";
            HasError = true;
            AnalysisStatus = $"Error: {ex.Message}";
            Logger.LogError(ex, "Failed to refresh budget data");

            await LoadBudgetDetailsAsync(
                SampleDataFactory.CreateSampleEnterprises(),
                "Sample data loaded (error fallback)",
                isSampleData: true);
        }
    }

    private async Task LoadBudgetDetailsAsync(IEnumerable<Enterprise> enterprises, string statusMessage, bool isSampleData)
    {
        var enterpriseList = enterprises.ToList();

        var budgetDetails = enterpriseList.Select(enterprise => new BudgetDetailItem
        {
            EnterpriseName = enterprise.Name,
            CitizenCount = enterprise.CitizenCount,
            CurrentRate = enterprise.CurrentRate,
            MonthlyRevenue = enterprise.MonthlyRevenue,
            MonthlyExpenses = enterprise.MonthlyExpenses,
            MonthlyBalance = enterprise.MonthlyBalance,
            BreakEvenRate = enterprise.BreakEvenRate,
            Status = enterprise.MonthlyBalance >= 0 ? "Surplus" : "Deficit",
            RateIncrease = enterprise.BreakEvenRate > enterprise.CurrentRate && enterprise.CurrentRate > 0
                ? (double)((enterprise.BreakEvenRate - enterprise.CurrentRate) / enterprise.CurrentRate) * 100
                : 0.0
        }).ToList();

        await BudgetItems.ReplaceAllAsync(budgetDetails);

        UpdateBudgetAggregates(budgetDetails);

        LastUpdated = DateTime.Now.ToString("g");
        AnalysisStatus = statusMessage;

        GenerateRecommendations();

        OnPropertyChanged(nameof(AverageRateIncrease));

        Logger.LogInformation(
            "Loaded budget data for {Count} enterprises ({Source})",
            budgetDetails.Count,
            isSampleData ? "sample data" : "database");
    }

    private void UpdateBudgetAggregates(IReadOnlyCollection<BudgetDetailItem> budgetDetails)
    {
        if (budgetDetails.Count == 0)
        {
            TotalRevenue = 0;
            TotalExpenses = 0;
            NetBalance = 0;
            TotalCitizens = 0;
            return;
        }

        TotalRevenue = budgetDetails.Sum(b => b.MonthlyRevenue);
        TotalExpenses = budgetDetails.Sum(b => b.MonthlyExpenses);
        NetBalance = TotalRevenue - TotalExpenses;
        TotalCitizens = budgetDetails.Sum(b => b.CitizenCount);
    }

    private void OnBudgetDetailsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(AverageRateIncrease));
    }

    /// <summary>
    /// Performs break-even analysis
    /// </summary>
    [RelayCommand]
    private void BreakEvenAnalysis()
    {
        if (!BudgetItems.Any())
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
    /// Exports budget report (placeholder for future implementation)
    /// </summary>
    [RelayCommand]
    private void ExportReport()
    {
        // TODO: Implement export functionality
        AnalysisStatus = "Export functionality not yet implemented";
    }

    /// <summary>
    /// Clears any error state
    /// </summary>
    [RelayCommand]
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        AnalysisStatus = "Ready";
        Log.Information("Error cleared by user");
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
    public double RateIncrease { get; set; }
}