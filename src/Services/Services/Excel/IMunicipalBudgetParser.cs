using System;
using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Interface for parsing municipal budget data from Excel files.
/// </summary>
public interface IMunicipalBudgetParser
{
    /// <summary>
    /// Parses municipal account data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed MunicipalAccount objects.</returns>
    List<MunicipalAccount> ParseAccounts(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Parses department data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed Department objects.</returns>
    List<Department> ParseDepartments(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Parses budget period data from Excel worksheet data.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values from the Excel worksheet.</param>
    /// <param name="worksheetName">Name of the worksheet being parsed.</param>
    /// <returns>List of parsed BudgetPeriod objects.</returns>
    List<BudgetPeriod> ParseBudgetPeriods(object[,] worksheetData, string worksheetName);

    /// <summary>
    /// Validates the structure of Excel data for municipal budget import.
    /// </summary>
    /// <param name="worksheetData">2D array of cell values to validate.</param>
    /// <param name="worksheetName">Name of the worksheet being validated.</param>
    /// <returns>Validation result with any errors found.</returns>
    ValidationResult ValidateWorksheetStructure(object[,] worksheetData, string worksheetName);
}

/// <summary>
/// Result of worksheet validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A successful ValidationResult.</returns>
    public static ValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new ValidationResult
        {
            IsValid = true,
            Warnings = warnings?.ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>A failed ValidationResult.</returns>
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? new List<string>()
        };
    }
}