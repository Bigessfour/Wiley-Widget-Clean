using System;
using System.Collections.Generic;
using System.IO;
using Syncfusion.XlsIO;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Interface for Excel file operations.
/// </summary>
public interface IExcelReaderService
{
    /// <summary>
    /// Reads data from an Excel file and returns it as a dictionary of worksheets.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <returns>Dictionary where key is worksheet name and value is 2D array of cell values.</returns>
    Dictionary<string, object[,]> ReadExcelFile(string filePath);

    /// <summary>
    /// Reads a specific worksheet from an Excel file.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <param name="worksheetName">Name of the worksheet to read.</param>
    /// <returns>2D array of cell values from the specified worksheet.</returns>
    object[,] ReadWorksheet(string filePath, string worksheetName);

    /// <summary>
    /// Gets all worksheet names from an Excel file.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <returns>List of worksheet names.</returns>
    List<string> GetWorksheetNames(string filePath);

    /// <summary>
    /// Validates if the file is a valid Excel file.
    /// </summary>
    /// <param name="filePath">Path to the file to validate.</param>
    /// <returns>True if the file is a valid Excel file, false otherwise.</returns>
    bool IsValidExcelFile(string filePath);
}