using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Validator for GASB (Government Accounting Standards Board) compliance rules.
/// </summary>
public class GASBValidator
{
    private readonly ILogger<GASBValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the GASBValidator class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    public GASBValidator(ILogger<GASBValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates GASB compliance for a collection of accounts.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateGASBCompliance(IEnumerable<MunicipalAccount> accounts)
    {
        if (accounts == null)
            throw new ArgumentNullException(nameof(accounts));

        var errors = new List<string>();
        var warnings = new List<string>();
        var accountList = accounts.ToList();

        _logger.LogInformation("Validating GASB compliance for {Count} accounts", accountList.Count);

        // Validate fund balance rules
        var fundBalanceErrors = ValidateFundBalanceRules(accountList);
        errors.AddRange(fundBalanceErrors);

        // Validate fund classification rules
        var fundClassificationErrors = ValidateFundClassificationRules(accountList);
        errors.AddRange(fundClassificationErrors);

        // Validate account hierarchy rules
        var hierarchyErrors = ValidateAccountHierarchyRules(accountList);
        errors.AddRange(hierarchyErrors);

        // Validate fund-specific rules
        var fundSpecificErrors = ValidateFundSpecificRules(accountList);
        errors.AddRange(fundSpecificErrors);

        // Validate inter-fund relationships
        var interFundErrors = ValidateInterFundRelationships(accountList);
        errors.AddRange(interFundErrors);

        // Generate warnings for potential issues
        var complianceWarnings = GenerateComplianceWarnings(accountList);
        warnings.AddRange(complianceWarnings);

        _logger.LogInformation("GASB validation completed: {Errors} errors, {Warnings} warnings",
            errors.Count, warnings.Count);

        return errors.Any()
            ? ValidationResult.Failure(errors, warnings)
            : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates fund balance rules according to GASB standards.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateFundBalanceRules(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Group accounts by fund
        var accountsByFund = accounts.GroupBy(a => a.Fund).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var fundGroup in accountsByFund)
        {
            var fund = fundGroup.Key;
            var fundAccounts = fundGroup.Value;

            // Calculate fund balance
            var fundBalance = fundAccounts.Sum(a => a.Balance);

            // GASB rules for fund balances
            switch (fund)
            {
                case FundType.General:
                    // General fund should not have negative unrestricted balance
                    var unrestrictedBalance = fundAccounts
                        .Where(a => a.Type == AccountType.Cash || a.Type == AccountType.Investments ||
                                   a.Type == AccountType.Receivables || a.Type == AccountType.Inventory ||
                                   a.Type == AccountType.FixedAssets ||
                                   a.Type == AccountType.Payables || a.Type == AccountType.Debt ||
                                   a.Type == AccountType.AccruedLiabilities)
                        .Sum(a => a.Balance);

                    if (unrestrictedBalance < 0)
                        errors.Add($"General Fund has negative unrestricted balance: {unrestrictedBalance:C}");
                    break;

                case FundType.Enterprise:
                    // Enterprise funds should be self-sustaining
                    if (fundBalance < 0)
                        errors.Add($"Enterprise Fund '{fund}' has negative balance: {fundBalance:C}");
                    break;

                case FundType.DebtService:
                    // Debt service funds should have sufficient reserves
                    var debtServiceBalance = fundAccounts.Sum(a => a.Balance);
                    if (debtServiceBalance < 0)
                        errors.Add($"Debt Service Fund has insufficient reserves: {debtServiceBalance:C}");
                    break;

                case FundType.CapitalProjects:
                    // Capital projects funds can have negative balances during construction
                    // but should be monitored
                    break;
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates fund classification rules.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateFundClassificationRules(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        foreach (var account in accounts)
        {
            // Validate that fund classification matches account usage
            switch (account.FundClass)
            {
                case FundClass.Governmental:
                    // Governmental funds should use modified accrual accounting
                    if (account.Type == AccountType.RetainedEarnings || account.Type == AccountType.FundBalance)
                        errors.Add($"Governmental fund account {account.AccountNumber.Value} should be in General Fund");
                    break;

                case FundClass.Proprietary:
                    // Proprietary funds should use full accrual accounting
                    if (account.Fund != FundType.Enterprise && account.Fund != FundType.InternalService)
                        errors.Add($"Proprietary fund account {account.AccountNumber.Value} should be in Enterprise or Internal Service Fund");
                    break;

                case FundClass.Fiduciary:
                    // Fiduciary funds should use full accrual accounting
                    if (account.Fund != FundType.Trust && account.Fund != FundType.Agency)
                        errors.Add($"Fiduciary fund account {account.AccountNumber.Value} should be in Trust or Agency Fund");
                    break;
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates account hierarchy rules.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateAccountHierarchyRules(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Group accounts by fund for hierarchy validation
        var accountsByFund = accounts.GroupBy(a => a.Fund).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var fundGroup in accountsByFund)
        {
            var fundAccounts = fundGroup.Value.OrderBy(a => a.AccountNumber.Value).ToList();

            // Validate parent-child relationships
            foreach (var account in fundAccounts)
            {
                if (account.AccountNumber.IsParent)
                {
                    // Parent accounts should have children
                    var hasChildren = fundAccounts.Any(a =>
                        a.AccountNumber.Value.StartsWith(account.AccountNumber.Value + ".") &&
                        a.AccountNumber.Level > account.AccountNumber.Level);

                    if (!hasChildren)
                        errors.Add($"Parent account {account.AccountNumber.Value} has no child accounts");
                }
                else
                {
                    // Child accounts should have parents
                    var parentNumber = account.AccountNumber.GetParentNumber();
                    if (!string.IsNullOrEmpty(parentNumber))
                    {
                        var hasParent = fundAccounts.Any(a => a.AccountNumber.Value == parentNumber);
                        if (!hasParent)
                            errors.Add($"Child account {account.AccountNumber.Value} missing parent account {parentNumber}");
                    }
                }
            }

            // Validate account balances roll up correctly
            var parentBalances = new Dictionary<string, decimal>();
            foreach (var account in fundAccounts.Where(a => !a.AccountNumber.IsParent))
            {
                var parentNumber = account.AccountNumber.GetParentNumber();
                if (!string.IsNullOrEmpty(parentNumber))
                {
                    if (!parentBalances.ContainsKey(parentNumber))
                        parentBalances[parentNumber] = 0;
                    parentBalances[parentNumber] += account.Balance;
                }
            }

            // Check that parent balances match sum of children
            foreach (var parentBalance in parentBalances)
            {
                var parentAccount = fundAccounts.FirstOrDefault(a => a.AccountNumber.Value == parentBalance.Key);
                if (parentAccount != null && Math.Abs(parentAccount.Balance - parentBalance.Value) > 0.01m)
                {
                    errors.Add($"Parent account {parentBalance.Key} balance ({parentAccount.Balance:C}) doesn't match sum of children ({parentBalance.Value:C})");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates fund-specific rules.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateFundSpecificRules(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Group accounts by fund
        var accountsByFund = accounts.GroupBy(a => a.Fund).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var fundGroup in accountsByFund)
        {
            var fund = fundGroup.Key;
            var fundAccounts = fundGroup.Value;

            switch (fund)
            {
                case FundType.Utility:
                    // Water utility should have revenue, expense, and asset accounts
                    var hasRevenue = fundAccounts.Any(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                                                 a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                                                 a.Type == AccountType.Sales);
                    var hasExpense = fundAccounts.Any(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                                           a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                                           a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                                           a.Type == AccountType.Depreciation);
                    var hasAssets = fundAccounts.Any(a => a.Type == AccountType.Cash || a.Type == AccountType.Investments ||
                                                          a.Type == AccountType.Receivables || a.Type == AccountType.Inventory ||
                                                          a.Type == AccountType.FixedAssets);

                    if (!hasRevenue)
                        errors.Add("Water Utility Fund missing revenue accounts");
                    if (!hasExpense)
                        errors.Add("Water Utility Fund missing expense accounts");
                    if (!hasAssets)
                        errors.Add("Water Utility Fund missing asset accounts");
                    break;

                case FundType.SpecialRevenue:
                    // Sanitation district should have similar structure
                    var sanitationRevenue = fundAccounts.Any(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                                                       a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                                                       a.Type == AccountType.Sales);
                    var sanitationExpense = fundAccounts.Any(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                                                 a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                                                 a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                                                 a.Type == AccountType.Depreciation);

                    if (!sanitationRevenue)
                        errors.Add("Sanitation Fund missing revenue accounts");
                    if (!sanitationExpense)
                        errors.Add("Sanitation Fund missing expense accounts");
                    break;

                case FundType.General:
                    // General fund should have major revenue sources
                    var hasPropertyTax = fundAccounts.Any(a => a.Name?.Contains("Property Tax") == true);
                    var hasSalesTax = fundAccounts.Any(a => a.Name?.Contains("Sales Tax") == true);

                    if (!hasPropertyTax && !hasSalesTax)
                        errors.Add("General Fund should have property tax or sales tax revenue");
                    break;
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates inter-fund relationships.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateInterFundRelationships(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Check for inter-fund transfers
        var transferAccounts = accounts.Where(a => a.Name?.Contains("Transfer") == true ||
                                                 a.Name?.Contains("Interfund") == true).ToList();

        foreach (var transfer in transferAccounts)
        {
            // Transfers should balance between funds
            var correspondingTransfer = accounts.FirstOrDefault(a =>
                a.Name == transfer.Name &&
                a.Fund != transfer.Fund &&
                Math.Abs(a.Balance + transfer.Balance) < 0.01m); // Should offset

            if (correspondingTransfer == null)
                errors.Add($"Inter-fund transfer {transfer.AccountNumber.Value} in {transfer.Fund} has no corresponding entry");
        }

        return errors;
    }

    /// <summary>
    /// Generates compliance warnings for potential issues.
    /// </summary>
    /// <param name="accounts">The accounts to analyze.</param>
    /// <returns>List of compliance warnings.</returns>
    private List<string> GenerateComplianceWarnings(List<MunicipalAccount> accounts)
    {
        var warnings = new List<string>();

        // Check for unusual account balances
        var highBalanceAccounts = accounts.Where(a => Math.Abs(a.Balance) > 1000000m).ToList();
        foreach (var account in highBalanceAccounts)
        {
            warnings.Add($"Account {account.AccountNumber.Value} has unusually high balance: {account.Balance:C}");
        }

        // Check for accounts with no activity
        var zeroBalanceAccounts = accounts.Where(a => a.Balance == 0 && a.BudgetAmount == 0).ToList();
        if (zeroBalanceAccounts.Count > accounts.Count * 0.1) // More than 10% zero balance
        {
            warnings.Add($"{zeroBalanceAccounts.Count} accounts have zero balance and budget - consider review");
        }

        // Check for budget variances
        var varianceAccounts = accounts.Where(a => a.BudgetAmount != 0 &&
                                                 Math.Abs((a.Balance - a.BudgetAmount) / a.BudgetAmount) > 0.2m).ToList();
        foreach (var account in varianceAccounts)
        {
            var variance = ((account.Balance - account.BudgetAmount) / account.BudgetAmount) * 100;
            warnings.Add($"Account {account.AccountNumber.Value} has {variance:F1}% budget variance");
        }

        return warnings;
    }
}

/// <summary>
/// Result of a GASB validation operation.
/// </summary>
public class GASBValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation passed.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Gets or sets the validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Gets or sets the compliance score (0-100).
    /// </summary>
    public int ComplianceScore { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>The validation result.</returns>
    public static GASBValidationResult Success(IEnumerable<string>? warnings = null)
    {
        return new GASBValidationResult
        {
            IsValid = true,
            Warnings = warnings?.ToList() ?? new List<string>(),
            ComplianceScore = 100
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="warnings">Optional warnings.</param>
    /// <returns>The validation result.</returns>
    public static GASBValidationResult Failure(IEnumerable<string> errors, IEnumerable<string>? warnings = null)
    {
        return new GASBValidationResult
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? new List<string>(),
            ComplianceScore = Math.Max(0, 100 - (errors.Count() * 10))
        };
    }
}