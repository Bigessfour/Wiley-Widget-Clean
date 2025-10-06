using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Grok Supercomputer service for advanced mathematical calculations and AI-powered computations
/// </summary>
public class GrokSupercomputer : IDisposable, IGrokSupercomputer
{
    private readonly IAIService _aiService;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly ILogger<GrokSupercomputer>? _logger;
    private bool _disposed;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public GrokSupercomputer(
        IAIService aiService,
        IEnterpriseRepository enterpriseRepository,
        ILogger<GrokSupercomputer>? logger = null)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _logger = logger;
    }

    /// <summary>
    /// Fetch report-ready enterprise data with optional filters
    /// </summary>
    public async Task<ReportDataModel> FetchEnterpriseDataAsync(
        int? enterpriseId = null,
        DateTime? start = null,
        DateTime? end = null,
        string filter = "")
    {
        try
        {
            IEnumerable<Enterprise> enterprises;

            if (enterpriseId.HasValue)
            {
                var enterprise = await _enterpriseRepository.GetByIdAsync(enterpriseId.Value).ConfigureAwait(false);
                enterprises = enterprise is not null
                    ? new[] { enterprise }
                    : Array.Empty<Enterprise>();
            }
            else
            {
                // Fetching DB data: Because AI needs real food, not just prompts.
                enterprises = await _enterpriseRepository.GetAllAsync().ConfigureAwait(false);
            }

            enterprises = ApplyFilters(enterprises, start, end, filter);

            var metrics = enterprises
                .Select(e => new EnterpriseMetric(
                    e.Id,
                    e.Name,
                    GetMonthlyRevenue(e),
                    e.MonthlyExpenses,
                    CalculateRoiPercentage(e),
                    CalculateProfitMarginPercentage(e),
                    e.LastModified))
                .OrderBy(m => m.Name)
                .ToList();

            return new ReportDataModel(enterpriseId, start, end, filter, metrics);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error fetching enterprise data for reporting");
            throw;
        }
    }

    /// <summary>
    /// Run analytics calculations for reporting
    /// </summary>
    public async Task<AnalyticsResult> RunReportCalcsAsync(ReportDataModel data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var metrics = data.Enterprises;
        if (metrics.Count == 0)
        {
            return new AnalyticsResult(
                data,
                new List<ChartSeries>(),
                new List<KpiMetric>(),
                null,
                null);
        }

        var revenueSeries = metrics.Select(m => m.Revenue).ToArray();
        var expenseSeries = metrics.Select(m => m.Expenses).ToArray();
        var profitSeries = metrics.Select(m => m.Revenue - m.Expenses).ToArray();

        StatisticalAnalysisResult? stats = null;
        if (revenueSeries.Length > 0)
        {
            stats = await AnalyzeStatisticsAsync(revenueSeries, "enterprise_revenue").ConfigureAwait(false);
        }

        var averageRevenue = revenueSeries.Average();
        var projection = await CalculateFinancialAsync(
            averageRevenue,
            0.05m,
            12,
            "compound_growth_projection").ConfigureAwait(false);

        var chartData = new List<ChartSeries>
        {
            new("Revenue", revenueSeries.ToList()),
            new("Expenses", expenseSeries.ToList()),
            new("Profit", profitSeries.ToList())
        };

        var gaugeData = new List<KpiMetric>
        {
            new("Average Revenue", Math.Round(averageRevenue, 2)),
            new("Total Revenue", Math.Round(revenueSeries.Sum(), 2)),
            new("Average Profit Margin", Math.Round(metrics.Average(m => m.ProfitMarginPercentage), 2)),
            new("Average ROI", Math.Round(metrics.Average(m => m.RoiPercentage), 2))
        };

        return new AnalyticsResult(data, chartData, gaugeData, stats, projection);
    }

    private static IEnumerable<Enterprise> ApplyFilters(
        IEnumerable<Enterprise> enterprises,
        DateTime? start,
        DateTime? end,
        string filter)
    {
        if (start.HasValue)
        {
            enterprises = enterprises.Where(e => e.LastModified.HasValue && e.LastModified.Value >= start.Value);
        }

        if (end.HasValue)
        {
            enterprises = enterprises.Where(e => e.LastModified.HasValue && e.LastModified.Value <= end.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var comparison = StringComparison.OrdinalIgnoreCase;
            enterprises = enterprises.Where(e =>
                (!string.IsNullOrWhiteSpace(e.Name) && e.Name.Contains(filter, comparison)) ||
                (!string.IsNullOrWhiteSpace(e.Type) && e.Type.Contains(filter, comparison)) ||
                (!string.IsNullOrWhiteSpace(e.Description) && e.Description.Contains(filter, comparison)));
        }

        return enterprises;
    }

    private static decimal GetMonthlyRevenue(Enterprise enterprise)
    {
        ArgumentNullException.ThrowIfNull(enterprise);
        return enterprise.CitizenCount * enterprise.CurrentRate;
    }

    private static decimal CalculateRoiPercentage(Enterprise enterprise)
    {
        var revenue = GetMonthlyRevenue(enterprise);
        var expenses = enterprise.MonthlyExpenses;
        if (expenses == 0)
        {
            return 0m;
        }

        return Math.Round(((revenue - expenses) / expenses) * 100m, 2);
    }

    private static decimal CalculateProfitMarginPercentage(Enterprise enterprise)
    {
        var revenue = GetMonthlyRevenue(enterprise);
        if (revenue == 0)
        {
            return 0m;
        }

        var profit = revenue - enterprise.MonthlyExpenses;
        return Math.Round((profit / revenue) * 100m, 2);
    }

    /// <summary>
    /// Perform advanced mathematical calculation using Grok AI
    /// </summary>
    public async Task<CalculationResult> CalculateAsync(string expression, string context = "")
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new ArgumentException("Expression cannot be null or empty", nameof(expression));

        try
        {
            var prompt = $"Please solve this mathematical calculation step by step: {expression}";
            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt += $"\n\nContext: {context}";
            }
            prompt += "\n\nProvide the final answer clearly and show your work.";

            var aiResponse = await _aiService.GetInsightsAsync("Mathematical Calculation", prompt);

            return new CalculationResult
            {
                Expression = expression,
                Result = aiResponse,
                IsSuccessful = true,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing calculation: {Expression}", expression);

            return new CalculationResult
            {
                Expression = expression,
                Result = $"Error: {ex.Message}",
                IsSuccessful = false,
                Timestamp = DateTime.Now,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Perform financial calculation with detailed analysis
    /// </summary>
    public async Task<FinancialCalculationResult> CalculateFinancialAsync(
        decimal principal,
        decimal rate,
        int periods,
        string calculationType = "compound_interest")
    {
        try
        {
            var context = $"Financial calculation: {calculationType}";
            var expression = $"Calculate {calculationType} for principal ${principal:N2}, rate {rate:P2}, over {periods} periods";

            var prompt = $"Perform a detailed financial calculation:\n\n" +
                        $"Type: {calculationType}\n" +
                        $"Principal: ${principal:N2}\n" +
                        $"Rate: {rate:P2}\n" +
                        $"Periods: {periods}\n\n" +
                        "Show all steps, formulas used, and provide the final result with proper formatting.";

            var aiResponse = await _aiService.GetInsightsAsync(context, prompt);

            return new FinancialCalculationResult
            {
                Principal = principal,
                Rate = rate,
                Periods = periods,
                CalculationType = calculationType,
                Result = aiResponse,
                IsSuccessful = true,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing financial calculation: {Type}", calculationType);

            return new FinancialCalculationResult
            {
                Principal = principal,
                Rate = rate,
                Periods = periods,
                CalculationType = calculationType,
                Result = $"Error: {ex.Message}",
                IsSuccessful = false,
                Timestamp = DateTime.Now,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Analyze data with statistical computations
    /// </summary>
    public async Task<StatisticalAnalysisResult> AnalyzeStatisticsAsync(decimal[] data, string analysisType = "descriptive")
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be null or empty", nameof(data));

        try
        {
            var dataString = string.Join(", ", data.Select(d => d.ToString("N2")));
            var context = $"Statistical analysis: {analysisType}";
            var prompt = $"Perform {analysisType} statistical analysis on the following data:\n\n{dataString}\n\n" +
                        "Calculate and provide:\n" +
                        "- Mean (average)\n" +
                        "- Median\n" +
                        "- Mode\n" +
                        "- Standard deviation\n" +
                        "- Variance\n" +
                        "- Range\n" +
                        "- Quartiles\n\n" +
                        "Provide insights and interpretation of the results.";

            var aiResponse = await _aiService.GetInsightsAsync(context, prompt);

            return new StatisticalAnalysisResult
            {
                Data = data.ToArray(),
                AnalysisType = analysisType,
                Result = aiResponse,
                IsSuccessful = true,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing statistical analysis: {Type}", analysisType);

            return new StatisticalAnalysisResult
            {
                Data = data.ToArray(),
                AnalysisType = analysisType,
                Result = $"Error: {ex.Message}",
                IsSuccessful = false,
                Timestamp = DateTime.Now,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Perform optimization calculation
    /// </summary>
    public async Task<OptimizationResult> OptimizeAsync(string objective, string[] constraints, string context = "")
    {
        if (string.IsNullOrWhiteSpace(objective))
            throw new ArgumentException("Objective cannot be null or empty", nameof(objective));

        try
        {
            var constraintsString = string.Join("\n", constraints.Select((c, i) => $"{i + 1}. {c}"));
            var prompt = $"Solve this optimization problem:\n\n" +
                        $"Objective: {objective}\n\n" +
                        $"Constraints:\n{constraintsString}\n\n";

            if (!string.IsNullOrWhiteSpace(context))
            {
                prompt += $"Context: {context}\n\n";
            }

            prompt += "Provide a step-by-step solution with the optimal values and explain your reasoning.";

            var aiResponse = await _aiService.GetInsightsAsync("Optimization Problem", prompt);

            return new OptimizationResult
            {
                Objective = objective,
                Constraints = constraints.ToArray(),
                Result = aiResponse,
                IsSuccessful = true,
                Timestamp = DateTime.Now
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error performing optimization: {Objective}", objective);

            return new OptimizationResult
            {
                Objective = objective,
                Constraints = constraints.ToArray(),
                Result = $"Error: {ex.Message}",
                IsSuccessful = false,
                Timestamp = DateTime.Now,
                Error = ex
            };
        }
    }

    /// <summary>
    /// Dispose of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources here if any
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Result of a mathematical calculation
/// </summary>
public class CalculationResult
{
    public string Expression { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public DateTime Timestamp { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Result of a financial calculation
/// </summary>
public class FinancialCalculationResult
{
    public decimal Principal { get; set; }
    public decimal Rate { get; set; }
    public int Periods { get; set; }
    public string CalculationType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public DateTime Timestamp { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Result of a statistical analysis
/// </summary>
public class StatisticalAnalysisResult
{
    public decimal[] Data { get; set; } = Array.Empty<decimal>();
    public string AnalysisType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public DateTime Timestamp { get; set; }
    public Exception? Error { get; set; }
}

/// <summary>
/// Result of an optimization calculation
/// </summary>
public class OptimizationResult
{
    public string Objective { get; set; } = string.Empty;
    public string[] Constraints { get; set; } = Array.Empty<string>();
    public string Result { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public DateTime Timestamp { get; set; }
    public Exception? Error { get; set; }
}