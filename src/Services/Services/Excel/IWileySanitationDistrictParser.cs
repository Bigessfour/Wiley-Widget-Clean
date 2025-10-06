using System;
using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Interface for parsing Wiley Sanitation District (WSD) budget data from Excel files.
/// Handles the specific format used by the Wiley Sanitation District spreadsheets.
/// </summary>
public interface IWileySanitationDistrictParser
{
    /// <summary>
    /// Parses WSD account data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed MunicipalAccount objects specific to WSD format.</returns>
    List<MunicipalAccount> ParseWsdAccounts(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Parses WSD department data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed Department objects specific to WSD.</returns>
    List<Department> ParseWsdDepartments(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Parses WSD budget period data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed BudgetPeriod objects for WSD.</returns>
    List<BudgetPeriod> ParseWsdBudgetPeriods(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Validates the structure of WSD Excel data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values to validate.</param>
    /// <param name="worksheetName">Name of the worksheet being validated.</param>
    /// <returns>Validation result with any errors found specific to WSD format.</returns>
    ValidationResult ValidateWsdWorksheetStructure(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Determines if the worksheet follows the Wiley Sanitation District format.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values to check.</param>
    /// <returns>True if the worksheet matches WSD format, false otherwise.</returns>
    bool IsWileySanitationDistrictFormat(object[,] worksheetData);
}