using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Parser for municipal budget data from Excel files.
/// Handles parsing of accounts, departments, and budget periods from municipal budget spreadsheets.
/// </summary>
public class MunicipalBudgetParser : IMunicipalBudgetParser
{
    private readonly ILogger<MunicipalBudgetParser> _logger;

    /// <summary>
    /// Initializes a new instance of the MunicipalBudgetParser class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    public MunicipalBudgetParser(ILogger<MunicipalBudgetParser> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public List<MunicipalAccount> ParseAccounts(object[,] worksheetData, string worksheetName)
    {
        var accounts = new List<MunicipalAccount>();

        if (worksheetData == null || worksheetData.GetLength(0) == 0 || worksheetData.GetLength(1) == 0)
        {
            _logger.LogWarning("Empty or null worksheet data for worksheet: {WorksheetName}", worksheetName);
            return accounts;
        }

        try
        {
            // Detect the format of the worksheet and parse accordingly
            if (IsTownOfWileyFormat(worksheetData))
            {
                accounts.AddRange(ParseTownOfWileyAccounts(worksheetData, worksheetName));
            }
            else if (IsWileySanitationDistrictFormat(worksheetData))
            {
                accounts.AddRange(ParseWileySanitationDistrictAccounts(worksheetData, worksheetName));
            }
            else
            {
                // Try generic parsing
                accounts.AddRange(ParseGenericAccounts(worksheetData, worksheetName));
            }

            _logger.LogInformation("Parsed {AccountCount} accounts from worksheet: {WorksheetName}",
                accounts.Count, worksheetName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing accounts from worksheet: {WorksheetName}", worksheetName);
            throw;
        }

        return accounts;
    }

    /// <inheritdoc/>
    public List<Department> ParseDepartments(object[,] worksheetData, string worksheetName)
    {
        var departments = new List<Department>();

        if (worksheetData == null || worksheetData.GetLength(0) == 0 || worksheetData.GetLength(1) == 0)
        {
            _logger.LogWarning("Empty or null worksheet data for worksheet: {WorksheetName}", worksheetName);
            return departments;
        }

        try
        {
            // Extract unique departments from account data
            var departmentCodes = new HashSet<string>();

            for (int row = 0; row < worksheetData.GetLength(0); row++)
            {
                for (int col = 0; col < worksheetData.GetLength(1); col++)
                {
                    var cellValue = worksheetData[row, col]?.ToString();
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        // Look for department codes (typically 2-4 characters)
                        if (cellValue.Length >= 2 && cellValue.Length <= 4 && IsDepartmentCode(cellValue))
                        {
                            departmentCodes.Add(cellValue.ToUpper(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            // Create department objects
            foreach (var code in departmentCodes)
            {
                var department = new Department
                {
                    Code = code,
                    Name = GetDepartmentNameFromCode(code),
                    Fund = ParseFundTypeFromWorksheetName(worksheetName) // Parse worksheet name to FundType
                };
                departments.Add(department);
            }

            _logger.LogInformation("Parsed {DepartmentCount} departments from worksheet: {WorksheetName}",
                departments.Count, worksheetName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing departments from worksheet: {WorksheetName}", worksheetName);
            throw;
        }

        return departments;
    }

    /// <inheritdoc/>
    public List<BudgetPeriod> ParseBudgetPeriods(object[,] worksheetData, string worksheetName)
    {
        var budgetPeriods = new List<BudgetPeriod>();

        if (worksheetData == null || worksheetData.GetLength(0) == 0 || worksheetData.GetLength(1) == 0)
        {
            _logger.LogWarning("Empty or null worksheet data for worksheet: {WorksheetName}", worksheetName);
            return budgetPeriods;
        }

        try
        {
            // Look for year information in the worksheet
            int year = ExtractYearFromWorksheet(worksheetData, worksheetName);

            if (year > 0)
            {
                var budgetPeriod = new BudgetPeriod
                {
                    Year = year,
                    Name = $"{year} Municipal Budget",
                    CreatedDate = DateTime.UtcNow,
                    Status = BudgetStatus.Draft
                };
                budgetPeriods.Add(budgetPeriod);

                _logger.LogInformation("Parsed budget period for year {Year} from worksheet: {WorksheetName}",
                    year, worksheetName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing budget periods from worksheet: {WorksheetName}", worksheetName);
            throw;
        }

        return budgetPeriods;
    }

    /// <inheritdoc/>
    public ValidationResult ValidateWorksheetStructure(object[,] worksheetData, string worksheetName)
    {
        var result = new ValidationResult { IsValid = true };

        if (worksheetData == null)
        {
            result.IsValid = false;
            result.Errors.Add("Worksheet data is null");
            return result;
        }

        if (worksheetData.GetLength(0) == 0 || worksheetData.GetLength(1) == 0)
        {
            result.IsValid = false;
            result.Errors.Add("Worksheet is empty");
            return result;
        }

        // Check for minimum data requirements
        if (worksheetData.GetLength(0) < 5)
        {
            result.Warnings.Add("Worksheet has very few rows - may not contain sufficient data");
        }

        if (worksheetData.GetLength(1) < 3)
        {
            result.Warnings.Add("Worksheet has very few columns - may not contain account information");
        }

        // Check for recognizable patterns
        bool hasNumericData = false;
        bool hasTextData = false;

        for (int row = 0; row < Math.Min(worksheetData.GetLength(0), 50); row++) // Check first 50 rows
        {
            for (int col = 0; col < Math.Min(worksheetData.GetLength(1), 10); col++) // Check first 10 columns
            {
                var cellValue = worksheetData[row, col];
                if (cellValue != null)
                {
                    if (double.TryParse(cellValue.ToString(), out _))
                    {
                        hasNumericData = true;
                    }
                    else if (!string.IsNullOrWhiteSpace(cellValue.ToString()))
                    {
                        hasTextData = true;
                    }
                }
            }
        }

        if (!hasNumericData)
        {
            result.Warnings.Add("No numeric data found - may not contain budget amounts");
        }

        if (!hasTextData)
        {
            result.Warnings.Add("No text data found - may not contain account descriptions");
        }

        _logger.LogInformation("Validated worksheet '{WorksheetName}': Valid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}",
            worksheetName, result.IsValid, result.Errors.Count, result.Warnings.Count);

        return result;
    }

    /// <summary>
    /// Determines if the worksheet follows the Town of Wiley budget format.
    /// </summary>
    private bool IsTownOfWileyFormat(object[,] data)
    {
        // Look for characteristic patterns in Town of Wiley budget
        for (int row = 0; row < Math.Min(data.GetLength(0), 20); row++)
        {
            for (int col = 0; col < Math.Min(data.GetLength(1), 5); col++)
            {
                var cellValue = data[row, col]?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    // Look for "Town of Wiley" or similar identifiers
                    if (cellValue.Contains("Town of Wiley", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Determines if the worksheet follows the Wiley Sanitation District budget format.
    /// </summary>
    private bool IsWileySanitationDistrictFormat(object[,] data)
    {
        // Look for characteristic patterns in Wiley Sanitation District budget
        for (int row = 0; row < Math.Min(data.GetLength(0), 20); row++)
        {
            for (int col = 0; col < Math.Min(data.GetLength(1), 5); col++)
            {
                var cellValue = data[row, col]?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    // Look for "Wiley Sanitation District" or similar identifiers
                    if (cellValue.Contains("Wiley Sanitation", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Parses accounts from Town of Wiley budget format.
    /// Format: Account | Description | Prior Year | 7 Month | Estimate | Budget
    /// </summary>
    private List<MunicipalAccount> ParseTownOfWileyAccounts(object[,] data, string worksheetName)
    {
        var accounts = new List<MunicipalAccount>();

        if (data.GetLength(0) < 10 || data.GetLength(1) < 6)
        {
            _logger.LogWarning("Town of Wiley worksheet {WorksheetName} doesn't have expected format (expected at least 10 rows and 6 columns)",
                worksheetName);
            return accounts;
        }

        try
        {
            // Skip header rows (typically first 9 rows contain headers/titles)
            for (int row = 9; row < data.GetLength(0); row++)
            {
                var cells = new string?[6];

                // Extract the 6 key columns: Account, Description, Prior Year, 7 Month, Estimate, Budget
                for (int col = 0; col < 6 && col < data.GetLength(1); col++)
                {
                    cells[col] = data[row, col]?.ToString()?.Trim();
                }

                // Skip empty rows
                if (string.IsNullOrWhiteSpace(cells[0]) && string.IsNullOrWhiteSpace(cells[1]))
                    continue;

                var accountNumber = cells[0];
                var description = cells[1];
                var budgetAmountText = cells[5]; // Budget column

                // Validate required fields
                if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(description))
                    continue;

                // Parse budget amount
                if (!TryParseDecimal(budgetAmountText, out var budgetAmount))
                {
                    _logger.LogWarning("Could not parse budget amount '{Amount}' for account {Account} in row {Row}",
                        budgetAmountText, accountNumber, row + 1);
                    continue;
                }

                // Create account
                var account = new MunicipalAccount
                {
                    AccountNumber = new AccountNumber(accountNumber),
                    Name = description,
                    BudgetAmount = budgetAmount,
                    Fund = FundMapper.MapWorksheetName(worksheetName),
                    FundClass = GetFundClassForFund(FundMapper.MapWorksheetName(worksheetName)),
                    Type = InferAccountType(description, accountNumber),
                    IsActive = true
                };

                accounts.Add(account);

                _logger.LogDebug("Parsed Town of Wiley account: {AccountNumber} - {Description} - {Amount:C}",
                    accountNumber, description, budgetAmount);
            }

            _logger.LogInformation("Successfully parsed {Count} accounts from Town of Wiley worksheet {WorksheetName}",
                accounts.Count, worksheetName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Town of Wiley accounts from worksheet {WorksheetName}", worksheetName);
            throw;
        }

        return accounts;
    }

    /// <summary>
    /// Parses accounts from Wiley Sanitation District budget format.
    /// </summary>
    private List<MunicipalAccount> ParseWileySanitationDistrictAccounts(object[,] data, string worksheetName)
    {
        var accounts = new List<MunicipalAccount>();
        // Implementation would parse specific Wiley Sanitation District format
        // This is a placeholder for the actual parsing logic
        _logger.LogInformation("Parsing Wiley Sanitation District format from worksheet: {WorksheetName}", worksheetName);
        return accounts;
    }

    /// <summary>
    /// Parses accounts using generic logic for unrecognized formats.
    /// </summary>
    private List<MunicipalAccount> ParseGenericAccounts(object[,] data, string worksheetName)
    {
        var accounts = new List<MunicipalAccount>();

        // Simple generic parsing - look for rows with account-like data
        for (int row = 1; row < data.GetLength(0); row++) // Skip header row
        {
            try
            {
                var account = ParseGenericAccountRow(data, row, worksheetName);
                if (account != null)
                {
                    accounts.Add(account);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing row {Row} in worksheet {WorksheetName}", row + 1, worksheetName);
            }
        }

        return accounts;
    }

    /// <summary>
    /// Parses a single account row using generic logic.
    /// </summary>
    private MunicipalAccount? ParseGenericAccountRow(object[,] data, int row, string worksheetName)
    {
        // This is a simplified implementation
        // In a real implementation, this would analyze the row structure
        // and extract account number, description, amounts, etc.

        if (data.GetLength(1) < 3) return null; // Need at least 3 columns

        var accountNumber = data[row, 0]?.ToString();
        var description = data[row, 1]?.ToString();
        var amountText = data[row, 2]?.ToString();

        if (string.IsNullOrEmpty(accountNumber) || string.IsNullOrEmpty(description))
            return null;

        if (!double.TryParse(amountText, NumberStyles.Currency, CultureInfo.CurrentCulture, out var amount))
            return null;

        // Create account with basic information
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber(accountNumber),
            Name = description,
            Fund = FundType.General, // Default
            Type = AccountType.Services, // Default expense type
            BudgetAmount = (decimal)amount
        };

        return account;
    }

    /// <summary>
    /// Checks if a string looks like a department code.
    /// </summary>
    private bool IsDepartmentCode(string value)
    {
        // Department codes are typically 2-4 uppercase letters/numbers
        if (value.Length < 2 || value.Length > 4) return false;

        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c)) return false;
        }

        return value == value.ToUpper(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a FundType from worksheet name.
    /// </summary>
    private FundType ParseFundTypeFromWorksheetName(string worksheetName)
    {
        if (string.IsNullOrEmpty(worksheetName))
            return FundType.General;

        var name = worksheetName.ToLowerInvariant();

        // Try to match common fund type patterns
        if (name.Contains("general") || name.Contains("gen"))
            return FundType.General;
        if (name.Contains("special") || name.Contains("revenue"))
            return FundType.SpecialRevenue;
        if (name.Contains("capital") || name.Contains("projects"))
            return FundType.CapitalProjects;
        if (name.Contains("debt") || name.Contains("service"))
            return FundType.DebtService;
        if (name.Contains("enterprise"))
            return FundType.Enterprise;
        if (name.Contains("internal") || name.Contains("service"))
            return FundType.InternalService;
        if (name.Contains("trust"))
            return FundType.Trust;
        if (name.Contains("agency"))
            return FundType.Agency;
        if (name.Contains("conservation"))
            return FundType.ConservationTrust;
        if (name.Contains("recreation"))
            return FundType.Recreation;
        if (name.Contains("utility") || name.Contains("sanitation") || name.Contains("water"))
            return FundType.Utility;

        // Default to General fund
        return FundType.General;
    }

    /// <summary>
    /// Gets a department name from a department code.
    /// </summary>
    private string GetDepartmentNameFromCode(string code)
    {
        // This would have a mapping of codes to names
        // For now, return a formatted version of the code
        return $"Department {code}";
    }

    /// <summary>
    /// Extracts year information from worksheet data.
    /// </summary>
    private int ExtractYearFromWorksheet(object[,] data, string worksheetName)
    {
        // Look for year patterns in worksheet name or data
        if (int.TryParse(worksheetName, out var year) && year >= 2000 && year <= 2100)
        {
            return year;
        }

        // Look for year patterns in cell data
        for (int row = 0; row < Math.Min(data.GetLength(0), 10); row++)
        {
            for (int col = 0; col < Math.Min(data.GetLength(1), 5); col++)
            {
                var cellValue = data[row, col]?.ToString();
                if (!string.IsNullOrEmpty(cellValue))
                {
                    // Look for 4-digit years
                    if (int.TryParse(cellValue, out var cellYear) && cellYear >= 2000 && cellYear <= 2100)
                    {
                        return cellYear;
                    }
                }
            }
        }

        // Default to current year if no year found
        return DateTime.Now.Year;
    }

    /// <summary>
    /// Attempts to parse a decimal value from various string formats.
    /// </summary>
    private bool TryParseDecimal(string? value, out decimal result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Remove common currency symbols and formatting
        var cleanValue = value.Replace("$", "").Replace(",", "").Replace("(", "").Replace(")", "").Trim();

        // Try parsing with different number styles
        return decimal.TryParse(cleanValue, NumberStyles.Number | NumberStyles.Currency, CultureInfo.CurrentCulture, out result) ||
               decimal.TryParse(cleanValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Gets the fund class for a given fund type.
    /// </summary>
    private FundClass GetFundClassForFund(FundType fundType)
    {
        return fundType switch
        {
            FundType.General or FundType.SpecialRevenue or FundType.CapitalProjects or FundType.DebtService => FundClass.Governmental,
            FundType.Enterprise or FundType.InternalService => FundClass.Proprietary,
            FundType.Trust or FundType.ConservationTrust or FundType.Agency => FundClass.Fiduciary,
            FundType.Recreation or FundType.Utility => FundClass.Proprietary, // Typically proprietary
            _ => FundClass.Governmental
        };
    }

    /// <summary>
    /// Infers account type from description and account number.
    /// </summary>
    private AccountType InferAccountType(string description, string accountNumber)
    {
        if (string.IsNullOrEmpty(description))
            return AccountType.Taxes; // Default

        var desc = description.ToLowerInvariant();

        if (desc.Contains("tax"))
            return AccountType.Taxes;
        if (desc.Contains("fee") || desc.Contains("permit") || desc.Contains("license") || desc.Contains("charge"))
            return AccountType.Fees;
        if (desc.Contains("revenue"))
            return AccountType.Sales;

        // Expense indicators
        if (desc.Contains("salary") || desc.Contains("wage") || desc.Contains("payroll"))
            return AccountType.Salaries;
        if (desc.Contains("supply") || desc.Contains("material"))
            return AccountType.Supplies;
        if (desc.Contains("service") || desc.Contains("contract") || desc.Contains("professional"))
            return AccountType.Services;
        if (desc.Contains("utility") || desc.Contains("electric") || desc.Contains("water") || desc.Contains("gas"))
            return AccountType.Utilities;
        if (desc.Contains("maintenance") || desc.Contains("repair"))
            return AccountType.Maintenance;
        if (desc.Contains("insurance"))
            return AccountType.Insurance;
        if (desc.Contains("depreciation"))
            return AccountType.Depreciation;
        if (desc.Contains("capital") || desc.Contains("equipment") || desc.Contains("vehicle"))
            return AccountType.CapitalOutlay;

        // Asset indicators
        if (desc.Contains("cash") || desc.Contains("bank"))
            return AccountType.Cash;
        if (desc.Contains("investment"))
            return AccountType.Investments;
        if (desc.Contains("receivable") || desc.Contains("billing"))
            return AccountType.Receivables;
        if (desc.Contains("inventory"))
            return AccountType.Inventory;
        if (desc.Contains("fixed asset") || desc.Contains("property") || desc.Contains("building"))
            return AccountType.FixedAssets;

        // Liability indicators
        if (desc.Contains("payable") || desc.Contains("vendor"))
            return AccountType.Payables;
        if (desc.Contains("debt") || desc.Contains("bond") || desc.Contains("loan"))
            return AccountType.Debt;
        if (desc.Contains("accrued"))
            return AccountType.AccruedLiabilities;

        // Equity indicators
        if (desc.Contains("balance") || desc.Contains("fund balance"))
            return AccountType.FundBalance;
        if (desc.Contains("earnings") || desc.Contains("profit") || desc.Contains("loss"))
            return AccountType.RetainedEarnings;

        // Check account number patterns
        if (accountNumber.StartsWith("1"))
            return AccountType.Cash; // Assets typically start with 1
        if (accountNumber.StartsWith("2"))
            return AccountType.Payables; // Liabilities typically start with 2
        if (accountNumber.StartsWith("3"))
            return AccountType.Taxes; // Revenue typically starts with 3
        if (accountNumber.StartsWith("4") || accountNumber.StartsWith("5"))
            return AccountType.Salaries; // Expenses typically start with 4-5

        // Default to revenue for unrecognized accounts
        return AccountType.Taxes;
    }
}