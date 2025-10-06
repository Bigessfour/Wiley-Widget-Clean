using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Syncfusion.XlsIO;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services.Excel;

namespace WileyWidget.Services;

/// <summary>
/// Result of a budget import operation
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public int RecordsImported { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public static ImportResult SuccessResult(int count) =>
        new() { Success = true, RecordsImported = count };

    public static ImportResult FailureResult(IEnumerable<string> errors) =>
        new() { Success = false, Errors = errors.ToList() };
}

/// <summary>
/// Service for importing budget data from Excel files
/// </summary>
public class ExcelBudgetImporter
{
    private readonly ILogger<ExcelBudgetImporter> _logger;
    private readonly AppDbContext _context;
    private readonly IExcelReaderService _excelReader;
    private readonly BudgetPeriodService _budgetPeriodService;

    public ExcelBudgetImporter(
        ILogger<ExcelBudgetImporter> logger,
        AppDbContext context,
        IExcelReaderService excelReader,
        BudgetPeriodService budgetPeriodService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _excelReader = excelReader ?? throw new ArgumentNullException(nameof(excelReader));
        _budgetPeriodService = budgetPeriodService ?? throw new ArgumentNullException(nameof(budgetPeriodService));
    }

    /// <summary>
    /// Import budget data from Excel file
    /// </summary>
    public async Task<ImportResult> ImportBudgetAsync(Stream excelStream, BudgetImportOptions options)
    {
        if (excelStream == null) throw new ArgumentNullException(nameof(excelStream));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var result = new ImportResult();
        var accountsToImport = new List<MunicipalAccount>();

        try
        {
            // Create temporary file for Syncfusion
            var tempFile = Path.GetTempFileName();
            try
            {
                using (var fileStream = File.Create(tempFile))
                {
                    await excelStream.CopyToAsync(fileStream);
                }

                var importResult = await ImportBudgetAsync(tempFile, options);
                return importResult;
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing budget data");
            result.Errors.Add($"Import failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Import budget data from file path
    /// </summary>
    public async Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options)
    {
        return await ImportBudgetAsync(filePath, options, null);
    }

    /// <summary>
    /// Import budget data from file path with progress reporting
    /// </summary>
    public async Task<ImportResult> ImportBudgetAsync(string filePath, BudgetImportOptions options, IProgress<ImportProgress>? progress)
    {
        if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
        if (options == null) throw new ArgumentNullException(nameof(options));

        var result = new ImportResult();
        var accountsToImport = new List<MunicipalAccount>();

        try
        {
            // Report initial progress
            progress?.Report(new ImportProgress { Stage = "Initializing", Progress = 0 });

            // Get worksheet names
            var worksheetNames = _excelReader.GetWorksheetNames(filePath);
            _logger.LogInformation("Found {Count} worksheets: {Names}",
                worksheetNames.Count, string.Join(", ", worksheetNames));

            // Determine which worksheets to process
            var worksheetsToProcess = GetWorksheetsToProcess(worksheetNames, options);

            progress?.Report(new ImportProgress { Stage = "Parsing worksheets", Progress = 10 });

            int worksheetIndex = 0;
            foreach (var worksheetName in worksheetsToProcess)
            {
                try
                {
                    var worksheetProgress = 10 + (worksheetIndex * 70 / worksheetsToProcess.Count);
                    progress?.Report(new ImportProgress
                    {
                        Stage = $"Parsing {worksheetName}",
                        Progress = worksheetProgress
                    });

                    var worksheetAccounts = await ParseWorksheetAsync(filePath, worksheetName, options);
                    accountsToImport.AddRange(worksheetAccounts);

                    _logger.LogInformation("Parsed {Count} accounts from worksheet {Name}",
                        worksheetAccounts.Count, worksheetName);

                    worksheetIndex++;
                }
                catch (Exception ex)
                {
                    var error = $"Error parsing worksheet {worksheetName}: {ex.Message}";
                    _logger.LogError(ex, error);
                    result.Errors.Add(error);
                }
            }

            progress?.Report(new ImportProgress { Stage = "Validating data", Progress = 80 });

            // Validate and import
            if (result.Errors.Count == 0 || options.SkipValidationErrors)
            {
                var validationResult = await ValidateAccountsAsync(accountsToImport, options);
                result.Errors.AddRange(validationResult.Errors);
                result.Warnings.AddRange(validationResult.Warnings);

                if (validationResult.IsValid || options.SkipValidationErrors)
                {
                    if (!options.PreviewOnly)
                    {
                        progress?.Report(new ImportProgress { Stage = "Saving to database", Progress = 90 });
                        await ImportAccountsAsync(accountsToImport, options);
                        result.RecordsImported = accountsToImport.Count;
                    }
                    result.Success = true;
                }
            }

            progress?.Report(new ImportProgress { Stage = "Completed", Progress = 100 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing budget data");
            result.Errors.Add($"Import failed: {ex.Message}");
        }

        return result;
    }

    private List<string> GetWorksheetsToProcess(List<string> availableWorksheets, BudgetImportOptions options)
    {
        if (options.WorksheetsToImport.Any())
            return options.WorksheetsToImport.Where(w => availableWorksheets.Contains(w)).ToList();

        // Default worksheets to process
        var defaultWorksheets = new[] { "GF SUMM", "ENTERPRISE", "GEN GOVT", "HWY&ST", "WATER&ADM" };
        return availableWorksheets.Where(w => defaultWorksheets.Contains(w.ToUpper(CultureInfo.InvariantCulture))).ToList();
    }

    private async Task<List<MunicipalAccount>> ParseWorksheetAsync(string filePath, string worksheetName, BudgetImportOptions options)
    {
        var accounts = new List<MunicipalAccount>();
        var worksheetData = _excelReader.ReadWorksheet(filePath, worksheetName);

        // Determine fund type from worksheet name
        var fundType = MapWorksheetToFundType(worksheetName);

        // Ensure budget period exists
        int budgetPeriodId;
        if (options.BudgetPeriodId.HasValue)
        {
            var budgetPeriod = await _budgetPeriodService.GetBudgetPeriodByIdAsync(options.BudgetPeriodId.Value);
            if (budgetPeriod == null)
            {
                _logger.LogError("Budget period {Id} not found", options.BudgetPeriodId.Value);
                return accounts;
            }
            budgetPeriodId = options.BudgetPeriodId.Value;
        }
        else if (options.CreateNewBudgetPeriod)
        {
            var budgetPeriod = await _budgetPeriodService.GetOrCreateBudgetPeriodAsync(options.BudgetYear, $"{options.BudgetYear} Budget");
            budgetPeriodId = budgetPeriod.Id;
        }
        else
        {
            _logger.LogError("No budget period specified and CreateNewBudgetPeriod is false");
            return accounts;
        }

        // Find header row (look for "ACCOUNT" or "DESCRIPTION" in first few rows)
        var headerRowIndex = FindHeaderRow(worksheetData);

        if (headerRowIndex < 0)
        {
            _logger.LogWarning("Could not find header row in worksheet {Name}", worksheetName);
            return accounts;
        }

        // Map column indices
        var columnMap = MapColumns(worksheetData, headerRowIndex);

        // Parse account rows
        for (int row = headerRowIndex + 1; row < worksheetData.GetLength(0); row++)
        {
            try
            {
                var (account, multiYearData) = ParseAccountRow(worksheetData, row, columnMap, fundType, budgetPeriodId);
                if (account != null)
                {
                    accounts.Add(account);

                    // Store multi-year data for later processing
                    account.MultiYearData = multiYearData;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing row {Row} in worksheet {Name}", row + 1, worksheetName);
            }
        }

        return accounts;
    }

    private int FindHeaderRow(object[,] data)
    {
        for (int row = 0; row < Math.Min(data.GetLength(0), 20); row++)
        {
            for (int col = 0; col < Math.Min(data.GetLength(1), 5); col++)
            {
                var cellValue = data[row, col]?.ToString();
                if (!string.IsNullOrEmpty(cellValue) &&
                    (cellValue.ToUpper(CultureInfo.InvariantCulture).Contains("ACCOUNT") || cellValue.ToUpper(CultureInfo.InvariantCulture).Contains("DESCRIPTION")))
                {
                    return row;
                }
            }
        }
        return -1;
    }

    private Dictionary<string, int> MapColumns(object[,] data, int headerRow)
    {
        var columnMap = new Dictionary<string, int>();

        for (int col = 0; col < data.GetLength(1); col++)
        {
            var headerValue = data[headerRow, col]?.ToString()?.ToUpper(CultureInfo.InvariantCulture) ?? "";

            if (headerValue.Contains("ACCOUNT") || headerValue.Contains("NUMBER"))
                columnMap["AccountNumber"] = col;
            else if (headerValue.Contains("DESCRIPTION") || headerValue.Contains("NAME"))
                columnMap["Description"] = col;
            else if (headerValue.Contains("PRIOR") && headerValue.Contains("YEAR"))
                columnMap["PriorYear"] = col;
            else if (headerValue.Contains("7") && headerValue.Contains("MONTH"))
                columnMap["SevenMonth"] = col;
            else if (headerValue.Contains("ESTIMATE") || headerValue.Contains("CURRENT"))
                columnMap["Estimate"] = col;
            else if (headerValue.Contains("BUDGET") && !headerValue.Contains("YEAR"))
                columnMap["Budget"] = col;
        }

        return columnMap;
    }

    private (MunicipalAccount?, MultiYearBudgetData?) ParseAccountRow(object[,] data, int row, Dictionary<string, int> columnMap,
        FundType fundType, int budgetPeriodId)
    {
        // Get account number
        if (!columnMap.TryGetValue("AccountNumber", out var accountCol))
            return (null, null);

        var accountNumberStr = data[row, accountCol]?.ToString();
        if (string.IsNullOrWhiteSpace(accountNumberStr))
            return (null, null);

        // Validate account number format
        if (!Regex.IsMatch(accountNumberStr, @"^\d+(\.\d+)*$"))
            return (null, null); // Skip non-account rows

        // Get description
        columnMap.TryGetValue("Description", out var descCol);
        var description = descCol >= 0 ? data[row, descCol]?.ToString() ?? "" : "";

        // Skip empty descriptions or header rows
        if (string.IsNullOrWhiteSpace(description) || description.ToUpper(CultureInfo.InvariantCulture).Contains("DESCRIPTION"))
            return (null, null);

        // Get budget amounts
        var priorYear = GetDecimalValue(data, row, columnMap, "PriorYear");
        var sevenMonth = GetDecimalValue(data, row, columnMap, "SevenMonth");
        var estimate = GetDecimalValue(data, row, columnMap, "Estimate");
        var budget = GetDecimalValue(data, row, columnMap, "Budget");

        // Create multi-year data
        var multiYearData = new MultiYearBudgetData
        {
            PriorYear = priorYear > 0 ? priorYear : null,
            SevenMonth = sevenMonth > 0 ? sevenMonth : null,
            Estimate = estimate > 0 ? estimate : null,
            Budget = budget > 0 ? budget : null
        };

        // Use budget amount, fallback to estimate
        var budgetAmount = budget > 0 ? budget : estimate;

        // Create account
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber(accountNumberStr),
            Name = description.Trim(),
            Fund = fundType,
            FundClass = GetFundClass(fundType),
            BudgetAmount = budgetAmount,
            Balance = priorYear, // Use prior year as current balance
            BudgetPeriodId = budgetPeriodId,
            IsActive = true
        };

        // Set department based on fund
        account.DepartmentId = GetDepartmentIdForFund(fundType);

        return (account, multiYearData);
    }

    private decimal GetDecimalValue(object[,] data, int row, Dictionary<string, int> columnMap, string columnKey)
    {
        if (!columnMap.TryGetValue(columnKey, out var col))
            return 0;

        var value = data[row, col]?.ToString();
        return decimal.TryParse(value, out var result) ? result : 0;
    }

    private FundType MapWorksheetToFundType(string worksheetName)
    {
        return worksheetName.ToUpper(CultureInfo.InvariantCulture) switch
        {
            "GF SUMM" or "GENERAL FUND SUMMARY" => FundType.General,
            "ENTERPRISE" => FundType.Enterprise,
            "WATER&ADM" or "WATER" => FundType.Utility,
            "GEN GOVT" or "GENERAL GOVERNMENT" => FundType.General,
            "HWY&ST" or "HIGHWAYS" => FundType.General,
            _ => FundType.General
        };
    }

    private FundClass GetFundClass(FundType fundType)
    {
        return fundType switch
        {
            FundType.Enterprise or FundType.InternalService => FundClass.Proprietary,
            FundType.Trust or FundType.Agency => FundClass.Fiduciary,
            _ => FundClass.Governmental
        };
    }

    private int GetDepartmentIdForFund(FundType fundType)
    {
        // This would need to be implemented based on your department structure
        // For now, return a default department ID
        return 1; // TODO: Implement proper department mapping
    }

    private async Task<ValidationResult> ValidateAccountsAsync(List<MunicipalAccount> accounts, BudgetImportOptions options)
    {
        var result = new ValidationResult();

        // Check for duplicate account numbers
        var duplicates = accounts.GroupBy(a => a.AccountNumber.Value)
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            result.Errors.Add($"Duplicate account number: {duplicate}");
        }

        // Validate account numbers are unique across existing data
        if (!options.OverwriteExistingAccounts && options.BudgetPeriodId.HasValue)
        {
            var existingAccountNumbers = await _context.MunicipalAccounts
                .Where(a => a.BudgetPeriodId == options.BudgetPeriodId.Value)
                .Select(a => a.AccountNumber.Value)
                .ToListAsync();

            var conflicts = accounts.Where(a => existingAccountNumbers.Contains(a.AccountNumber.Value))
                                   .Select(a => a.AccountNumber.Value);

            foreach (var conflict in conflicts)
            {
                result.Errors.Add($"Account number already exists: {conflict}");
            }
        }

        // GASB validation
        if (options.ValidateGASBCompliance)
        {
            foreach (var account in accounts)
            {
                // Governmental funds cannot have negative balances
                if (account.FundClass == FundClass.Governmental && account.Balance < 0)
                {
                    result.Errors.Add($"Governmental fund account {account.AccountNumber.Value} cannot have negative balance");
                }

                // Revenue accounts should not have negative budget amounts
                if (account.Type == AccountType.Revenue && account.BudgetAmount < 0)
                {
                    result.Warnings.Add($"Revenue account {account.AccountNumber.Value} has negative budget amount");
                }
            }
        }

        return result;
    }

    private async Task ImportAccountsAsync(List<MunicipalAccount> accounts, BudgetImportOptions options)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Set parent-child relationships
            SetParentRelationships(accounts);

            await _context.MunicipalAccounts.AddRangeAsync(accounts);
            await _context.SaveChangesAsync();

            // Create multi-year budget entries
            foreach (var account in accounts)
            {
                if (account.MultiYearData != null)
                {
                    await _budgetPeriodService.CreateMultiYearBudgetAsync(account, account.MultiYearData);
                }
            }

            await transaction.CommitAsync();

            _logger.LogInformation("Successfully imported {Count} accounts", accounts.Count);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private void SetParentRelationships(List<MunicipalAccount> accounts)
    {
        foreach (var account in accounts.Where(a => a.AccountNumber.Level > 1))
        {
            var parentNumber = account.AccountNumber.GetParentNumber();
            var parent = accounts.FirstOrDefault(a => a.AccountNumber.Value == parentNumber);

            if (parent != null)
            {
                account.ParentAccountId = parent.Id;
                parent.ChildAccounts.Add(account);
            }
        }
    }
}

/// <summary>
/// Result of validation operation
/// </summary>
public class ValidationResult
{
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}