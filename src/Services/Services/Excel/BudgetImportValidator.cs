using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Validator for budget import data with comprehensive business rule validation.
/// </summary>
public class BudgetImportValidator
{
    private readonly ILogger<BudgetImportValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the BudgetImportValidator class.
    /// </summary>
    /// <param name="logger">Logger instance for logging validation operations.</param>
    public BudgetImportValidator(ILogger<BudgetImportValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates a single municipal account.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>Validation result with any errors found.</returns>
    public ValidationResult ValidateAccount(MunicipalAccount account)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Required field validations
            if (account == null)
            {
                errors.Add("Account is null");
                return ValidationResult.Failure(errors);
            }

            if (account.AccountNumber == null || string.IsNullOrWhiteSpace(account.AccountNumber.Value))
                errors.Add("Account number is required");

            if (string.IsNullOrWhiteSpace(account.Name))
                errors.Add("Account name is required");

            // Account number format validation
            if (account.AccountNumber != null && !string.IsNullOrWhiteSpace(account.AccountNumber.Value))
            {
                if (!Regex.IsMatch(account.AccountNumber.Value, @"^\d+(\.\d+)*$"))
                    errors.Add($"Invalid account number format: {account.AccountNumber.Value}. Must be numeric with optional decimal levels (e.g., 405, 405.1, 410.2.1)");
            }

            // Fund-specific validations
            var fundValidation = ValidateAccountForFund(account);
            errors.AddRange(fundValidation.Errors);
            warnings.AddRange(fundValidation.Warnings);

            // Balance validations
            var balanceValidation = ValidateAccountBalances(account);
            errors.AddRange(balanceValidation.Errors);
            warnings.AddRange(balanceValidation.Warnings);

            // Hierarchical validations
            if (account.ParentAccountId.HasValue)
            {
                var hierarchyValidation = ValidateAccountHierarchy(account);
                errors.AddRange(hierarchyValidation.Errors);
                warnings.AddRange(hierarchyValidation.Warnings);
            }

            // Department validation
            if (account.DepartmentId == 0)
                warnings.Add("Account is not assigned to a department");

            _logger.LogDebug("Validated account {AccountNumber}: {Errors} errors, {Warnings} warnings",
                account.AccountNumber?.Value ?? "Unknown", errors.Count, warnings.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating account {AccountNumber}", account?.AccountNumber?.Value ?? "Unknown");
            errors.Add($"Validation error: {ex.Message}");
        }

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates multiple accounts as a batch.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>Validation result with any errors found across all accounts.</returns>
    public ValidationResult ValidateAccounts(IEnumerable<MunicipalAccount> accounts)
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();
        var validAccounts = 0;

        foreach (var account in accounts)
        {
            var result = ValidateAccount(account);
            if (!result.IsValid)
            {
                allErrors.AddRange(result.Errors.Select(e => $"{account.AccountNumber?.Value ?? "Unknown"}: {e}"));
            }
            allWarnings.AddRange(result.Warnings.Select(w => $"{account.AccountNumber?.Value ?? "Unknown"}: {w}"));
            if (result.IsValid)
                validAccounts++;
        }

        // Cross-account validations
        var crossValidation = ValidateAccountRelationships(accounts);
        allErrors.AddRange(crossValidation.Errors);
        allWarnings.AddRange(crossValidation.Warnings);

        _logger.LogInformation("Batch validation complete: {Total} accounts, {Valid} valid, {Errors} errors, {Warnings} warnings",
            accounts.Count(), validAccounts, allErrors.Count, allWarnings.Count);

        return allErrors.Any() ? ValidationResult.Failure(allErrors, allWarnings) : ValidationResult.Success(allWarnings);
    }

    /// <summary>
    /// Validates account relationships and constraints across multiple accounts.
    /// </summary>
    /// <param name="accounts">The accounts to validate relationships for.</param>
    /// <returns>Validation result for cross-account relationships.</returns>
    private ValidationResult ValidateAccountRelationships(IEnumerable<MunicipalAccount> accounts)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for duplicate account numbers
        var duplicates = accounts
            .Where(a => a.AccountNumber != null)
            .GroupBy(a => a.AccountNumber!.Value)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicate in duplicates)
        {
            errors.Add($"Duplicate account number: {duplicate}");
        }

        // Validate hierarchical relationships
        var accountsByNumber = accounts
            .Where(a => a.AccountNumber != null)
            .ToDictionary(a => a.AccountNumber!.Value, a => a);

        foreach (var account in accounts.Where(a => a.ParentAccountId.HasValue))
        {
            if (account.ParentAccountId.HasValue && account.ParentAccount != null)
            {
                var parentNumber = account.ParentAccount.AccountNumber?.Value;
                var childNumber = account.AccountNumber?.Value;

                if (parentNumber != null && childNumber != null &&
                    !childNumber.StartsWith(parentNumber + "."))
                {
                    errors.Add($"Invalid parent-child relationship: {childNumber} does not start with {parentNumber}.");
                }
            }
        }

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates an account against fund-specific rules.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>Validation result for fund-specific rules.</returns>
    private ValidationResult ValidateAccountForFund(MunicipalAccount account)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // GASB compliance rules
        switch (account.Fund)
        {
            case FundType.General:
            case FundType.SpecialRevenue:
            case FundType.CapitalProjects:
            case FundType.DebtService:
                // Governmental funds should not have negative fund balances
                if (account.Balance < 0 && account.Type == AccountType.FundBalance)
                    errors.Add("Governmental funds cannot have negative fund balance");
                break;

            case FundType.Enterprise:
            case FundType.InternalService:
            case FundType.Utility:
                // Proprietary funds can have negative balances (operating losses)
                // No specific validation needed
                break;

            case FundType.ConservationTrust:
            case FundType.Trust:
            case FundType.Agency:
                // Fiduciary funds have specific rules
                if ((account.Type == AccountType.Taxes || account.Type == AccountType.Fees ||
                     account.Type == AccountType.Grants || account.Type == AccountType.Interest ||
                     account.Type == AccountType.Sales) && account.BudgetAmount < 0)
                    warnings.Add("Fiduciary fund revenue accounts typically should not have negative budgets");
                break;
        }

        // Account type and fund compatibility
        if (!IsValidAccountTypeForFund(account.Type, account.Fund))
        {
            errors.Add($"Account type {account.Type} is not valid for fund type {account.Fund}");
        }

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates account balance rules.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>Validation result for balance rules.</returns>
    private ValidationResult ValidateAccountBalances(MunicipalAccount account)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Revenue accounts should not have negative budgets (typically)
        if ((account.Type == AccountType.Taxes || account.Type == AccountType.Fees ||
             account.Type == AccountType.Grants || account.Type == AccountType.Interest ||
             account.Type == AccountType.Sales) && account.BudgetAmount < 0)
            warnings.Add("Revenue accounts typically should not have negative budget amounts");

        // Expense accounts should not have negative budgets (typically)
        if (IsExpenseAccountType(account.Type) && account.BudgetAmount < 0)
            warnings.Add("Expense accounts typically should not have negative budget amounts");

        // Asset accounts should not have negative balances (typically)
        if (IsAssetAccountType(account.Type) && account.Balance < 0)
            warnings.Add("Asset accounts typically should not have negative balances");

        // Check for unreasonably large amounts (potential data entry errors)
        const decimal unreasonableAmount = 1000000000; // 1 billion
        if (Math.Abs(account.BudgetAmount) > unreasonableAmount)
            warnings.Add($"Budget amount {account.BudgetAmount:C} seems unreasonably large");

        if (Math.Abs(account.Balance) > unreasonableAmount)
            warnings.Add($"Balance amount {account.Balance:C} seems unreasonably large");

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates account hierarchy rules.
    /// </summary>
    /// <param name="account">The account to validate.</param>
    /// <returns>Validation result for hierarchy rules.</returns>
    private ValidationResult ValidateAccountHierarchy(MunicipalAccount account)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (account.ParentAccount == null)
        {
            errors.Add("Parent account reference is null but ParentAccountId is set");
            return ValidationResult.Failure(errors);
        }

        // Validate that parent and child have compatible account numbers
        var parentNumber = account.ParentAccount.AccountNumber?.Value;
        var childNumber = account.AccountNumber?.Value;

        if (parentNumber != null && childNumber != null)
        {
            if (!childNumber.StartsWith(parentNumber + "."))
            {
                errors.Add($"Child account {childNumber} must start with parent account {parentNumber} followed by a dot");
            }

            // Validate level hierarchy
            var parentLevel = account.ParentAccount.AccountNumber?.Level ?? 0;
            var childLevel = account.AccountNumber?.Level ?? 0;

            if (childLevel != parentLevel + 1)
            {
                errors.Add($"Child account level ({childLevel}) must be exactly one level deeper than parent ({parentLevel})");
            }
        }

        return errors.Any() ? ValidationResult.Failure(errors, warnings) : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Determines if an account type is valid for a given fund type.
    /// </summary>
    /// <param name="accountType">The account type to check.</param>
    /// <param name="fundType">The fund type to check against.</param>
    /// <returns>True if the account type is valid for the fund type, false otherwise.</returns>
    private bool IsValidAccountTypeForFund(AccountType accountType, FundType fundType)
    {
        // Capital outlay is typically only valid for capital projects funds
        if (accountType == AccountType.CapitalOutlay &&
            fundType != FundType.CapitalProjects &&
            fundType != FundType.Enterprise)
        {
            return false;
        }

        // Debt service accounts are typically only valid for debt service funds
        if (accountType == AccountType.Debt &&
            fundType != FundType.DebtService &&
            fundType != FundType.Enterprise)
        {
            return false;
        }

        // All other combinations are generally valid
        return true;
    }

    /// <summary>
    /// Determines if an account type represents an expense.
    /// </summary>
    /// <param name="accountType">The account type to check.</param>
    /// <returns>True if the account type is an expense type, false otherwise.</returns>
    private bool IsExpenseAccountType(AccountType accountType)
    {
        return accountType switch
        {
            AccountType.Salaries or AccountType.Supplies or AccountType.Services or
            AccountType.Utilities or AccountType.Maintenance or AccountType.Insurance or
            AccountType.Depreciation or AccountType.PermitsAndAssessments or
            AccountType.ProfessionalServices or AccountType.ContractLabor or
            AccountType.DuesAndSubscriptions or AccountType.CapitalOutlay or
            AccountType.Transfers => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if an account type represents an asset.
    /// </summary>
    /// <param name="accountType">The account type to check.</param>
    /// <returns>True if the account type is an asset type, false otherwise.</returns>
    private bool IsAssetAccountType(AccountType accountType)
    {
        return accountType switch
        {
            AccountType.Cash or AccountType.Investments or AccountType.Receivables or
            AccountType.Inventory or AccountType.FixedAssets => true,
            _ => false
        };
    }
}

/// <summary>
/// Extension methods for ValidationResult.
/// </summary>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    /// <param name="warnings">The warnings to include.</param>
    /// <returns>A successful ValidationResult with warnings.</returns>
    public static ValidationResult Success(IEnumerable<string> warnings)
    {
        return new ValidationResult
        {
            IsValid = true,
            Warnings = new List<string>(warnings)
        };
    }

    /// <summary>
    /// Creates a failed validation result with errors and warnings.
    /// </summary>
    /// <param name="errors">The errors to include.</param>
    /// <param name="warnings">The warnings to include.</param>
    /// <returns>A failed ValidationResult with errors and warnings.</returns>
    public static ValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = new List<string>(errors),
            Warnings = warnings != null ? new List<string>(warnings) : new List<string>()
        };
    }
}