using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Services;

/// <summary>
/// Defines export operations for report data into common document formats.
/// </summary>
public interface IReportExportService
{
    /// <summary>
    /// Persists the supplied report data to a PDF destination.
    /// </summary>
    /// <param name="reportData">The report data to export.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="cancellationToken">Token to cancel the export.</param>
    Task ExportToPdfAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied report data to an Excel workbook.
    /// </summary>
    /// <param name="reportData">The report data to export.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="cancellationToken">Token to cancel the export.</param>
    Task ExportToExcelAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the supplied report data to an RDL document for SSRS usage.
    /// </summary>
    /// <param name="reportData">The report data to export.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="cancellationToken">Token to cancel the export.</param>
    Task ExportToRdlAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default);
}
