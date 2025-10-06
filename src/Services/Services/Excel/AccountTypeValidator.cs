using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;

namespace WileyWidget.Services.Excel;

/// <summary>
/// Validator for account type rules following GASB compliance standards.
/// Validates specific account type restrictions and fund balance rules.
/// </summary>
public class AccountTypeValidator
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the AccountTypeValidator class.
    /// </summary>
    /// <param name="logger">Logger instance for logging operations.</param>
    public AccountTypeValidator(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates GASB account type compliance for a collection of accounts.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>The validation result.</returns>
    public ValidationResult ValidateAccountTypeCompliance(IEnumerable<MunicipalAccount> accounts)
    {
        if (accounts == null)
            throw new ArgumentNullException(nameof(accounts));

        var errors = new List<string>();
        var warnings = new List<string>();
        var accountList = accounts.ToList();

        _logger.LogInformation("Validating account type compliance for {Count} accounts", accountList.Count);

        // Validate fund balance rules
        var fundBalanceErrors = ValidateGovernmentalFundBalances(accountList);
        errors.AddRange(fundBalanceErrors);

        // Validate account type restrictions
        var accountTypeErrors = ValidateAccountTypeRestrictions(accountList);
        errors.AddRange(accountTypeErrors);

        // Validate fund class equity rules
        var equityErrors = ValidateFundClassEquityRules(accountList);
        errors.AddRange(equityErrors);

        // Validate mill levy and uncollectible provisions
        var provisionErrors = ValidateMillLevyAndProvisions(accountList);
        errors.AddRange(provisionErrors);

        _logger.LogInformation("Account type validation completed: {Errors} errors, {Warnings} warnings",
            errors.Count, warnings.Count);

        return errors.Any()
            ? ValidationResult.Failure(errors, warnings)
            : ValidationResult.Success(warnings);
    }

    /// <summary>
    /// Validates that Governmental funds do not have negative balances.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateGovernmentalFundBalances(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Group accounts by fund
        var governmentalFunds = accounts
            .Where(a => a.FundClass == FundClass.Governmental)
            .GroupBy(a => a.Fund)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var fundGroup in governmentalFunds)
        {
            var fund = fundGroup.Key;
            var fundAccounts = fundGroup.Value;

            // Calculate total fund balance
            var fundBalance = fundAccounts.Sum(a => a.Balance);

            // GASB Rule: Governmental funds cannot have negative balances
            if (fundBalance < 0)
            {
                errors.Add($"Governmental Fund '{fund}' has negative balance: {fundBalance:C}. Governmental funds must maintain non-negative balances per GASB standards.");
            }

            _logger.LogDebug("Validated Governmental Fund {Fund}: Balance = {Balance:C}", fund, fundBalance);
        }

        return errors;
    }

    /// <summary>
    /// Validates account type restrictions per GASB standards.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateAccountTypeRestrictions(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        foreach (var account in accounts)
        {
            // GASB Rule: CapitalOutlay accounts are restricted to Capital Projects Fund
            if (account.Type == AccountType.CapitalOutlay && account.Fund != FundType.CapitalProjects)
            {
                errors.Add($"Account {account.AccountNumber.Value} ({account.Name}) is CapitalOutlay type but assigned to {account.Fund} fund. CapitalOutlay accounts must be in CapitalProjects fund per GASB standards.");
            }

            // Additional account type restrictions can be added here
            // For example: Certain expense types restricted to specific funds
        }

        return errors;
    }

    /// <summary>
    /// Validates fund class equity rules per GASB standards.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateFundClassEquityRules(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        foreach (var account in accounts)
        {
            // GASB Rule: RetainedEarnings equity accounts are only allowed in Proprietary funds
            if (account.Type == AccountType.RetainedEarnings && account.FundClass != FundClass.Proprietary)
            {
                errors.Add($"Account {account.AccountNumber.Value} ({account.Name}) is RetainedEarnings type but assigned to {account.FundClass} fund class. RetainedEarnings accounts are only permitted in Proprietary funds per GASB standards.");
            }

            // GASB Rule: FundBalance equity accounts are only allowed in Governmental funds
            if (account.Type == AccountType.FundBalance && account.FundClass != FundClass.Governmental)
            {
                errors.Add($"Account {account.AccountNumber.Value} ({account.Name}) is FundBalance type but assigned to {account.FundClass} fund class. FundBalance accounts are only permitted in Governmental funds per GASB standards.");
            }

            // Additional equity rules can be added here
        }

        return errors;
    }

    /// <summary>
    /// Validates mill levy and uncollectible provisions based on TOW/WSD budget structures.
    /// </summary>
    /// <param name="accounts">The accounts to validate.</param>
    /// <returns>List of validation errors.</returns>
    private List<string> ValidateMillLevyAndProvisions(List<MunicipalAccount> accounts)
    {
        var errors = new List<string>();

        // Identify mill levy accounts (property tax revenue)
        var millLevyAccounts = accounts.Where(a =>
            a.Type == AccountType.Taxes &&
            (a.Name?.Contains("Property Tax", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.Contains("Mill Levy", StringComparison.OrdinalIgnoreCase) == true ||
             a.Name?.Contains("Ad Valorem", StringComparison.OrdinalIgnoreCase) == true)).ToList();

        // Identify uncollectible provision accounts
        var uncollectibleAccounts = accounts.Where(a =>
            a.Name?.Contains("Uncollectible", StringComparison.OrdinalIgnoreCase) == true ||
            a.Name?.Contains("Allowance for Doubtful", StringComparison.OrdinalIgnoreCase) == true ||
            a.Name?.Contains("Bad Debt", StringComparison.OrdinalIgnoreCase) == true).ToList();

        // Validate mill levy accounts are in appropriate funds (typically General or Special Revenue)
        foreach (var account in millLevyAccounts)
        {
            if (account.Fund != FundType.General && account.Fund != FundType.SpecialRevenue)
            {
                errors.Add($"Mill levy account {account.AccountNumber.Value} ({account.Name}) should be in General or Special Revenue fund, not {account.Fund}.");
            }
        }

        // Validate uncollectible provisions are properly structured
        foreach (var account in uncollectibleAccounts)
        {
            // Uncollectible provisions should typically be contra-accounts to receivables
            if (account.Type != AccountType.Receivables && account.Balance > 0)
            {
                errors.Add($"Uncollectible provision account {account.AccountNumber.Value} ({account.Name}) should typically be a contra-receivable account with negative balance.");
            }
        }

        // Validate that property tax revenues have corresponding uncollectible provisions
        var taxRevenueFunds = millLevyAccounts.Select(a => a.Fund).Distinct();
        foreach (var fund in taxRevenueFunds)
        {
            var hasUncollectibleProvision = uncollectibleAccounts.Any(a => a.Fund == fund);
            if (!hasUncollectibleProvision)
            {
                errors.Add($"Fund {fund} has mill levy/property tax accounts but no corresponding uncollectible provision account. TOW/WSD budgets require provisions for uncollectible taxes.");
            }
        }

        _logger.LogDebug("Validated {MillLevyCount} mill levy accounts and {ProvisionCount} uncollectible provision accounts",
            millLevyAccounts.Count, uncollectibleAccounts.Count);

        return errors;
    }
}