using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Interface for importing budget data from various sources.
/// </summary>
public interface IBudgetImporter
{
    /// <summary>
    /// Imports budget data from a stream and returns the import result.
    /// </summary>
    /// <param name="excelStream">Stream containing the Excel file data.</param>
    /// <param name="options">Import options to control the import process.</param>
    /// <returns>Task that completes with the import result.</returns>
    Task<ImportResult> ImportBudgetAsync(Stream excelStream, BudgetImportOptions options);

    /// <summary>
    /// Imports budget data from a file path and returns the import result.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <param name="options">Import options to control the import process.</param>
    /// <returns>Task that completes with the import result.</returns>
    Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options);

    /// <summary>
    /// Imports budget data from a file path with progress reporting.
    /// </summary>
    /// <param name="filePath">Path to the Excel file.</param>
    /// <param name="options">Import options to control the import process.</param>
    /// <param name="progress">Progress reporter for import operations.</param>
    /// <returns>Task that completes with the import result.</returns>
    Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options, IProgress<ImportProgress> progress);
}

/// <summary>
/// Options for controlling the budget import process.
/// </summary>
public class BudgetImportOptions
{
    /// <summary>
    /// Gets or sets whether this is a preview-only import.
    /// When true, data is parsed and validated but not saved to the database.
    /// </summary>
    public bool PreviewOnly { get; set; } = false;

    /// <summary>
    /// Gets or sets the budget period to associate imported accounts with.
    /// If null, a new budget period will be created.
    /// </summary>
    public int? BudgetPeriodId { get; set; }

    /// <summary>
    /// Gets or sets the default fund type to use when fund cannot be determined from worksheet name.
    /// </summary>
    public FundType DefaultFundType { get; set; } = FundType.General;

    /// <summary>
    /// Gets or sets whether to skip validation errors and continue importing valid data.
    /// </summary>
    public bool SkipValidationErrors { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of validation errors to allow before stopping the import.
    /// </summary>
    public int MaxValidationErrors { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to validate GASB compliance during import.
    /// </summary>
    public bool ValidateGASBCompliance { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create a new budget period for the import.
    /// </summary>
    public bool CreateNewBudgetPeriod { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to overwrite existing accounts with the same account number.
    /// </summary>
    public bool OverwriteExistingAccounts { get; set; } = false;

    /// <summary>
    /// Gets or sets the budget year for the import.
    /// </summary>
    public int BudgetYear { get; set; }

    /// <summary>
    /// Gets or sets the list of worksheet names to import. If empty, all worksheets will be processed.
    /// </summary>
    public List<string> WorksheetsToImport { get; set; } = new();
}

/// <summary>
/// Result of a budget import operation.
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Gets or sets whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the list of successfully imported accounts.
    /// </summary>
    public List<MunicipalAccount> Accounts { get; set; } = new List<MunicipalAccount>();

    /// <summary>
    /// Gets or sets the list of successfully imported departments.
    /// </summary>
    public List<Department> Departments { get; set; } = new List<Department>();

    /// <summary>
    /// Gets or sets the list of validation errors encountered during import.
    /// </summary>
    public List<string> Errors { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the list of validation warnings encountered during import.
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the preview data for the import.
    /// </summary>
    public List<dynamic> PreviewData { get; set; } = new List<dynamic>();

    /// <summary>
    /// Gets or sets the detected format of the imported file.
    /// </summary>
    public string DetectedFormat { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of rows successfully imported.
    /// </summary>
    public int RowsImported { get; set; }

    /// <summary>
    /// Gets or sets the total number of rows processed.
    /// </summary>
    public int TotalRowsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the budget period that was created or used for the import.
    /// </summary>
    public BudgetPeriod? BudgetPeriod { get; set; }

    /// <summary>
    /// Gets or sets the duration of the import operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Creates a successful import result.
    /// </summary>
    /// <param name="accountsImported">Number of accounts successfully imported.</param>
    /// <returns>A successful ImportResult.</returns>
    public static ImportResult CreateSuccess(int accountsImported)
    {
        return new ImportResult
        {
            Success = true,
            RowsImported = accountsImported,
            TotalRowsProcessed = accountsImported
        };
    }

    /// <summary>
    /// Creates a failed import result.
    /// </summary>
    /// <param name="errors">List of error messages.</param>
    /// <returns>A failed ImportResult.</returns>
    public static ImportResult Failure(IEnumerable<string> errors)
    {
        return new ImportResult
        {
            Success = false,
            Errors = new List<string>(errors)
        };
    }
}

/// <summary>
/// Progress information for import operations.
/// </summary>
public class ImportProgress
{
    /// <summary>
    /// Gets or sets the current operation being performed.
    /// </summary>
    public string CurrentOperation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current stage of the import process.
    /// </summary>
    public string Stage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Gets or sets the percentage complete (0-100).
    /// </summary>
    public int PercentComplete { get; set; }

    /// <summary>
    /// Gets or sets the number of items processed so far.
    /// </summary>
    public int ItemsProcessed { get; set; }

    /// <summary>
    /// Gets or sets the total number of items to process.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets any current status message.
    /// </summary>
    public string StatusMessage { get; set; } = string.Empty;
}