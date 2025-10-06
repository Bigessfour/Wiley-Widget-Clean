using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Threading.Tasks;
using System.IO;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for report export operations
/// Handles exporting reports to various formats (PDF, Excel, RDL)
/// </summary>
public partial class ReportsExportViewModel : ValidatableViewModelBase
{
    private readonly IReportExportService _reportExportService;

    /// <summary>
    /// Event raised when export is completed
    /// </summary>
    public event EventHandler<ReportsViewModel.ReportExportCompletedEventArgs>? ExportCompleted;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public ReportsExportViewModel(
        IReportExportService reportExportService,
        IDispatcherHelper dispatcherHelper,
        ILogger<ReportsExportViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
    }

    /// <summary>
    /// Export report data to the specified format
    /// </summary>
    public async Task ExportAsync(ReportDataModel reportData, string? format)
    {
        if (reportData is null)
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
                    await _reportExportService.ExportToPdfAsync(reportData, targetPath, ct).ConfigureAwait(false);
                    break;
                case "excel":
                case "xlsx":
                    await _reportExportService.ExportToExcelAsync(reportData, targetPath, ct).ConfigureAwait(false);
                    break;
                case "rdl":
                case "rdlc":
                    await _reportExportService.ExportToRdlAsync(reportData, targetPath, ct).ConfigureAwait(false);
                    break;
                default:
                    Logger.LogWarning("Unsupported export format: {Format}", normalizedFormat);
                    return;
            }

            ExportCompleted?.Invoke(this, new ReportsViewModel.ReportExportCompletedEventArgs(normalizedFormat, targetPath));
        }, statusMessage: $"Exporting report ({normalizedFormat.ToUpperInvariant()})...");
    }

    /// <summary>
    /// Create export file path with appropriate extension
    /// </summary>
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
}