using System;
using System.Collections.Generic;

namespace WileyWidget.Services;

/// <summary>
/// Describes enterprise data used for report generation.
/// </summary>
public sealed record EnterpriseMetric(
    int Id,
    string Name,
    decimal Revenue,
    decimal Expenses,
    decimal RoiPercentage,
    decimal ProfitMarginPercentage,
    DateTime? LastModified);

/// <summary>
/// Container for report data fetched from the repository.
/// </summary>
public sealed record ReportDataModel(
    int? EnterpriseId,
    DateTime? Start,
    DateTime? End,
    string Filter,
    List<EnterpriseMetric> Enterprises);

/// <summary>
/// Represents a chart-ready series for Syncfusion charts.
/// </summary>
public sealed record ChartSeries(string Name, IReadOnlyList<decimal> Values);

/// <summary>
/// Represents a KPI value for gauge visualizations.
/// </summary>
public sealed record KpiMetric(string Name, decimal Value);

/// <summary>
/// Output of analytics processing ready for reporting views.
/// </summary>
public sealed record AnalyticsResult(
    ReportDataModel Data,
    List<ChartSeries> ChartData,
    List<KpiMetric> GaugeData,
    StatisticalAnalysisResult? StatisticalSummary,
    FinancialCalculationResult? FinancialProjection);
