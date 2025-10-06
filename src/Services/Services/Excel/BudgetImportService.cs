using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Service for importing budget data with transaction handling and rollback capability.
/// </summary>
public class BudgetImportService
{
    private readonly AppDbContext _context;
    private readonly BudgetImportValidator _validator;
    private readonly ILogger<BudgetImportService> _logger;

    /// <summary>
    /// Initializes a new instance of the BudgetImportService class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="validator">The budget import validator.</param>
    /// <param name="logger">Logger instance for logging operations.</param>
    public BudgetImportService(
        AppDbContext context,
        BudgetImportValidator validator,
        ILogger<BudgetImportService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Imports budget data with transaction handling.
    /// </summary>
    /// <param name="data">The budget import data.</param>
    /// <returns>Task that completes with the import result.</returns>
    public async Task<ImportResult> ImportWithTransactionAsync(BudgetImportData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var result = new ImportResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _logger.LogInformation("Starting budget import transaction for {Accounts} accounts, {Departments} departments",
                data.Accounts?.Count ?? 0, data.Departments?.Count ?? 0);

            // Validate all data first
            var validationResult = await ValidateImportDataAsync(data);
            if (!validationResult.IsValid)
            {
                result.Errors.AddRange(validationResult.Errors);
                result.Warnings.AddRange(validationResult.Warnings);
                result.Success = false;

                _logger.LogWarning("Budget import validation failed: {Errors} errors, {Warnings} warnings",
                    result.Errors.Count, result.Warnings.Count);
                return result;
            }

            result.Warnings.AddRange(validationResult.Warnings);

            // Create or get budget period
            var budgetPeriod = await GetOrCreateBudgetPeriodAsync(data.BudgetPeriod);
            result.BudgetPeriod = budgetPeriod;

            // Assign budget period to accounts
            if (data.Accounts != null)
            {
                foreach (var account in data.Accounts)
                {
                    account.BudgetPeriodId = budgetPeriod.Id;
                    account.BudgetPeriod = budgetPeriod;
                }
            }

            // Import departments first (accounts reference them)
            if (data.Departments != null && data.Departments.Any())
            {
                await ImportDepartmentsAsync(data.Departments);
                result.Departments.AddRange(data.Departments);
            }

            // Import accounts
            if (data.Accounts != null && data.Accounts.Any())
            {
                await ImportAccountsAsync(data.Accounts);
                result.Accounts.AddRange(data.Accounts);
                result.RowsImported = data.Accounts.Count;
            }

            // Commit transaction
            await transaction.CommitAsync();
            result.Success = true;

            _logger.LogInformation("Budget import completed successfully: {Accounts} accounts, {Departments} departments imported",
                result.Accounts.Count, result.Departments.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during budget import transaction");
            result.Success = false;
            result.Errors.Add($"Import failed: {ex.Message}");

            try
            {
                await transaction.RollbackAsync();
                _logger.LogInformation("Import transaction rolled back successfully");
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Error rolling back import transaction");
                result.Errors.Add($"Rollback failed: {rollbackEx.Message}");
            }
        }
        finally
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.TotalRowsProcessed = (data.Accounts?.Count ?? 0) + (data.Departments?.Count ?? 0);
        }

        return result;
    }

    /// <summary>
    /// Validates import data before processing.
    /// </summary>
    /// <param name="data">The budget import data to validate.</param>
    /// <returns>Task that completes with the validation result.</returns>
    private async Task<ValidationResult> ValidateImportDataAsync(BudgetImportData data)
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        // Validate accounts
        if (data.Accounts != null)
        {
            var accountValidation = _validator.ValidateAccounts(data.Accounts);
            allErrors.AddRange(accountValidation.Errors);
            allWarnings.AddRange(accountValidation.Warnings);
        }

        // Validate departments
        if (data.Departments != null)
        {
            foreach (var department in data.Departments)
            {
                var deptValidation = await ValidateDepartmentAsync(department);
                allErrors.AddRange(deptValidation.Errors);
                allWarnings.AddRange(deptValidation.Warnings);
            }
        }

        // Cross-validation
        var crossValidation = await ValidateImportRelationshipsAsync(data);
        allErrors.AddRange(crossValidation.Errors);
        allWarnings.AddRange(crossValidation.Warnings);

        return allErrors.Any()
            ? ValidationResult.Failure(allErrors, allWarnings)
            : ValidationResult.Success(allWarnings);
    }

    /// <summary>
    /// Validates a department.
    /// </summary>
    /// <param name="department">The department to validate.</param>
    /// <returns>Task that completes with the validation result.</returns>
    private async Task<ValidationResult> ValidateDepartmentAsync(Department department)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (department == null)
        {
            errors.Add("Department is null");
            return ValidationResult.Failure(errors);
        }

        if (string.IsNullOrWhiteSpace(department.Code))
            errors.Add("Department code is required");

        if (string.IsNullOrWhiteSpace(department.Name))
            errors.Add("Department name is required");

        // Check for duplicate codes
        var existingDept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Code == department.Code);

        if (existingDept != null)
            warnings.Add($"Department code '{department.Code}' already exists and will be merged");

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates relationships between imported data.
    /// </summary>
    /// <param name="data">The budget import data to validate.</param>
    /// <returns>Task that completes with the validation result.</returns>
    private async Task<ValidationResult> ValidateImportRelationshipsAsync(BudgetImportData data)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (data.Accounts != null && data.Departments != null)
        {
            // Check that all accounts reference valid departments
            var departmentCodes = data.Departments.Select(d => d.Code).ToHashSet();
            var existingDepartmentCodes = await _context.Departments
                .Select(d => d.Code)
                .ToListAsync();

            departmentCodes.UnionWith(existingDepartmentCodes);

            foreach (var account in data.Accounts)
            {
                // Find the department for this account
                var accountDepartment = data.Departments.FirstOrDefault(d => d.Id == account.DepartmentId);
                if (accountDepartment == null)
                {
                    // Try to find by fund if department not explicitly set
                    accountDepartment = data.Departments.FirstOrDefault(d => d.Fund == account.Fund);
                }

                if (accountDepartment == null)
                {
                    warnings.Add($"Account {account.AccountNumber?.Value ?? "Unknown"} is not assigned to a department");
                }
            }
        }

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Gets or creates a budget period.
    /// </summary>
    /// <param name="budgetPeriod">The budget period to get or create.</param>
    /// <returns>Task that completes with the budget period.</returns>
    private async Task<BudgetPeriod> GetOrCreateBudgetPeriodAsync(BudgetPeriod? budgetPeriod)
    {
        if (budgetPeriod == null)
        {
            // Create default budget period
            budgetPeriod = new BudgetPeriod
            {
                Year = DateTime.Now.Year,
                Name = $"{DateTime.Now.Year} Imported Budget",
                Status = BudgetStatus.Draft,
                CreatedDate = DateTime.UtcNow
            };
        }

        // Try to find existing budget period
        var existingPeriod = await _context.BudgetPeriods
            .FirstOrDefaultAsync(bp => bp.Year == budgetPeriod.Year && bp.Name == budgetPeriod.Name);

        if (existingPeriod != null)
        {
            _logger.LogInformation("Using existing budget period: {Year} - {Name}", existingPeriod.Year, existingPeriod.Name);
            return existingPeriod;
        }

        // Create new budget period
        _context.BudgetPeriods.Add(budgetPeriod);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new budget period: {Year} - {Name}", budgetPeriod.Year, budgetPeriod.Name);
        return budgetPeriod;
    }

    /// <summary>
    /// Imports departments.
    /// </summary>
    /// <param name="departments">The departments to import.</param>
    /// <returns>Task that completes when departments are imported.</returns>
    private async Task ImportDepartmentsAsync(IEnumerable<Department> departments)
    {
        foreach (var department in departments)
        {
            // Check if department already exists
            var existingDept = await _context.Departments
                .FirstOrDefaultAsync(d => d.Code == department.Code);

            if (existingDept != null)
            {
                // Update existing department
                existingDept.Name = department.Name;
                existingDept.Fund = department.Fund;
                _logger.LogDebug("Updated existing department: {Code}", department.Code);
            }
            else
            {
                // Add new department
                _context.Departments.Add(department);
                _logger.LogDebug("Added new department: {Code} - {Name}", department.Code, department.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Imports accounts.
    /// </summary>
    /// <param name="accounts">The accounts to import.</param>
    /// <returns>Task that completes when accounts are imported.</returns>
    private async Task ImportAccountsAsync(IEnumerable<MunicipalAccount> accounts)
    {
        foreach (var account in accounts)
        {
            // Check if account already exists
            var existingAccount = await _context.MunicipalAccounts
                .FirstOrDefaultAsync(a => a.AccountNumber.Value == account.AccountNumber.Value &&
                                        a.BudgetPeriodId == account.BudgetPeriodId);

            if (existingAccount != null)
            {
                // Update existing account
                existingAccount.Name = account.Name;
                existingAccount.Type = account.Type;
                existingAccount.Fund = account.Fund;
                existingAccount.FundClass = account.FundClass;
                existingAccount.BudgetAmount = account.BudgetAmount;
                existingAccount.Balance = account.Balance;
                existingAccount.IsActive = account.IsActive;
                existingAccount.LastSyncDate = DateTime.UtcNow;

                _logger.LogDebug("Updated existing account: {AccountNumber}", account.AccountNumber.Value);
            }
            else
            {
                // Add new account
                _context.MunicipalAccounts.Add(account);
                _logger.LogDebug("Added new account: {AccountNumber} - {Name}", account.AccountNumber.Value, account.Name);
            }
        }

        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Data structure for budget import operations.
/// </summary>
public class BudgetImportData
{
    /// <summary>
    /// Gets or sets the accounts to import.
    /// </summary>
    public List<MunicipalAccount>? Accounts { get; set; }

    /// <summary>
    /// Gets or sets the departments to import.
    /// </summary>
    public List<Department>? Departments { get; set; }

    /// <summary>
    /// Gets or sets the budget period for the import.
    /// </summary>
    public BudgetPeriod? BudgetPeriod { get; set; }
}