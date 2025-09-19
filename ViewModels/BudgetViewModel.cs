using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Data;
using System.Threading.Tasks;
using System.Linq;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for budget analysis and reporting
/// Provides comprehensive budget insights and financial analysis
/// </summary>
public partial class BudgetViewModel : ObservableObject
{
    private readonly IEnterpriseRepository _enterpriseRepository;

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
    /// Constructor with dependency injection
    /// </summary>
    public BudgetViewModel(IEnterpriseRepository enterpriseRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Refreshes all budget data from the database
    /// </summary>
    [RelayCommand]
    public async Task RefreshBudgetDataAsync()
    {
        try
        {
            AnalysisStatus = "Loading...";
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
            AnalysisStatus = "Data loaded successfully";

            // Generate initial recommendations
            GenerateRecommendations();
        }
        catch (Exception ex)
        {
            AnalysisStatus = $"Error: {ex.Message}";
            Console.WriteLine($"Error refreshing budget data: {ex.Message}");
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