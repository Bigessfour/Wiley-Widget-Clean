using System;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Contract for the Grok Supercomputer service providing advanced analytics and reporting calculations.
/// </summary>
public interface IGrokSupercomputer
{
    /// <summary>
    /// Fetches enterprise data suitable for reporting scenarios with optional filters.
    /// </summary>
    /// <param name="enterpriseId">Optional enterprise identifier filter.</param>
    /// <param name="start">Optional inclusive start date for data selection.</param>
    /// <param name="end">Optional inclusive end date for data selection.</param>
    /// <param name="filter">Optional text filter applied to enterprise metadata.</param>
    /// <returns>A populated report data model representing the filtered enterprise metrics.</returns>
    Task<ReportDataModel> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? start = null, DateTime? end = null, string filter = "");

    /// <summary>
    /// Runs the Grok analytics pipeline over previously fetched report data.
    /// </summary>
    /// <param name="data">The source report data model.</param>
    /// <returns>An analytics result containing chart and KPI output.</returns>
    Task<AnalyticsResult> RunReportCalcsAsync(ReportDataModel data);

    /// <summary>
    /// Performs a mathematical calculation using Grok.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="context">Context for the calculation.</param>
    /// <returns>The calculation result.</returns>
    Task<CalculationResult> CalculateAsync(string expression, string context = "");

    /// <summary>
    /// Performs a financial calculation with detailed AI-assisted analysis.
    /// </summary>
    /// <param name="principal">The starting principal value.</param>
    /// <param name="rate">The interest or growth rate.</param>
    /// <param name="periods">The number of periods for the calculation.</param>
    /// <param name="calculationType">The calculation scenario identifier.</param>
    /// <returns>The financial calculation result.</returns>
    Task<FinancialCalculationResult> CalculateFinancialAsync(decimal principal, decimal rate, int periods, string calculationType = "compound_interest");

    /// <summary>
    /// Performs statistical analysis over a provided dataset.
    /// </summary>
    /// <param name="data">The dataset to analyse.</param>
    /// <param name="analysisType">The analysis flavor identifier.</param>
    /// <returns>The statistical analysis result.</returns>
    Task<StatisticalAnalysisResult> AnalyzeStatisticsAsync(decimal[] data, string analysisType = "descriptive");

    /// <summary>
    /// Performs an optimisation routine based on the specified objective and constraints.
    /// </summary>
    /// <param name="objective">The optimisation objective statement.</param>
    /// <param name="constraints">The optimisation constraints.</param>
    /// <param name="context">Optional additional context.</param>
    /// <returns>The optimisation result.</returns>
    Task<OptimizationResult> OptimizeAsync(string objective, string[] constraints, string context = "");
}
