using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for report generation and AI insights
/// Handles generating reports and creating AI-powered analysis
/// </summary>
public partial class ReportsGenerationViewModel : ValidatableViewModelBase
{
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IAIService _aiService;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Latest generated report data
    /// </summary>
    private ReportDataModel? _latestReport;

    /// <summary>
    /// AI insights for the current report
    /// </summary>
    [ObservableProperty]
    private string aiInsights = "Click 'Generate Report' to see AI insights";

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public ReportsGenerationViewModel(
        IGrokSupercomputer grokSupercomputer,
        IAIService aiService,
        IMemoryCache memoryCache,
        IDispatcherHelper dispatcherHelper,
        ILogger<ReportsGenerationViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Generate report data for the specified parameters
    /// </summary>
    public async Task<ReportDataModel?> GenerateReportAsync(int enterpriseId, DateTime startDate, DateTime endDate, string? filter)
    {
        // Create cache key based on report parameters
        var cacheKey = $"Report_{enterpriseId}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{filter}";

        // Try to get from cache first
        if (!_memoryCache.TryGetValue(cacheKey, out ReportDataModel? reportData) || reportData == null)
        {
            // Not in cache, fetch from service
            reportData = await ExecuteAsyncOperation(ct => _grokSupercomputer.FetchEnterpriseDataAsync(enterpriseId, startDate, endDate, filter ?? string.Empty));

            if (reportData == null)
            {
                Logger.LogWarning("Failed to generate report data");
                return null;
            }

            // Cache the result for 10 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            _memoryCache.Set(cacheKey, reportData, cacheOptions);
        }

        _latestReport = reportData;

        // Generate AI insights in the background
        _ = GenerateAIInsightsAsync(reportData);

        return reportData;
    }

    /// <summary>
    /// Generate AI insights for report data
    /// </summary>
    private async Task GenerateAIInsightsAsync(ReportDataModel reportData)
    {
        try
        {
            // Create context from report data
            var context = $"Report data for {reportData.Enterprises.Count} enterprises from {reportData.Start:d} to {reportData.End:d}. " +
                         $"Total revenue: {reportData.Enterprises.Sum(e => e.Revenue):C}, " +
                         $"Total expenses: {reportData.Enterprises.Sum(e => e.Expenses):C}, " +
                         $"Average ROI: {reportData.Enterprises.Average(e => e.RoiPercentage):P2}";

            // Generate AI insights
            var insights = await _aiService.GetInsightsAsync(context,
                "Provide key insights and recommendations based on this financial report data. Focus on trends, outliers, and actionable recommendations.");

            await DispatcherHelper.InvokeAsync(() =>
            {
                AiInsights = insights;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate AI insights for report data");
            await DispatcherHelper.InvokeAsync(() =>
            {
                AiInsights = "AI insights are currently unavailable. Please try again later.";
            });
        }
    }

    /// <summary>
    /// Get the latest generated report
    /// </summary>
    public ReportDataModel? GetLatestReport() => _latestReport;

    /// <summary>
    /// Clear the latest report data
    /// </summary>
    public void ClearLatestReport()
    {
        _latestReport = null;
        AiInsights = "Click 'Generate Report' to see AI insights";
    }
}