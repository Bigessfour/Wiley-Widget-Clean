using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Service for reading Excel files using Syncfusion XlsIO.
/// </summary>
public class ExcelReaderService : IExcelReaderService
{
    private readonly ILogger<ExcelReaderService> _logger;

    /// <summary>
    /// Initializes a new instance of the ExcelReaderService class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    public ExcelReaderService(ILogger<ExcelReaderService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public Dictionary<string, object[,]> ReadExcelFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found.", filePath);

        if (!IsValidExcelFile(filePath))
            throw new InvalidOperationException("File is not a valid Excel file.");

        var result = new Dictionary<string, object[,]>();

        try
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Open(filePath);
                try
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        var data = ReadWorksheetData(worksheet);
                        result[worksheet.Name] = data;
                    }
                }
                finally
                {
                    workbook.Close();
                }
            }

            _logger.LogInformation("Successfully read Excel file: {FilePath} with {WorksheetCount} worksheets",
                filePath, result.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Excel file: {FilePath}", filePath);
            throw;
        }

        return result;
    }

    /// <inheritdoc/>
    public object[,] ReadWorksheet(string filePath, string worksheetName)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(worksheetName);
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        if (string.IsNullOrEmpty(worksheetName))
            throw new ArgumentException("Worksheet name cannot be null or empty.", nameof(worksheetName));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel file not found.", filePath);

        try
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Open(filePath);
                try
                {
                    var worksheet = workbook.Worksheets[worksheetName];
                    if (worksheet == null)
                    {
                        throw new ArgumentException($"Worksheet '{worksheetName}' not found in Excel file.", nameof(worksheetName));
                    }

                    var data = ReadWorksheetData(worksheet);
                    _logger.LogInformation("Successfully read worksheet '{WorksheetName}' from file: {FilePath}",
                        worksheetName, filePath);

                    return data;
                }
                finally
                {
                    workbook.Close();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading worksheet '{WorksheetName}' from file: {FilePath}",
                worksheetName, filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public List<string> GetWorksheetNames(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var worksheetNames = new List<string>();

        try
        {
            using (var excelEngine = new ExcelEngine())
            {
                var application = excelEngine.Excel;
                application.DefaultVersion = ExcelVersion.Xlsx;

                var workbook = application.Workbooks.Open(filePath);
                try
                {
                    foreach (var worksheet in workbook.Worksheets)
                    {
                        worksheetNames.Add(worksheet.Name);
                    }
                }
                finally
                {
                    workbook.Close();
                }
            }

            _logger.LogInformation("Found {WorksheetCount} worksheets in file: {FilePath}",
                worksheetNames.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting worksheet names from file: {FilePath}", filePath);
            throw;
        }

        return worksheetNames;
    }

    /// <inheritdoc/>
    public bool IsValidExcelFile(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        if (string.IsNullOrEmpty(filePath))
            return false;

        if (!File.Exists(filePath))
            return false;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension == ".xlsx" || extension == ".xls" || extension == ".xlsm";
    }

    /// <summary>
    /// Reads all data from a worksheet into a 2D object array.
    /// </summary>
    /// <param name="worksheet">The worksheet to read from.</param>
    /// <returns>2D array containing all cell values from the worksheet.</returns>
    private object[,] ReadWorksheetData(IWorksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);

        // Get the used range of the worksheet
        var usedRange = worksheet.UsedRange;
        if (usedRange == null)
            return new object[0, 0];

        var lastRow = usedRange.LastRow;
        var lastColumn = usedRange.LastColumn;

        // Create a 2D array to hold the data (1-based indexing to match Excel)
        var data = new object[lastRow, lastColumn];

        // Read data from each cell
        for (int row = 1; row <= lastRow; row++)
        {
            for (int col = 1; col <= lastColumn; col++)
            {
                try
                {
                    var cellValue = worksheet.Range[row, col].Value;
                    data[row - 1, col - 1] = cellValue ?? string.Empty; // Convert to 0-based indexing
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading cell [{Row},{Column}] in worksheet '{WorksheetName}'",
                        row, col, worksheet.Name);
                    data[row - 1, col - 1] = string.Empty;
                }
            }
        }

        _logger.LogDebug("Read {RowCount} rows and {ColumnCount} columns from worksheet '{WorksheetName}'",
            lastRow, lastColumn, worksheet.Name);

        return data;
    }
}