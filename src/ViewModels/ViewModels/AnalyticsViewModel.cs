using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// Analytics dashboard coordinator powering Syncfusion visualizations.
/// Strapping AI calcs to UI: What could go wrong?
/// </summary>
public sealed partial class AnalyticsViewModel : ValidatableViewModelBase
{
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IAIService _aiService;

    private ReportDataModel? _currentReport;
    private AnalyticsResult? _latestAnalytics;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsViewModel"/> class.
    /// </summary>
    public AnalyticsViewModel(
        IGrokSupercomputer grokSupercomputer,
        IEnterpriseRepository enterpriseRepository,
        IMemoryCache memoryCache,
        IAIService aiService,
        IDispatcherHelper dispatcherHelper,
        ILogger<AnalyticsViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));

    ChartSeriesCollection = new ObservableCollection<ChartSeries>();
    GaugeCollection = new ObservableCollection<KpiMetric>();
    PivotSource = new ObservableCollection<ReportsViewModel.ReportItem>();
    Enterprises = new ObservableCollection<ReportsViewModel.EnterpriseReference>();
    FilterOptions = new ObservableCollection<string>(new[] { "All Data", "Top ROI", "Margin Leaders", "Recent Updates" });
    Filter = FilterOptions.First();

        StartDate = DateTime.Today.AddMonths(-3);
        EndDate = DateTime.Today;

        RefreshAnalyticsCommand = new AsyncRelayCommand(RefreshAnalyticsAsync, () => !IsLoading);
        DrillDownCommand = new AsyncRelayCommand<object?>(DrillDownAsync, _ => !IsLoading && _latestAnalytics is not null);

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(IsLoading))
            {
                RefreshAnalyticsCommand.NotifyCanExecuteChanged();
                DrillDownCommand.NotifyCanExecuteChanged();
            }
        };

        _ = LoadEnterpriseReferencesAsync();
    }

    /// <summary>
    /// Fired when analytics data is refreshed.
    /// </summary>
    public event EventHandler<AnalyticsDataEventArgs>? DataLoaded;

    /// <summary>
    /// Collection powering chart series.
    /// </summary>
    public ObservableCollection<ChartSeries> ChartSeriesCollection { get; }

    /// <summary>
    /// Gauge metrics for KPI display.
    /// </summary>
    public ObservableCollection<KpiMetric> GaugeCollection { get; }

    /// <summary>
    /// Pivot grid data source.
    /// </summary>
    public ObservableCollection<ReportsViewModel.ReportItem> PivotSource { get; }

    /// <summary>
    /// Available enterprises for filtering.
    /// </summary>
    public ObservableCollection<ReportsViewModel.EnterpriseReference> Enterprises { get; }

    /// <summary>
    /// Selected enterprise identifier.
    /// </summary>
    [ObservableProperty]
    private int? enterpriseId;

    /// <summary>
    /// Inclusive filter start date.
    /// </summary>
    [ObservableProperty]
    private DateTime startDate;

    partial void OnStartDateChanged(DateTime value)
    {
        ValidateProperty(value, nameof(StartDate));
    }

    /// <summary>
    /// Inclusive filter end date.
    /// </summary>
    [ObservableProperty]
    private DateTime endDate;

    partial void OnEndDateChanged(DateTime value)
    {
        ValidateProperty(value, nameof(EndDate));
    }

    /// <summary>
    /// Optional free-text filter.
    /// </summary>
    [ObservableProperty]
    private string filter = string.Empty;

    /// <summary>
    /// AI-generated insights and recommendations based on analytics data.
    /// </summary>
    [ObservableProperty]
    private string? aiInsights;

    /// <summary>
    /// Predefined filter choices exposed to the UI.
    /// </summary>
    public ObservableCollection<string> FilterOptions { get; }

    /// <summary>
    /// Command that refreshes the analytics dashboard.
    /// </summary>
    public IAsyncRelayCommand RefreshAnalyticsCommand { get; }

    /// <summary>
    /// Command invoked when a chart selection requests drill-down.
    /// </summary>
    public IAsyncRelayCommand<object?> DrillDownCommand { get; }

    /// <inheritdoc />
    protected override void ValidatePropertyValue(object? value, string propertyName, List<string> errors)
    {
        base.ValidatePropertyValue(value, propertyName, errors);

        if (propertyName is nameof(StartDate) or nameof(EndDate))
        {
            var start = propertyName == nameof(StartDate) ? (DateTime)value! : StartDate;
            var end = propertyName == nameof(EndDate) ? (DateTime)value! : EndDate;

            if (start > end)
            {
                errors.Add("Start date must be earlier than end date.");
            }
        }
    }

    private async Task LoadEnterpriseReferencesAsync()
    {
        try
        {
            var enterprises = await _enterpriseRepository.GetAllAsync().ConfigureAwait(false);
            await DispatcherHelper.InvokeAsync(() =>
            {
                Enterprises.Clear();
                foreach (var enterprise in enterprises.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
                {
                    Enterprises.Add(new ReportsViewModel.EnterpriseReference(enterprise.Id, enterprise.Name));
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load enterprise references for analytics filters");
        }
    }

    private async Task RefreshAnalyticsAsync()
    {
        if (!ValidateProperty(StartDate, nameof(StartDate)) || !ValidateProperty(EndDate, nameof(EndDate)))
        {
            return;
        }

        // Create cache key based on analytics parameters
        var cacheKey = $"Analytics_{EnterpriseId}_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}_{Filter}";

        // Try to get from cache first
        ReportDataModel? reportData;
        AnalyticsResult? analytics;

        if (!_memoryCache.TryGetValue(cacheKey, out (ReportDataModel report, AnalyticsResult analyticsResult)? cachedData))
        {
            // Not in cache, fetch and calculate
            reportData = await ExecuteAsyncOperation(ct => _grokSupercomputer.FetchEnterpriseDataAsync(EnterpriseId, StartDate, EndDate, Filter));
            analytics = await ExecuteAsyncOperation(_ => _grokSupercomputer.RunReportCalcsAsync(reportData), statusMessage: "Crunching analytics insight...");

            // Cache both results for 10 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            _memoryCache.Set(cacheKey, (reportData, analytics), cacheOptions);
        }
        else
        {
            (reportData, analytics) = cachedData.Value;
        }

        _currentReport = reportData;
        _latestAnalytics = analytics;

        await DispatcherHelper.InvokeAsync(() =>
        {
            ChartSeriesCollection.Clear();
            foreach (var series in analytics.ChartData)
            {
                ChartSeriesCollection.Add(series);
            }

            GaugeCollection.Clear();
            foreach (var metric in analytics.GaugeData)
            {
                GaugeCollection.Add(metric);
            }

            PivotSource.Clear();
            foreach (var metric in reportData.Enterprises)
            {
                PivotSource.Add(new ReportsViewModel.ReportItem(
                    metric.Id,
                    metric.Name,
                    metric.Revenue,
                    metric.Expenses,
                    metric.RoiPercentage,
                    metric.ProfitMarginPercentage,
                    metric.LastModified));
            }
        });

        DataLoaded?.Invoke(this, new AnalyticsDataEventArgs(reportData, analytics));

        // Generate AI insights in the background
        _ = GenerateAIInsightsAsync(reportData, analytics);
    }

    private async Task GenerateAIInsightsAsync(ReportDataModel reportData, AnalyticsResult analytics)
    {
        try
        {
            // Create context from analytics data
            var context = $"Analytics data for {reportData.Enterprises.Count} enterprises from {StartDate:d} to {EndDate:d}. " +
                         $"{analytics.ChartData.Count} chart series, {analytics.GaugeData.Count} KPIs. " +
                         $"Key metrics: Revenue trends, expense analysis, ROI patterns, profit margins.";

            // Generate AI insights
            var insights = await _aiService.GetInsightsAsync(context, 
                "Provide key insights and recommendations based on this analytics data. Focus on trends, correlations, and actionable business recommendations.");

            await DispatcherHelper.InvokeAsync(() =>
            {
                AiInsights = insights;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate AI insights for analytics data");
            await DispatcherHelper.InvokeAsync(() =>
            {
                AiInsights = "AI insights are currently unavailable. Please try again later.";
            });
        }
    }

    private async Task DrillDownAsync(object? argument)
    {
        if (_latestAnalytics is null || _currentReport is null)
        {
            Logger.LogWarning("Drill-down requested without analytics data available");
            return;
        }

        await ExecuteAsyncOperation(_ => Task.CompletedTask, statusMessage: "Applying drill-down filter...");

        if (argument is ReportsViewModel.ReportItem reportItem)
        {
            Filter = reportItem.EnterpriseName;
        }
        else if (argument is string label && !string.IsNullOrWhiteSpace(label))
        {
            Filter = label;
        }
    }

    /// <summary>
    /// Event payload for analytics refreshes.
    /// </summary>
    public sealed class AnalyticsDataEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnalyticsDataEventArgs"/> class.
        /// </summary>
        public AnalyticsDataEventArgs(ReportDataModel report, AnalyticsResult analytics)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
            Analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        /// <summary>
        /// Gets the base report data.
        /// </summary>
        public ReportDataModel Report { get; }

        /// <summary>
        /// Gets the analytics summary.
        /// </summary>
        public AnalyticsResult Analytics { get; }
    }
}