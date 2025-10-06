using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
/// View model responsible for coordinating report generation and export workflows.
/// Strapping AI calcs to UI: What could go wrong?
/// </summary>
public sealed partial class ReportsViewModel : ValidatableViewModelBase
{
    private readonly IGrokSupercomputer _grokSupercomputer;
    private readonly IReportExportService _reportExportService;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly IAIService _aiService;

    private ReportDataModel? _latestReport;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportsViewModel"/> class.
    /// </summary>
    public ReportsViewModel(
        IGrokSupercomputer grokSupercomputer,
        IReportExportService reportExportService,
        IEnterpriseRepository enterpriseRepository,
        IMemoryCache memoryCache,
        IAIService aiService,
        IDispatcherHelper dispatcherHelper,
        ILogger<ReportsViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _grokSupercomputer = grokSupercomputer ?? throw new ArgumentNullException(nameof(grokSupercomputer));
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));

        ReportItems = new ObservableCollection<ReportItem>();
        Enterprises = new ObservableCollection<EnterpriseReference>();

        StartDate = DateTime.Today.AddMonths(-1);
        EndDate = DateTime.Today;

        GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync, () => !IsLoading);
        ExportCommand = new AsyncRelayCommand<string?>(ExportAsync, _ => !IsLoading && _latestReport is not null);

        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(IsLoading))
            {
                GenerateReportCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        };

        _ = LoadEnterpriseReferencesAsync();
    }

    /// <summary>
    /// Event raised once fresh data has been loaded.
    /// </summary>
    public event EventHandler<ReportDataEventArgs>? DataLoaded;

    /// <summary>
    /// Event raised once an export has completed.
    /// </summary>
    public event EventHandler<ReportExportCompletedEventArgs>? ExportCompleted;

    /// <summary>
    /// Collection of report items displayed in the UI.
    /// </summary>
    public ObservableCollection<ReportItem> ReportItems { get; }

    /// <summary>
    /// Available enterprises for filtering.
    /// </summary>
    public ObservableCollection<EnterpriseReference> Enterprises { get; }

    /// <summary>
    /// Selected enterprise identifier.
    /// </summary>
    [ObservableProperty]
    private int? enterpriseId;

    /// <summary>
    /// Inclusive report start date.
    /// </summary>
    [ObservableProperty]
    private DateTime startDate;

    partial void OnStartDateChanged(DateTime value)
    {
        ValidateProperty(value, nameof(StartDate));
    }

    /// <summary>
    /// Inclusive report end date.
    /// </summary>
    [ObservableProperty]
    private DateTime endDate;

    partial void OnEndDateChanged(DateTime value)
    {
        ValidateProperty(value, nameof(EndDate));
    }

    /// <summary>
    /// Optional text filter applied to enterprise metadata.
    /// </summary>
    [ObservableProperty]
    private string filter = string.Empty;

    /// <summary>
    /// AI-generated insights and recommendations based on report data.
    /// </summary>
    [ObservableProperty]
    private string? aiInsights;

    /// <summary>
    /// Command that triggers report generation.
    /// </summary>
    public IAsyncRelayCommand GenerateReportCommand { get; }

    /// <summary>
    /// Command that executes export workflows.
    /// </summary>
    public IAsyncRelayCommand<string?> ExportCommand { get; }

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
                    Enterprises.Add(new EnterpriseReference(enterprise.Id, enterprise.Name));
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load enterprise references for reporting filters");
        }
    }

    private async Task GenerateReportAsync()
    {
        if (!ValidateProperty(StartDate, nameof(StartDate)) || !ValidateProperty(EndDate, nameof(EndDate)))
        {
            return;
        }

        // Create cache key based on report parameters
        var cacheKey = $"Report_{EnterpriseId}_{StartDate:yyyyMMdd}_{EndDate:yyyyMMdd}_{Filter}";

        // Try to get from cache first
        if (!_memoryCache.TryGetValue(cacheKey, out ReportDataModel? reportData))
        {
            // Not in cache, fetch from service
            reportData = await ExecuteAsyncOperation(ct => _grokSupercomputer.FetchEnterpriseDataAsync(EnterpriseId, StartDate, EndDate, Filter));

            // Cache the result for 10 minutes
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));
            _memoryCache.Set(cacheKey, reportData, cacheOptions);
        }

        _latestReport = reportData;

        await DispatcherHelper.InvokeAsync(() =>
        {
            ReportItems.Clear();
            foreach (var metric in reportData.Enterprises)
            {
                ReportItems.Add(new ReportItem(
                    metric.Id,
                    metric.Name,
                    metric.Revenue,
                    metric.Expenses,
                    metric.RoiPercentage,
                    metric.ProfitMarginPercentage,
                    metric.LastModified));
            }
        });

        DataLoaded?.Invoke(this, new ReportDataEventArgs(reportData));

        // Generate AI insights in the background
        _ = GenerateAIInsightsAsync(reportData);
    }

    private async Task GenerateAIInsightsAsync(ReportDataModel reportData)
    {
        try
        {
            // Create context from report data
            var context = $"Report data for {reportData.Enterprises.Count} enterprises from {StartDate:d} to {EndDate:d}. " +
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

    private async Task ExportAsync(string? format)
    {
        if (_latestReport is null)
        {
            Logger.LogWarning("Export requested with no report data available");
            return;
        }

        if (string.IsNullOrWhiteSpace(format))
        {
            Logger.LogWarning("Export requested without specifying a format");
            return;
        }

        var normalizedFormat = format.Trim().ToLowerInvariant();
        var targetPath = CreateExportPath(normalizedFormat);

        await ExecuteAsyncOperation(async ct =>
        {
            switch (normalizedFormat)
            {
                case "pdf":
                    await _reportExportService.ExportToPdfAsync(_latestReport, targetPath, ct).ConfigureAwait(false);
                    break;
                case "excel":
                case "xlsx":
                    await _reportExportService.ExportToExcelAsync(_latestReport, targetPath, ct).ConfigureAwait(false);
                    break;
                case "rdl":
                case "rdlc":
                    await _reportExportService.ExportToRdlAsync(_latestReport, targetPath, ct).ConfigureAwait(false);
                    break;
                default:
                    Logger.LogWarning("Unsupported export format: {Format}", normalizedFormat);
                    return;
            }

            ExportCompleted?.Invoke(this, new ReportExportCompletedEventArgs(normalizedFormat, targetPath));
        }, statusMessage: $"Exporting report ({normalizedFormat.ToUpperInvariant()})...");
    }

    private static string CreateExportPath(string format)
    {
        var extension = format switch
        {
            "pdf" => "pdf",
            "excel" or "xlsx" => "xlsx",
            "rdl" or "rdlc" => "rdl",
            _ => format
        };

        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WileyWidget", "Reports");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"Report_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}");
    }

    /// <summary>
    /// Represents a flattened report record suitable for UI consumption.
    /// </summary>
    public sealed record ReportItem(
        int EnterpriseId,
        string EnterpriseName,
        decimal Revenue,
        decimal Expenses,
        decimal RoiPercentage,
        decimal ProfitMarginPercentage,
        DateTime? LastModified);

    /// <summary>
    /// Lightweight reference to an enterprise for selection controls.
    /// </summary>
    public sealed record EnterpriseReference(int Id, string Name)
    {
        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>
    /// Arguments describing newly loaded report data.
    /// </summary>
    public sealed class ReportDataEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReportDataEventArgs"/> class.
        /// </summary>
        public ReportDataEventArgs(ReportDataModel report)
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        /// <summary>
        /// Gets the loaded report payload.
        /// </summary>
        public ReportDataModel Report { get; }
    }

    /// <summary>
    /// Arguments describing the result of an export operation.
    /// </summary>
    public sealed class ReportExportCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReportExportCompletedEventArgs"/> class.
        /// </summary>
        public ReportExportCompletedEventArgs(string format, string filePath)
        {
            Format = format ?? throw new ArgumentNullException(nameof(format));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        /// <summary>
        /// Gets the normalized export format.
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Gets the physical file path created during export.
        /// </summary>
        public string FilePath { get; }
    }
}