#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Validator for GASB (Government Accounting Standards Board) compliance
/// </summary>
public class GASBValidator
{
    /// <summary>
    /// Validates GASB compliance for a collection of budget entries
    /// </summary>
    /// <param name="entries">The budget entries to validate</param>
    /// <returns>A list of validation errors, empty if compliant</returns>
    public List<string> ValidateGASBCompliance(IEnumerable<BudgetEntry> entries)
    {
        var errors = new List<string>();

        if (entries == null)
        {
            errors.Add("Budget entries collection cannot be null");
            return errors;
        }

        var entryList = entries.ToList();

        // Validate each entry
        foreach (var entry in entryList)
        {
            ValidateBudgetEntry(entry, errors);
        }

        // Validate relationships and consistency
        ValidateBudgetConsistency(entryList, errors);

        return errors;
    }

    /// <summary>
    /// Validates a single budget entry for GASB compliance
    /// </summary>
    /// <param name="entry">The budget entry to validate</param>
    /// <param name="errors">The list to add errors to</param>
    private void ValidateBudgetEntry(BudgetEntry entry, List<string> errors)
    {
        if (entry == null)
        {
            errors.Add("Budget entry cannot be null");
            return;
        }

        // Validate municipal account reference
        if (entry.MunicipalAccount == null)
        {
            errors.Add($"Budget entry {entry.Id} is missing municipal account reference");
            return;
        }

        var account = entry.MunicipalAccount;

        // Validate account number format
        if (string.IsNullOrWhiteSpace(account.AccountNumber?.Value))
        {
            errors.Add($"Account {account.Id} ({account.Name}) is missing account number");
        }
        else
        {
            ValidateAccountNumber(account.AccountNumber.Value, account.Id, account.Name, errors);
        }

        // Validate account type
        if (account.Type == AccountType.Unknown)
        {
            errors.Add($"Account {account.Id} ({account.Name}) has unknown account type");
        }

        // Validate fund class
        if (account.FundClass == null)
        {
            errors.Add($"Account {account.Id} ({account.Name}) is missing fund class");
        }

        // Validate department
        if (account.Department == null)
        {
            errors.Add($"Account {account.Id} ({account.Name}) is missing department reference");
        }

        // Validate amount
        if (entry.Amount < 0)
        {
            errors.Add($"Account {account.Id} ({account.Name}) has negative amount: {entry.Amount}");
        }

        // Validate budget period
        if (entry.BudgetPeriod == null)
        {
            errors.Add($"Budget entry {entry.Id} is missing budget period reference");
        }

        // Validate year type
        if (entry.YearType == YearType.Prior && entry.EntryType != EntryType.Actual)
        {
            errors.Add($"Prior year entry {entry.Id} should be of type Actual, not {entry.EntryType}");
        }
    }

    /// <summary>
    /// Validates account number format according to GASB standards
    /// </summary>
    /// <param name="accountNumber">The account number to validate</param>
    /// <param name="accountId">The account ID for error reporting</param>
    /// <param name="accountName">The account name for error reporting</param>
    /// <param name="errors">The list to add errors to</param>
    private void ValidateAccountNumber(string accountNumber, int accountId, string accountName, List<string> errors)
    {
        // GASB account numbers typically follow patterns like:
        // - 1-3 digits for major categories
        // - Optional decimal points for sub-accounts
        // - Examples: "101", "405.1", "410.2.1"

        if (!System.Text.RegularExpressions.Regex.IsMatch(accountNumber, @"^\d+(\.\d+)*$"))
        {
            errors.Add($"Account {accountId} ({accountName}) has invalid account number format: {accountNumber}");
        }

        // Check for reasonable length
        if (accountNumber.Length > 20)
        {
            errors.Add($"Account {accountId} ({accountName}) has account number that is too long: {accountNumber}");
        }
    }

    /// <summary>
    /// Validates budget consistency across entries
    /// </summary>
    /// <param name="entries">The budget entries to validate</param>
    /// <param name="errors">The list to add errors to</param>
    private void ValidateBudgetConsistency(List<BudgetEntry> entries, List<string> errors)
    {
        // Group by account and check for duplicate entries of same type/year
        var duplicates = entries
            .Where(e => e.MunicipalAccount != null)
            .GroupBy(e => new { e.MunicipalAccountId, e.YearType, e.EntryType, e.BudgetPeriodId })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var duplicate in duplicates)
        {
            var key = duplicate.Key;
            errors.Add($"Duplicate budget entries found for account {key.MunicipalAccountId}, year {key.YearType}, type {key.EntryType}");
        }

        // Check for missing required entry types
        var accountsByPeriod = entries
            .Where(e => e.BudgetPeriod != null)
            .GroupBy(e => e.BudgetPeriodId)
            .ToList();

        foreach (var periodGroup in accountsByPeriod)
        {
            var periodEntries = periodGroup.ToList();
            var accountsInPeriod = periodEntries.Select(e => e.MunicipalAccountId).Distinct();

            foreach (var accountId in accountsInPeriod)
            {
                var accountEntries = periodEntries.Where(e => e.MunicipalAccountId == accountId).ToList();

                // Check if we have at least one budget entry for current year
                var hasCurrentBudget = accountEntries.Any(e => e.YearType == YearType.Budget && e.EntryType == EntryType.Budget);
                if (!hasCurrentBudget)
                {
                    errors.Add($"Account {accountId} is missing current year budget entry");
                }

                // Check if we have actual entries for prior year
                var hasPriorActual = accountEntries.Any(e => e.YearType == YearType.Prior && e.EntryType == EntryType.Actual);
                if (!hasPriorActual)
                {
                    errors.Add($"Account {accountId} is missing prior year actual entry");
                }
            }
        }
    }
}