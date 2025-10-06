using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;

namespace ExcelAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Excel Spreadsheet Analyzer for Wiley Widget");
            Console.WriteLine("==========================================");

            // Create a simple logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger("ExcelAnalyzer");

            // Analyze both spreadsheets
            AnalyzeSpreadsheet(logger, @"c:\Users\biges\Desktop\Wiley_Widget\TOW WORKING COPY TOWN OF WILEY 2026 BUDGET.xls", "Town of Wiley");
            AnalyzeSpreadsheet(logger, @"c:\Users\biges\Desktop\Wiley_Widget\WSD WORKING COPY WILEY SANITATION DISTRICT 2026 BUDGET.xls", "Wiley Sanitation District");

            Console.WriteLine("\nAnalysis complete. Press any key to exit.");
            Console.ReadKey();
        }

        static void AnalyzeSpreadsheet(ILogger logger, string filePath, string name)
        {
            Console.WriteLine($"\n=== Analyzing {name} Spreadsheet ===");
            Console.WriteLine($"File: {Path.GetFileName(filePath)}");

            try
            {
                using (var excelEngine = new ExcelEngine())
                {
                    var application = excelEngine.Excel;
                    application.DefaultVersion = ExcelVersion.Xlsx;

                    var workbook = application.Workbooks.Open(filePath);
                    try
                    {
                        Console.WriteLine($"Worksheets found: {workbook.Worksheets.Count}");
                        foreach (var worksheet in workbook.Worksheets)
                        {
                            Console.WriteLine($"  - {worksheet.Name}");
                        }

                        // Focus on key budget worksheets - check TOW first
                        var keyWorksheets = new[] { "GF SUMM", "WATER&ADM", "ENTERPRISE", "CON SUMM", "HWY&ST", "GEN GOVT", "TAX REV" };
                        foreach (var worksheet in workbook.Worksheets)
                        {
                            if (keyWorksheets.Contains(worksheet.Name.ToUpper()) ||
                                worksheet.Name.ToUpper().Contains("SUMM") ||
                                worksheet.Name.ToUpper().Contains("WATER") ||
                                worksheet.Name.ToUpper().Contains("ENTERPRISE"))
                            {
                                AnalyzeWorksheet(logger, worksheet, name);
                            }
                        }
                    }
                    finally
                    {
                        workbook.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing {name}: {ex.Message}");
            }
        }

        static void AnalyzeWorksheet(ILogger logger, IWorksheet worksheet, string spreadsheetName)
        {
            Console.WriteLine($"\n--- Worksheet: {worksheet.Name} ---");

            try
            {
                // Get the used range of the worksheet
                var usedRange = worksheet.UsedRange;
                if (usedRange == null)
                {
                    Console.WriteLine("No data found in worksheet");
                    return;
                }

                var lastRow = usedRange.LastRow;
                var lastColumn = usedRange.LastColumn;

                Console.WriteLine($"Dimensions: {lastRow} rows x {lastColumn} columns");

                // Show first few rows to understand structure
                int rowsToShow = Math.Min(25, lastRow); // Increased from 15
                int colsToShow = Math.Min(10, lastColumn); // Increased from 8

                Console.WriteLine("First few rows:");
                for (int row = 1; row <= rowsToShow; row++)
                {
                    Console.Write($"Row {row}: ");
                    for (int col = 1; col <= colsToShow; col++)
                    {
                        try
                        {
                            var cellValue = worksheet.Range[row, col].Value?.ToString() ?? "NULL";
                            if (cellValue.Length > 15)
                                cellValue = cellValue.Substring(0, 12) + "...";
                            Console.Write($"[{cellValue}] ");
                        }
                        catch
                        {
                            Console.Write("[ERROR] ");
                        }
                    }
                    Console.WriteLine();
                }

                // Look for headers and data patterns
                AnalyzeWorksheetStructure(worksheet, lastRow, lastColumn);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error analyzing worksheet {worksheet.Name}");
                Console.WriteLine($"Error analyzing worksheet: {ex.Message}");
            }
        }

        static void AnalyzeWorksheetStructure(IWorksheet worksheet, int lastRow, int lastColumn)
        {
            Console.WriteLine("Structure Analysis:");

            // Check for header row (first row)
            bool hasHeaders = false;
            if (lastRow > 0)
            {
                int textCells = 0;
                for (int col = 1; col <= Math.Min(lastColumn, 5); col++)
                {
                    var cellValue = worksheet.Range[1, col].Value?.ToString();
                    if (!string.IsNullOrEmpty(cellValue) && !double.TryParse(cellValue, out _))
                    {
                        textCells++;
                    }
                }
                hasHeaders = textCells >= 2;
            }

            Console.WriteLine($"  - Appears to have headers: {hasHeaders}");

            // Count numeric vs text cells in first few rows
            int numericCells = 0;
            int textCellCount = 0;
            int totalCells = 0;

            int startRow = hasHeaders ? 2 : 1;
            for (int row = startRow; row <= Math.Min(lastRow, startRow + 4); row++)
            {
                for (int col = 1; col <= Math.Min(lastColumn, 10); col++)
                {
                    totalCells++;
                    var cellValue = worksheet.Range[row, col].Value?.ToString();
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        if (double.TryParse(cellValue, out _))
                            numericCells++;
                        else
                            textCellCount++;
                    }
                }
            }

            Console.WriteLine($"  - Sample data cells: {totalCells} total, {numericCells} numeric, {textCellCount} text");

            // Look for account number patterns
            Console.WriteLine("  - Account number patterns found:");
            var accountPatterns = new HashSet<string>();
            for (int row = 1; row <= Math.Min(lastRow, 100); row++) // Increased from 50
            {
                for (int col = 1; col <= Math.Min(lastColumn, 5); col++)
                {
                    var cellValue = worksheet.Range[row, col].Value?.ToString();
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        // Look for patterns like "405", "405.1", "410.2.1"
                        if (System.Text.RegularExpressions.Regex.IsMatch(cellValue, @"^\d+(\.\d+)*$"))
                        {
                            accountPatterns.Add(cellValue);
                        }
                    }
                }
            }

            foreach (var pattern in accountPatterns)
            {
                Console.WriteLine($"    * {pattern}");
            }

            // Look for department/section headers
            Console.WriteLine("  - Department/Section patterns found:");
            var deptPatterns = new HashSet<string>();
            for (int row = 1; row <= Math.Min(lastRow, 100); row++) // Increased from 50
            {
                for (int col = 1; col <= Math.Min(lastColumn, 3); col++)
                {
                    var cellValue = worksheet.Range[row, col].Value?.ToString();
                    if (!string.IsNullOrEmpty(cellValue) && cellValue.Length > 3)
                    {
                        // Look for department names (all caps, specific patterns)
                        if (cellValue == cellValue.ToUpper() &&
                            (cellValue.Contains("DEPT") || cellValue.Contains("GENERAL") ||
                             cellValue.Contains("HIGHWAY") || cellValue.Contains("WATER") ||
                             cellValue.Contains("SEWER") || cellValue.Contains("SANITATION") ||
                             cellValue.Contains("ADMIN") || cellValue.Contains("MAINT")))
                        {
                            deptPatterns.Add(cellValue);
                        }
                    }
                }
            }

            foreach (var pattern in deptPatterns)
            {
                Console.WriteLine($"    * {pattern}");
            }

            // Look for multi-year columns (Prior, Current, Budget)
            Console.WriteLine("  - Multi-year column patterns found:");
            var yearPatterns = new HashSet<string>();
            if (hasHeaders && lastRow > 0)
            {
                for (int col = 1; col <= Math.Min(lastColumn, 10); col++)
                {
                    var headerValue = worksheet.Range[1, col].Value?.ToString();
                    if (!string.IsNullOrEmpty(headerValue))
                    {
                        var upperHeader = headerValue.ToUpper();
                        if (upperHeader.Contains("PRIOR") || upperHeader.Contains("CURRENT") ||
                            upperHeader.Contains("BUDGET") || upperHeader.Contains("2025") ||
                            upperHeader.Contains("2026") || upperHeader.Contains("ESTIMATE"))
                        {
                            yearPatterns.Add($"{col}: {headerValue}");
                        }
                    }
                }
            }

            foreach (var pattern in yearPatterns)
            {
                Console.WriteLine($"    * {pattern}");
            }

            // Analyze hierarchical structure
            Console.WriteLine("  - Hierarchical analysis:");
            var hierarchicalAccounts = new List<string>();
            for (int row = startRow; row <= Math.Min(lastRow, startRow + 50); row++) // Increased from 30
            {
                var firstCell = worksheet.Range[row, 1].Value?.ToString();
                if (!string.IsNullOrEmpty(firstCell) &&
                    System.Text.RegularExpressions.Regex.IsMatch(firstCell, @"^\d+(\.\d+)*$"))
                {
                    var parts = firstCell.Split('.');
                    if (parts.Length > 1)
                    {
                        hierarchicalAccounts.Add(firstCell);
                    }
                }
            }

            Console.WriteLine($"    * Found {hierarchicalAccounts.Count} hierarchical accounts");
            if (hierarchicalAccounts.Count > 0)
            {
                Console.WriteLine($"    * Sample hierarchical: {string.Join(", ", hierarchicalAccounts)}");
            }
        }
    }
}
