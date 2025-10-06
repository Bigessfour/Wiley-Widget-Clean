using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using Syncfusion.UI.Xaml.TreeGrid;
using Syncfusion.UI.Xaml.Grid;
using Syncfusion.XlsIO;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Microsoft.Win32;
using System.IO;
using System.Drawing;
using Syncfusion.Drawing;
using System.Linq;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing enterprise data export operations
/// Handles Excel, PDF, and CSV export functionality
/// </summary>
public partial class EnterpriseExportViewModel : AsyncViewModelBase
{
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Collection of enterprises to export
    /// </summary>
    public ThreadSafeObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseExportViewModel(
        IEnterpriseRepository enterpriseRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<EnterpriseViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Exports enterprises to Excel
    /// </summary>
    [RelayCommand]
    public async Task ExportToExcelAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create Excel engine and workbook directly
                    var excelEngine = new ExcelEngine();
                    var workBook = excelEngine.Excel.Workbooks.Create();
                    var worksheet = workBook.Worksheets.Create("Municipal Enterprises");

                    // Add header information
                    worksheet.Range["A1"].Text = "Municipal Enterprises Export";
                    worksheet.Range["A1"].CellStyle.Font.Bold = true;
                    worksheet.Range["A1"].CellStyle.Font.Size = 14;

                    worksheet.Range["A2"].Text = $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    worksheet.Range["A3"].Text = $"Total Enterprises: {enterprises.Count}";
                    worksheet.Range["A4"].Text = $"Total Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}";
                    worksheet.Range["A5"].Text = $"Total Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}";

                    // Add data headers
                    worksheet.Range["A7"].Text = "Name";
                    worksheet.Range["B7"].Text = "Type";
                    worksheet.Range["C7"].Text = "Status";
                    worksheet.Range["D7"].Text = "Citizen Count";
                    worksheet.Range["E7"].Text = "Current Rate";
                    worksheet.Range["F7"].Text = "Monthly Revenue";
                    worksheet.Range["G7"].Text = "Monthly Expenses";
                    worksheet.Range["H7"].Text = "Monthly Balance";
                    worksheet.Range["I7"].Text = "Break Even Rate";
                    worksheet.Range["J7"].Text = "Last Updated";

                    // Make headers bold
                    worksheet.Range["A7:J7"].CellStyle.Font.Bold = true;

                    // Add data rows
                    for (int i = 0; i < enterprises.Count; i++)
                    {
                        var enterprise = enterprises[i];
                        var row = i + 8; // Start from row 8
                        worksheet.Range[$"A{row}"].Text = enterprise.Name;
                        worksheet.Range[$"B{row}"].Text = enterprise.Type;
                        worksheet.Range[$"C{row}"].Text = enterprise.Status.ToString();
                        worksheet.Range[$"D{row}"].Number = enterprise.CitizenCount;
                        worksheet.Range[$"E{row}"].Number = (double)enterprise.CurrentRate;
                        worksheet.Range[$"F{row}"].Number = (double)enterprise.MonthlyRevenue;
                        worksheet.Range[$"G{row}"].Number = (double)enterprise.MonthlyExpenses;
                        worksheet.Range[$"H{row}"].Number = (double)enterprise.MonthlyBalance;
                        worksheet.Range[$"I{row}"].Number = (double)enterprise.BreakEvenRate;
                        worksheet.Range[$"J{row}"].Text = enterprise.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss");
                    }

                    // Auto-fit columns
                    worksheet.UsedRange.AutofitColumns();

                    // Save with file dialog
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx|Excel 97-2003 Files (*.xls)|*.xls",
                        FilterIndex = 1,
                        DefaultExt = "xlsx",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        workBook.SaveAs(saveFileDialog.FileName);
                        Logger.LogInformation("Excel export completed: {FileName}", saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to Excel");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to Excel...");
    }

    /// <summary>
    /// Exports enterprises to advanced Excel with templates, charts, and executive summary
    /// </summary>
    [RelayCommand]
    public async Task ExportToExcelAdvancedAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create Excel engine and workbook directly
                    var excelEngine = new ExcelEngine();
                    var workBook = excelEngine.Excel.Workbooks.Create();

                    // Create main data sheet
                    var mainSheet = workBook.Worksheets.Create("Enterprise Data");

                    // Add header information
                    mainSheet.Range["A1"].Text = "Enterprise Analysis Report";
                    mainSheet.Range["A1"].CellStyle.Font.Bold = true;
                    mainSheet.Range["A1"].CellStyle.Font.Size = 16;

                    mainSheet.Range["A2"].Text = $"Report Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                    mainSheet.Range["A3"].Text = $"Total Enterprises: {enterprises.Count}";

                    // Add data headers
                    mainSheet.Range["A5"].Text = "Name";
                    mainSheet.Range["B5"].Text = "Type";
                    mainSheet.Range["C5"].Text = "Status";
                    mainSheet.Range["D5"].Text = "Citizen Count";
                    mainSheet.Range["E5"].Text = "Current Rate";
                    mainSheet.Range["F5"].Text = "Monthly Revenue";
                    mainSheet.Range["G5"].Text = "Monthly Expenses";
                    mainSheet.Range["H5"].Text = "Monthly Balance";
                    mainSheet.Range["I5"].Text = "Break Even Rate";

                    // Make headers bold
                    mainSheet.Range["A5:I5"].CellStyle.Font.Bold = true;
                    mainSheet.Range["A5:I5"].CellStyle.Color = System.Drawing.Color.LightBlue;

                    // Add data rows
                    for (int i = 0; i < enterprises.Count; i++)
                    {
                        var enterprise = enterprises[i];
                        var row = i + 6; // Start from row 6
                        mainSheet.Range[$"A{row}"].Text = enterprise.Name;
                        mainSheet.Range[$"B{row}"].Text = enterprise.Type;
                        mainSheet.Range[$"C{row}"].Text = enterprise.Status.ToString();
                        mainSheet.Range[$"D{row}"].Number = enterprise.CitizenCount;
                        mainSheet.Range[$"E{row}"].Number = (double)enterprise.CurrentRate;
                        mainSheet.Range[$"F{row}"].Number = (double)enterprise.MonthlyRevenue;
                        mainSheet.Range[$"G{row}"].Number = (double)enterprise.MonthlyExpenses;
                        mainSheet.Range[$"H{row}"].Number = (double)enterprise.MonthlyBalance;
                        mainSheet.Range[$"I{row}"].Number = (double)enterprise.BreakEvenRate;
                    }

                    // Create summary sheet
                    var summarySheet = workBook.Worksheets.Create("Summary");
                    summarySheet.Range["A1"].Text = "Executive Summary";
                    summarySheet.Range["A1"].CellStyle.Font.Bold = true;
                    summarySheet.Range["A1"].CellStyle.Font.Size = 14;

                    summarySheet.Range["A3"].Text = "Total Enterprises:";
                    summarySheet.Range["B3"].Number = enterprises.Count;

                    summarySheet.Range["A4"].Text = "Total Revenue:";
                    summarySheet.Range["B4"].Number = (double)enterprises.Sum(e => e.MonthlyRevenue);

                    summarySheet.Range["A5"].Text = "Total Expenses:";
                    summarySheet.Range["B5"].Number = (double)enterprises.Sum(e => e.MonthlyExpenses);

                    summarySheet.Range["A6"].Text = "Net Balance:";
                    summarySheet.Range["B6"].Number = (double)enterprises.Sum(e => e.MonthlyBalance);

                    // Auto-fit columns
                    mainSheet.UsedRange.AutofitColumns();
                    summarySheet.UsedRange.AutofitColumns();

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        FileName = $"Enterprise_Analysis_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        workBook.SaveAs(saveFileDialog.FileName);
                        Logger.LogInformation("Advanced Excel export completed: {FileName}",
                                             saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error in advanced Excel export");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Creating advanced Excel report...");
    }

    /// <summary>
    /// Exports enterprises to CSV
    /// </summary>
    [RelayCommand]
    public async Task ExportToCsvAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "CSV Files (*.csv)|*.csv",
                        DefaultExt = "csv",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        using var writer = new StreamWriter(saveFileDialog.FileName);
                        // Write header
                        writer.WriteLine("ID,Name,Type,Status,CitizenCount,CurrentRate,MonthlyRevenue,MonthlyExpenses,MonthlyBalance,BreakEvenRate,LastUpdated,Description,Notes");

                        // Write data
                        foreach (var enterprise in enterprises)
                        {
                            writer.WriteLine($"{enterprise.Id},\"{enterprise.Name}\",\"{enterprise.Type}\",\"{enterprise.Status}\"," +
                                          $"{enterprise.CitizenCount},{enterprise.CurrentRate},{enterprise.MonthlyRevenue}," +
                                          $"{enterprise.MonthlyExpenses},{enterprise.MonthlyBalance},{enterprise.BreakEvenRate}," +
                                          $"{enterprise.LastUpdated:yyyy-MM-dd HH:mm:ss},\"{enterprise.Description}\",\"{enterprise.Notes}\"");
                        }

                        Logger.LogInformation("CSV export completed: {FileName}", saveFileDialog.FileName);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to CSV");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to CSV...");
    }

    /// <summary>
    /// Exports enterprises to PDF
    /// </summary>
    [RelayCommand]
    public async Task ExportToPdfAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create PDF document directly
                    var document = new PdfDocument();
                    var page = document.Pages.Add();

                    // Add header information
                    var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 16, PdfFontStyle.Bold);
                    page.Graphics.DrawString("Municipal Enterprises Report", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                    font = new PdfStandardFont(PdfFontFamily.TimesRoman, 12);
                    page.Graphics.DrawString($"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 35));
                    page.Graphics.DrawString($"Total Enterprises: {enterprises.Count}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 55));
                    page.Graphics.DrawString($"Total Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 75));
                    page.Graphics.DrawString($"Total Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 95));

                    // Create table for enterprise data
                    var table = new Syncfusion.Pdf.Grid.PdfGrid();
                    table.Columns.Add(5);

                    // Add headers
                    var headerRow = table.Rows.Add();
                    headerRow.Cells[0].Value = "Name";
                    headerRow.Cells[1].Value = "Type";
                    headerRow.Cells[2].Value = "Status";
                    headerRow.Cells[3].Value = "Revenue";
                    headerRow.Cells[4].Value = "Expenses";

                    // Make headers bold
                    foreach (var cell in headerRow.Cells)
                    {
                        ((PdfGridCell)cell).Style.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10, PdfFontStyle.Bold);
                    }

                    // Add data rows
                    foreach (var enterprise in enterprises)
                    {
                        var row = table.Rows.Add();
                        row.Cells[0].Value = enterprise.Name;
                        row.Cells[1].Value = enterprise.Type;
                        row.Cells[2].Value = enterprise.Status.ToString();
                        row.Cells[3].Value = $"${enterprise.MonthlyRevenue:N2}";
                        row.Cells[4].Value = $"${enterprise.MonthlyExpenses:N2}";
                    }

                    // Draw table on page
                    table.Draw(page, new Syncfusion.Drawing.PointF(10, 120));

                    // Save with file dialog
                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "PDF Files (*.pdf)|*.pdf",
                        DefaultExt = "pdf",
                        FileName = $"Municipal_Enterprises_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                        document.Close();
                        Logger.LogInformation("PDF export completed: {FileName}", saveFileDialog.FileName);
                    }
                    else
                    {
                        document.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error exporting to PDF");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Exporting to PDF...");
    }

    /// <summary>
    /// Exports enterprises to professional PDF report with headers, footers, and executive summary
    /// </summary>
    [RelayCommand]
    public async Task ExportToPdfReportAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var enterprises = Enterprises.ToList();
            if (!enterprises.Any())
            {
                Logger.LogWarning("No enterprises to export");
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    // Create PDF document directly
                    var document = new PdfDocument();

                    // Add executive summary page first
                    AddExecutiveSummaryPage(document, enterprises);

                    // Add data page
                    var dataPage = document.Pages.Add();

                    // Add header
                    var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 14, PdfFontStyle.Bold);
                    dataPage.Graphics.DrawString("Enterprise Data Report", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 10));

                    font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10);
                    dataPage.Graphics.DrawString($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 30));
                    dataPage.Graphics.DrawString($"Total Enterprises: {enterprises.Count}", font, PdfBrushes.Black, new Syncfusion.Drawing.PointF(10, 45));

                    // Create table for enterprise data
                    var table = new Syncfusion.Pdf.Grid.PdfGrid();
                    table.Columns.Add(6);

                    // Add headers
                    var headerRow = table.Rows.Add();
                    headerRow.Cells[0].Value = "Name";
                    headerRow.Cells[1].Value = "Type";
                    headerRow.Cells[2].Value = "Status";
                    headerRow.Cells[3].Value = "Citizens";
                    headerRow.Cells[4].Value = "Revenue";
                    headerRow.Cells[5].Value = "Expenses";

                    // Make headers bold
                    foreach (var cell in headerRow.Cells)
                    {
                        ((PdfGridCell)cell).Style.Font = new PdfStandardFont(PdfFontFamily.TimesRoman, 10, PdfFontStyle.Bold);
                    }

                    // Add data rows (limit to prevent page overflow)
                    var itemsToShow = enterprises.Take(50); // Limit for single page
                    foreach (var enterprise in itemsToShow)
                    {
                        var row = table.Rows.Add();
                        row.Cells[0].Value = enterprise.Name;
                        row.Cells[1].Value = enterprise.Type;
                        row.Cells[2].Value = enterprise.Status.ToString();
                        row.Cells[3].Value = enterprise.CitizenCount.ToString();
                        row.Cells[4].Value = $"${enterprise.MonthlyRevenue:N2}";
                        row.Cells[5].Value = $"${enterprise.MonthlyExpenses:N2}";
                    }

                    // Draw table on page
                    table.Draw(dataPage, new Syncfusion.Drawing.PointF(10, 65));

                    var saveFileDialog = new SaveFileDialog
                    {
                        Filter = "PDF Files (*.pdf)|*.pdf",
                        FileName = $"Enterprise_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        document.Save(saveFileDialog.FileName);
                        document.Close();
                        Logger.LogInformation("Professional PDF report completed: {FileName}",
                                             saveFileDialog.FileName);
                    }
                    else
                    {
                        document.Close();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error creating professional PDF report");
                    throw;
                }
            }, cancellationToken);
        }, statusMessage: "Generating professional PDF report...");
    }

    /// <summary>
    /// Adds executive summary page to PDF document
    /// </summary>
    private void AddExecutiveSummaryPage(PdfDocument document, List<Enterprise> enterprises)
    {
        var page = document.Pages.Add();
        var font = new PdfStandardFont(PdfFontFamily.TimesRoman, 18, PdfFontStyle.Bold);
        var subFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 14, PdfFontStyle.Bold);
        var normalFont = new PdfStandardFont(PdfFontFamily.TimesRoman, 12);

        float yPosition = 50;

        // Title
        page.Graphics.DrawString("Executive Summary", font, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 40;

        // Key Metrics
        page.Graphics.DrawString("Key Financial Metrics", subFont, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 30;

        var metrics = new[]
        {
            $"Total Enterprises: {enterprises.Count}",
            $"Active Enterprises: {enterprises.Count(e => e.Status == EnterpriseStatus.Active)}",
            $"Total Citizens Served: {enterprises.Sum(e => e.CitizenCount):N0}",
            $"Total Monthly Revenue: ${enterprises.Sum(e => e.MonthlyRevenue):N2}",
            $"Total Monthly Expenses: ${enterprises.Sum(e => e.MonthlyExpenses):N2}",
            $"Net Monthly Balance: ${enterprises.Sum(e => e.MonthlyBalance):N2}",
            $"Average Rate: ${enterprises.Average(e => e.CurrentRate):N2}"
        };

        foreach (var metric in metrics)
        {
            page.Graphics.DrawString(metric, normalFont, PdfBrushes.Black,
                                   new Syncfusion.Drawing.PointF(70, yPosition));
            yPosition += 20;
        }

        yPosition += 20;

        // Enterprise Type Breakdown
        page.Graphics.DrawString("Enterprise Type Breakdown", subFont, PdfBrushes.Black,
                               new Syncfusion.Drawing.PointF(50, yPosition));
        yPosition += 30;

        var typeBreakdown = enterprises.GroupBy(e => e.Type)
            .Select(g => $"{g.Key}: {g.Count()} enterprises")
            .ToArray();

        foreach (var breakdown in typeBreakdown)
        {
            page.Graphics.DrawString(breakdown, normalFont, PdfBrushes.Black,
                                   new Syncfusion.Drawing.PointF(70, yPosition));
            yPosition += 20;
        }
    }
}