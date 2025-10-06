using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

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
/// ViewModel for budget analysis operations
/// Handles break-even analysis, trend analysis, recommendations, and chart data
/// </summary>
public partial class BudgetAnalysisViewModel : AsyncViewModelBase
{
    /// <summary>
    /// Collection of budget details for analysis
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
    /// Analysis status
    /// </summary>
    [ObservableProperty]
    private string analysisStatus = "Ready";

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
    /// Revenue growth rate for trend analysis
    /// </summary>
    [ObservableProperty]
    private double revenueGrowthRate;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetAnalysisViewModel(
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetViewModel> logger)
        : base(dispatcherHelper, logger)
    {
    }

    /// <summary>
    /// Average rate increase across all enterprises
    /// </summary>
    public double AverageRateIncrease => BudgetItems.Any() ? BudgetItems.Average(b => b.RateIncrease) : 0.0;

    /// <summary>
    /// Performs break-even analysis
    /// </summary>
    [RelayCommand]
    public void BreakEvenAnalysis()
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

        foreach (var detail in BudgetItems.OrderByDescending(b => b.MonthlyBalance))
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
        AnalysisStatus = "Break-even analysis completed";
    }

    /// <summary>
    /// Performs trend analysis
    /// </summary>
    [RelayCommand]
    public void TrendAnalysis()
    {
        if (!BudgetItems.Any())
        {
            TrendAnalysisText = "No budget data available. Please refresh data first.";
            return;
        }

        var analysis = new System.Text.StringBuilder();
        analysis.AppendLine("BUDGET TREND ANALYSIS");
        analysis.AppendLine("====================");
        analysis.AppendLine();

        // Rate trend analysis
        var avgRateIncrease = AverageRateIncrease;
        analysis.AppendLine($"Average Rate Increase: {avgRateIncrease:F2}%");
        analysis.AppendLine();

        // Revenue vs Expense trends
        var profitableEnterprises = BudgetItems.Count(b => b.MonthlyBalance > 0);
        var lossEnterprises = BudgetItems.Count(b => b.MonthlyBalance < 0);
        var breakEvenEnterprises = BudgetItems.Count(b => b.MonthlyBalance == 0);

        analysis.AppendLine("Enterprise Status Summary:");
        analysis.AppendLine($"  Profitable: {profitableEnterprises}");
        analysis.AppendLine($"  Loss: {lossEnterprises}");
        analysis.AppendLine($"  Break-even: {breakEvenEnterprises}");
        analysis.AppendLine();

        // Revenue growth projection
        if (RevenueGrowthRate > 0)
        {
            var projectedRevenue = TotalRevenue * (decimal)(1 + RevenueGrowthRate / 100);
            analysis.AppendLine($"Projected Revenue (at {RevenueGrowthRate:F1}% growth): ${projectedRevenue:F2}");
        }

        TrendAnalysisText = analysis.ToString();
        AnalysisStatus = "Trend analysis completed";
    }

    /// <summary>
    /// Generates budget recommendations
    /// </summary>
    [RelayCommand]
    public void GenerateRecommendations()
    {
        if (!BudgetItems.Any())
        {
            RecommendationsText = "No budget data available. Please refresh data first.";
            return;
        }

        var recommendations = new System.Text.StringBuilder();
        recommendations.AppendLine("BUDGET RECOMMENDATIONS");
        recommendations.AppendLine("====================");
        recommendations.AppendLine();

        // Identify enterprises needing attention
        var lossMakers = BudgetItems.Where(b => b.MonthlyBalance < 0).ToList();
        var lowMarginEnterprises = BudgetItems.Where(b => b.MonthlyBalance > 0 && b.MonthlyBalance < 1000).ToList();
        var highPerformers = BudgetItems.Where(b => b.MonthlyBalance > 5000).ToList();

        if (lossMakers.Any())
        {
            recommendations.AppendLine("Enterprises Requiring Immediate Attention:");
            foreach (var enterprise in lossMakers.OrderBy(b => b.MonthlyBalance))
            {
                recommendations.AppendLine($"  • {enterprise.EnterpriseName}: Loss of ${Math.Abs(enterprise.MonthlyBalance):F2}");
                recommendations.AppendLine($"    Suggested rate increase: ${(enterprise.BreakEvenRate - enterprise.CurrentRate):F2}");
            }
            recommendations.AppendLine();
        }

        if (lowMarginEnterprises.Any())
        {
            recommendations.AppendLine("Enterprises with Low Profit Margins:");
            foreach (var enterprise in lowMarginEnterprises.OrderBy(b => b.MonthlyBalance))
            {
                recommendations.AppendLine($"  • {enterprise.EnterpriseName}: Profit of ${enterprise.MonthlyBalance:F2}");
            }
            recommendations.AppendLine();
        }

        if (highPerformers.Any())
        {
            recommendations.AppendLine("High-Performing Enterprises:");
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
        AnalysisStatus = "Recommendations generated";
    }

    /// <summary>
    /// Updates totals when budget items change
    /// </summary>
    public void UpdateTotals()
    {
        TotalRevenue = BudgetItems.Sum(b => b.MonthlyRevenue);
        TotalExpenses = BudgetItems.Sum(b => b.MonthlyExpenses);
        NetBalance = TotalRevenue - TotalExpenses;
        TotalCitizens = BudgetItems.Sum(b => b.CitizenCount);
    }
}