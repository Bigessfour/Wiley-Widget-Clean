using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Implementation of IBudgetImporter using OpenXML for Excel file processing.
/// </summary>
public class ExcelBudgetImporter : IBudgetImporter
{
    private readonly ILogger<ExcelBudgetImporter> _logger;
    private readonly IMunicipalBudgetParser _parser;

    /// <summary>
    /// Initializes a new instance of the ExcelBudgetImporter class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    /// <param name="parser">Parser for municipal budget data.</param>
    public ExcelBudgetImporter(ILogger<ExcelBudgetImporter> logger, IMunicipalBudgetParser parser)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <inheritdoc/>
    public async Task<ImportResult> ImportBudgetAsync(Stream excelStream, BudgetImportOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ImportResult();

        try
        {
            _logger.LogInformation("Starting budget import from Excel stream");

            // Parse worksheets from the Excel file
            var budgetData = await ParseExcelWorksheetsAsync(excelStream);

            // Process the budget data
            result = await ProcessBudgetDataAsync(budgetData, options);

            _logger.LogInformation("Budget import completed. Success: {Success}, Accounts: {Accounts}, Errors: {Errors}",
                result.Success, result.Accounts.Count, result.Errors.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during budget import");
            result = ImportResult.Failure(new[] { $"Import failed: {ex.Message}" });
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options)
    {
        using var stream = File.OpenRead(filePath);
        return await ImportBudgetAsync(stream, options);
    }

    /// <inheritdoc/>
    public async Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options, IProgress<ImportProgress> progress)
    {
        using var stream = File.OpenRead(filePath);
        return await ImportBudgetAsync(stream, options);
    }

    /// <summary>
    /// Parses Excel worksheets and returns budget data organized by worksheet name.
    /// </summary>
    /// <param name="excelStream">Stream containing the Excel file.</param>
    /// <returns>Dictionary of worksheet data keyed by worksheet name.</returns>
    private async Task<Dictionary<string, object[,]>> ParseExcelWorksheetsAsync(Stream excelStream)
    {
        return await Task.Run(() =>
        {
            var budgetData = new Dictionary<string, object[,]>();

            using var document = SpreadsheetDocument.Open(excelStream, false);
            var workbookPart = document.WorkbookPart;

            if (workbookPart == null)
            {
                throw new InvalidOperationException("Invalid Excel file: no workbook part found");
            }

            var worksheets = workbookPart.Workbook.Descendants<Sheet>();

            foreach (var sheet in worksheets)
            {
                var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id!);
                var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();

                var worksheetData = ParseWorksheet(sheetData);
                budgetData[sheet.Name!] = worksheetData;

                _logger.LogDebug("Parsed worksheet: {WorksheetName}, Rows: {Rows}, Columns: {Columns}",
                    sheet.Name, worksheetData.GetLength(0), worksheetData.GetLength(1));
            }

            return budgetData;
        });
    }

    /// <summary>
    /// Parses a worksheet's SheetData into a 2D object array.
    /// </summary>
    /// <param name="sheetData">The SheetData to parse.</param>
    /// <returns>2D array of cell values.</returns>
    private object[,] ParseWorksheet(SheetData sheetData)
    {
        var rows = sheetData.Elements<Row>().ToList();
        if (!rows.Any())
        {
            return new object[0, 0];
        }

        // Find the maximum number of columns
        int maxColumns = rows.Max(r => r.Elements<Cell>().Count());

        var worksheetData = new object[rows.Count, maxColumns];

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var cells = row.Elements<Cell>().ToList();

            for (int colIndex = 0; colIndex < cells.Count && colIndex < maxColumns; colIndex++)
            {
                var cell = cells[colIndex];
                worksheetData[rowIndex, colIndex] = GetCellValue(cell);
            }
        }

        return worksheetData;
    }

    /// <summary>
    /// Gets the value of a cell, handling different cell types.
    /// </summary>
    /// <param name="cell">The cell to get the value from.</param>
    /// <returns>The cell value as an object, or empty string if the cell is null.</returns>
    private object GetCellValue(Cell cell)
    {
        if (cell == null)
            return string.Empty;

        var value = cell.InnerText;

        if (cell.DataType != null)
        {
            var dataType = cell.DataType.Value.ToString();
            switch (dataType)
            {
                case "SharedString":
                    // Handle shared strings
                    return value; // Would need SharedStringTable for full implementation
                case "Boolean":
                    return value == "1";
                case "Number":
                    if (double.TryParse(value, out var number))
                        return number;
                    break;
                case "Date":
                    if (DateTime.TryParse(value, out var date))
                        return date;
                    break;
            }
        }

        // Try to parse as number first, then return as string
        if (double.TryParse(value, out var doubleValue))
            return doubleValue;

        return value;
    }

    /// <summary>
    /// Processes parsed budget data into MunicipalAccount objects.
    /// </summary>
    /// <param name="budgetData">Dictionary of worksheet data.</param>
    /// <param name="options">Import options.</param>
    /// <returns>ImportResult with processed data.</returns>
    private Task<ImportResult> ProcessBudgetDataAsync(Dictionary<string, object[,]> budgetData, BudgetImportOptions options)
    {
        var result = new ImportResult();
        var allAccounts = new List<MunicipalAccount>();
        var allDepartments = new List<Department>();
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        foreach (var (worksheetName, worksheetData) in budgetData)
        {
            try
            {
                // Validate worksheet structure
                var validationResult = _parser.ValidateWorksheetStructure(worksheetData, worksheetName);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors.Select(e => $"{worksheetName}: {e}");
                    allErrors.AddRange(errors);

                    if (!options.SkipValidationErrors)
                    {
                        continue; // Skip this worksheet
                    }
                }

                // Add warnings
                allWarnings.AddRange(validationResult.Warnings.Select(w => $"{worksheetName}: {w}"));

                // Parse accounts
                var accounts = _parser.ParseAccounts(worksheetData, worksheetName);
                allAccounts.AddRange(accounts);

                // Parse departments
                var departments = _parser.ParseDepartments(worksheetData, worksheetName);
                allDepartments.AddRange(departments);

                result.TotalRowsProcessed += worksheetData.GetLength(0);

                _logger.LogInformation("Processed worksheet {WorksheetName}: {Accounts} accounts, {Departments} departments",
                    worksheetName, accounts.Count, departments.Count);
            }
            catch (Exception ex)
            {
                var error = $"Error processing worksheet {worksheetName}: {ex.Message}";
                allErrors.Add(error);
                _logger.LogError(ex, "Error processing worksheet {WorksheetName}", worksheetName);

                if (!options.SkipValidationErrors && allErrors.Count >= options.MaxValidationErrors)
                {
                    break; // Stop processing if too many errors
                }
            }
        }

        // Set result properties
        result.Accounts = allAccounts;
        result.Departments = allDepartments;
        result.Errors = allErrors;
        result.Warnings = allWarnings;
        result.RowsImported = allAccounts.Count;
        result.Success = !allErrors.Any() || (options.SkipValidationErrors && allAccounts.Any());

        return Task.FromResult(result);
    }
}