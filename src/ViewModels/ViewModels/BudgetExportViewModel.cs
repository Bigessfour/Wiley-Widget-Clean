using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using System.Threading.Tasks;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using WileyWidget.Services;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.XlsIO;
using System.IO;
using Syncfusion.Drawing;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for budget export operations
/// Handles PDF and Excel export functionality using Syncfusion
/// </summary>
public partial class BudgetExportViewModel : AsyncViewModelBase
{
    private readonly IReportExportService _reportExportService;

    /// <summary>
    /// Collection of budget details for export
    /// </summary>
    public ThreadSafeObservableCollection<BudgetDetailItem> BudgetItems { get; } = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetExportViewModel(
        IReportExportService reportExportService,
        IDispatcherHelper dispatcherHelper,
        ILogger<BudgetViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _reportExportService = reportExportService ?? throw new ArgumentNullException(nameof(reportExportService));
    }

    /// <summary>
    /// Whether data can be exported
    /// </summary>
    public bool CanExport => !IsLoading && BudgetItems.Any();

    /// <summary>
    /// Export to PDF command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    public async Task ExportPdfAsync()
    {
        if (!BudgetItems.Any())
        {
            return;
        }

        try
        {
            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                var exportPath = $"BudgetAnalysis_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

                // Create a simple PDF export using Syncfusion
                await ExportToPdfInternalAsync(BudgetItems, exportPath, cancellationToken);

                StatusMessage = $"PDF exported successfully to {exportPath}";

            }, statusMessage: "Exporting budget data to PDF...");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export budget data to PDF");
            StatusMessage = $"PDF export failed: {ex.Message}";
            throw;
        }
    }

    /// <summary>
    /// Export to Excel command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    public async Task ExportExcelAsync()
    {
        if (!BudgetItems.Any())
        {
            return;
        }

        try
        {
            await ExecuteAsyncOperation(async (cancellationToken) =>
            {
                var exportPath = $"BudgetAnalysis_Report_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                // Create a simple Excel export using Syncfusion
                await ExportToExcelInternalAsync(BudgetItems, exportPath, cancellationToken);

                StatusMessage = $"Excel exported successfully to {exportPath}";

            }, statusMessage: "Exporting budget data to Excel...");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export budget data to Excel");
            StatusMessage = $"Excel export failed: {ex.Message}";
            throw;
        }
    }

    /// <summary>
    /// Export budget report (placeholder for future implementation)
    /// </summary>
    [RelayCommand]
    public void ExportReport()
    {
        // TODO: Implement export functionality
        StatusMessage = "Export functionality not yet implemented";
    }

    /// <summary>
    /// Internal PDF export implementation using Syncfusion
    /// </summary>
    private async Task ExportToPdfInternalAsync(IEnumerable<BudgetDetailItem> items, string filePath, CancellationToken cancellationToken)
    {
        // Create PDF document using Syncfusion
        using var document = new PdfDocument();
        var page = document.Pages.Add();

        // Add title
        var font = new PdfStandardFont(PdfFontFamily.Helvetica, 16, PdfFontStyle.Bold);
        page.Graphics.DrawString("Municipal Budget Analysis Report", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(0, 0));

        // Add timestamp
        var smallFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
        page.Graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", smallFont, PdfBrushes.Black, new Syncfusion.Drawing.PointF(0, 30));

        // Create table
        var table = new PdfGrid();
        table.Columns.Add(8);

        // Add headers
        var headerRow = table.Rows.Add();
        headerRow.Cells[0].Value = "Enterprise";
        headerRow.Cells[1].Value = "Citizens";
        headerRow.Cells[2].Value = "Current Rate";
        headerRow.Cells[3].Value = "Monthly Revenue";
        headerRow.Cells[4].Value = "Monthly Expenses";
        headerRow.Cells[5].Value = "Monthly Balance";
        headerRow.Cells[6].Value = "Break-Even Rate";
        headerRow.Cells[7].Value = "Status";

        // Style headers
        var headerStyle = new PdfGridCellStyle();
        headerStyle.Font = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        headerStyle.BackgroundBrush = PdfBrushes.LightGray;

        foreach (PdfGridCell cell in headerRow.Cells)
        {
            cell.Style = headerStyle;
        }

        // Add data rows
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = table.Rows.Add();
            row.Cells[0].Value = item.EnterpriseName;
            row.Cells[1].Value = item.CitizenCount.ToString();
            row.Cells[2].Value = item.CurrentRate.ToString("C2");
            row.Cells[3].Value = item.MonthlyRevenue.ToString("C2");
            row.Cells[4].Value = item.MonthlyExpenses.ToString("C2");
            row.Cells[5].Value = item.MonthlyBalance.ToString("C2");
            row.Cells[6].Value = item.BreakEvenRate.ToString("C2");
            row.Cells[7].Value = item.Status;
        }

        // Draw table
        table.Draw(page.Graphics, new Syncfusion.Drawing.RectangleF(0, 60, page.GetClientSize().Width, page.GetClientSize().Height - 60));

        // Save document
        await using var stream = File.Create(filePath);
        document.Save(stream);
    }

    /// <summary>
    /// Internal Excel export implementation using Syncfusion
    /// </summary>
    private async Task ExportToExcelInternalAsync(IEnumerable<BudgetDetailItem> items, string filePath, CancellationToken cancellationToken)
    {
        // Create Excel workbook using Syncfusion
        var excelEngine = new ExcelEngine();
        var workbook = excelEngine.Excel.Workbooks.Create();
        var worksheet = workbook.Worksheets[0];
        worksheet.Name = "Budget Analysis";

        // Add title
        worksheet.Range["A1"].Text = "Municipal Budget Analysis Report";
        worksheet.Range["A1"].CellStyle.Font.Bold = true;
        worksheet.Range["A1"].CellStyle.Font.Size = 16;

        // Add timestamp
        worksheet.Range["A2"].Text = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        worksheet.Range["A2"].CellStyle.Font.Size = 10;

        // Add headers
        var headers = new[] { "Enterprise", "Citizens", "Current Rate", "Monthly Revenue", "Monthly Expenses", "Monthly Balance", "Break-Even Rate", "Status" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Range[4, i + 1].Text = headers[i];
            worksheet.Range[4, i + 1].CellStyle.Font.Bold = true;
        }

        // Add data rows
        int rowIndex = 5;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            worksheet.Range[rowIndex, 1].Text = item.EnterpriseName;
            worksheet.Range[rowIndex, 2].Number = item.CitizenCount;
            worksheet.Range[rowIndex, 3].Number = (double)item.CurrentRate;
            worksheet.Range[rowIndex, 3].NumberFormat = "$#,##0.00";
            worksheet.Range[rowIndex, 4].Number = (double)item.MonthlyRevenue;
            worksheet.Range[rowIndex, 4].NumberFormat = "$#,##0.00";
            worksheet.Range[rowIndex, 5].Number = (double)item.MonthlyExpenses;
            worksheet.Range[rowIndex, 5].NumberFormat = "$#,##0.00";
            worksheet.Range[rowIndex, 6].Number = (double)item.MonthlyBalance;
            worksheet.Range[rowIndex, 6].NumberFormat = "$#,##0.00";
            worksheet.Range[rowIndex, 7].Number = (double)item.BreakEvenRate;
            worksheet.Range[rowIndex, 7].NumberFormat = "$#,##0.00";
            worksheet.Range[rowIndex, 8].Text = item.Status;

            rowIndex++;
        }

        // Auto-fit columns
        worksheet.UsedRange.AutofitColumns();

        // Save workbook
        await using var stream = File.Create(filePath);
        workbook.SaveAs(stream, ExcelSaveType.SaveAsXLS);

        // Dispose resources
        workbook.Close();
        excelEngine.Dispose();
    }
}