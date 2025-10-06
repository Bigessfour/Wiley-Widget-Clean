using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using Syncfusion.XlsIO;

namespace WileyWidget.Services;

/// <summary>
/// Provides concrete export functionality for report data using Syncfusion document components.
/// </summary>
public sealed class ReportExportService : IReportExportService
{
    private readonly ILogger<ReportExportService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExportService"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    public ReportExportService(ILogger<ReportExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ExportToPdfAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        ValidateFilePath(filePath);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = new PdfDocument();
            var page = document.Pages.Add();
            var graphics = page.Graphics;
            var headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 16f, PdfFontStyle.Bold);
            var bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10f);

            graphics.DrawString("Wiley Widget Enterprise Report", headerFont, PdfBrushes.DarkBlue, 0, 0);

            var subtitle = $"Generated: {DateTime.Now:G}\nFilter: {reportData.Filter}";
            graphics.DrawString(subtitle, bodyFont, PdfBrushes.Black, 0, 28);

            var tableData = reportData.Enterprises.Select(metric => new
            {
                Enterprise = metric.Name,
                Revenue = metric.Revenue.ToString("C", CultureInfo.CurrentCulture),
                Expenses = metric.Expenses.ToString("C", CultureInfo.CurrentCulture),
                ROI = metric.RoiPercentage.ToString("N2", CultureInfo.CurrentCulture) + "%",
                Margin = metric.ProfitMarginPercentage.ToString("N2", CultureInfo.CurrentCulture) + "%"
            }).ToList();

            var grid = new PdfGrid
            {
                DataSource = tableData
            };

            grid.Headers[0].Cells[0].StringFormat = new PdfStringFormat { Alignment = PdfTextAlignment.Left };
            grid.Style.Font = bodyFont;
            grid.Draw(page.Graphics, new Syncfusion.Drawing.RectangleF(0, 60, page.GetClientSize().Width - 20, page.GetClientSize().Height - 80));

            document.Save(filePath);
            document.Close(true);

            _logger.LogInformation("Report exported to PDF at {FilePath}", filePath);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExportToExcelAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        ValidateFilePath(filePath);

        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var excelEngine = new ExcelEngine();
            var application = excelEngine.Excel;
            application.DefaultVersion = ExcelVersion.Xlsx;

            var workbook = application.Workbooks.Create(1);
            var worksheet = workbook.Worksheets[0];
            worksheet.Name = "Enterprise Report";

            worksheet.Range["A1"].Text = "Wiley Widget Enterprise Report";
            worksheet.Range["A1"].CellStyle.Font.Bold = true;
            worksheet.Range["A1"].CellStyle.Font.Size = 16;

            worksheet.Range["A2"].Text = $"Generated: {DateTime.Now:G}";
            worksheet.Range["A3"].Text = $"Filter: {reportData.Filter}";

            var tableData = reportData.Enterprises.Select(metric => new
            {
                metric.Name,
                Revenue = metric.Revenue,
                Expenses = metric.Expenses,
                metric.RoiPercentage,
                metric.ProfitMarginPercentage
            }).ToList();

            worksheet.ImportData(tableData, 5, 1, false);
            worksheet.AutoFilters.FilterRange = worksheet.Range[5, 1, 4 + tableData.Count, 5];
            worksheet.UsedRange.AutofitColumns();

            workbook.SaveAs(filePath);
            _logger.LogInformation("Report exported to Excel at {FilePath}", filePath);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ExportToRdlAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportData);
        ValidateFilePath(filePath);

        var rdlTemplate = ReportDefinitionTemplateBuilder.Build(reportData);
        await File.WriteAllTextAsync(filePath, rdlTemplate, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Report exported to RDL at {FilePath}", filePath);
    }

    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static class ReportDefinitionTemplateBuilder
    {
        public static string Build(ReportDataModel reportData)
        {
            var rows = string.Join(Environment.NewLine, reportData.Enterprises.Select(metric =>
                $"          <Details>\n            <TableCells>\n              <TableCell>\n                <ReportItems><Textbox Name=\"Enterprise\"><Value>{Escape(metric.Name)}</Value></Textbox></ReportItems>\n              </TableCell>\n              <TableCell>\n                <ReportItems><Textbox Name=\"Revenue\"><Value>{metric.Revenue}</Value></Textbox></ReportItems>\n              </TableCell>\n              <TableCell>\n                <ReportItems><Textbox Name=\"Expenses\"><Value>{metric.Expenses}</Value></Textbox></ReportItems>\n              </TableCell>\n              <TableCell>\n                <ReportItems><Textbox Name=\"ROI\"><Value>{metric.RoiPercentage}</Value></Textbox></ReportItems>\n              </TableCell>\n              <TableCell>\n                <ReportItems><Textbox Name=\"Margin\"><Value>{metric.ProfitMarginPercentage}</Value></Textbox></ReportItems>\n              </TableCell>\n            </TableCells>\n          </Details>"));

            return $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Report xmlns=\"http://schemas.microsoft.com/sqlserver/reporting/2008/01/reportdefinition\" xmlns:rd=\"http://schemas.microsoft.com/SQLServer/reporting/reportdesigner\">\n  <AutoRefresh>0</AutoRefresh>\n  <Body>\n    <Height>8.5in</Height>\n    <ReportItems>\n      <Tablix Name=\"EnterpriseReport\">\n        <TablixBody>\n          <TablixColumns>\n            {string.Join(Environment.NewLine, Enumerable.Repeat("<TablixColumn><Width>1.5in</Width></TablixColumn>", 5))}\n          </TablixColumns>\n          <TablixRows>\n            {rows}\n          </TablixRows>\n        </TablixBody>\n        <DataSetName>Enterprises</DataSetName>\n      </Tablix>\n    </ReportItems>\n  </Body>\n  <DataSets>\n    <DataSet Name=\"Enterprises\">\n      <Fields>\n        <Field Name=\"Enterprise\"><DataField>Enterprise</DataField><rd:TypeName>String</rd:TypeName></Field>\n        <Field Name=\"Revenue\"><DataField>Revenue</DataField><rd:TypeName>Decimal</rd:TypeName></Field>\n        <Field Name=\"Expenses\"><DataField>Expenses</DataField><rd:TypeName>Decimal</rd:TypeName></Field>\n        <Field Name=\"ROI\"><DataField>ROI</DataField><rd:TypeName>Decimal</rd:TypeName></Field>\n        <Field Name=\"Margin\"><DataField>Margin</DataField><rd:TypeName>Decimal</rd:TypeName></Field>\n      </Fields>\n    </DataSet>\n  </DataSets>\n  <rd:ReportUnitType>Inch</rd:ReportUnitType>\n  <rd:ReportID>{Guid.NewGuid()}</rd:ReportID>\n</Report>";
        }

        private static string Escape(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
        }
    }
}
