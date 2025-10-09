#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Service for municipal accounting operations and calculations
/// </summary>
public class MunicipalAccountingService
{
    /// <summary>
    /// Calculates the total budget for a collection of budget entries
    /// </summary>
    /// <param name="entries">The budget entries to calculate total for</param>
    /// <returns>The total budget amount</returns>
    public decimal CalculateTotalBudget(IEnumerable<BudgetEntry> entries)
    {
        return entries?.Sum(e => e.Amount) ?? 0;
    }

    /// <summary>
    /// Calculates budget variance between planned and actual amounts
    /// </summary>
    /// <param name="plannedAmount">The planned budget amount</param>
    /// <param name="actualAmount">The actual spent amount</param>
    /// <returns>The variance amount (positive = under budget, negative = over budget)</returns>
    public decimal CalculateBudgetVariance(decimal plannedAmount, decimal actualAmount)
    {
        return plannedAmount - actualAmount;
    }

    /// <summary>
    /// Calculates the variance percentage
    /// </summary>
    /// <param name="plannedAmount">The planned budget amount</param>
    /// <param name="actualAmount">The actual spent amount</param>
    /// <returns>The variance percentage</returns>
    public decimal CalculateVariancePercentage(decimal plannedAmount, decimal actualAmount)
    {
        if (plannedAmount == 0)
            return 0;

        return ((plannedAmount - actualAmount) / plannedAmount) * 100;
    }

    /// <summary>
    /// Groups budget entries by fund
    /// </summary>
    /// <param name="entries">The budget entries to group</param>
    /// <returns>Dictionary of fund names to budget entries</returns>
    public Dictionary<string, List<BudgetEntry>> GroupByFund(IEnumerable<BudgetEntry> entries)
    {
        return entries?
            .Where(e => e.MunicipalAccount != null)
            .GroupBy(e => e.MunicipalAccount!.FundClass?.ToString() ?? "Unspecified")
            .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<string, List<BudgetEntry>>();
    }

    /// <summary>
    /// Groups budget entries by department
    /// </summary>
    /// <param name="entries">The budget entries to group</param>
    /// <returns>Dictionary of department names to budget entries</returns>
    public Dictionary<string, List<BudgetEntry>> GroupByDepartment(IEnumerable<BudgetEntry> entries)
    {
        return entries?
            .Where(e => e.MunicipalAccount != null)
            .GroupBy(e => e.MunicipalAccount!.Department?.Name ?? "Unspecified")
            .ToDictionary(g => g.Key, g => g.ToList()) ?? new Dictionary<string, List<BudgetEntry>>();
    }

    /// <summary>
    /// Validates GASB (Government Accounting Standards Board) compliance for budget entries
    /// </summary>
    /// <param name="entries">The budget entries to validate</param>
    /// <returns>List of validation errors, empty if compliant</returns>
    public List<string> ValidateGASBCompliance(IEnumerable<BudgetEntry> entries)
    {
        var errors = new List<string>();

        if (entries == null)
        {
            errors.Add("Budget entries collection cannot be null");
            return errors;
        }

        foreach (var entry in entries)
        {
            if (entry == null)
            {
                errors.Add("Budget entry cannot be null");
                continue;
            }

            if (entry.MunicipalAccount == null)
            {
                errors.Add($"Budget entry {entry.Id} is missing municipal account reference");
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.MunicipalAccount.AccountNumber?.Value))
            {
                errors.Add($"Budget entry missing account number: {entry.MunicipalAccount.Name ?? "Unknown"}");
            }

            if (entry.MunicipalAccount.FundClass == null)
            {
                errors.Add($"Budget entry missing fund class: {entry.MunicipalAccount.Name ?? "Unknown"}");
            }

            if (entry.Amount < 0)
            {
                errors.Add($"Budget entry has negative amount: {entry.MunicipalAccount.Name ?? "Unknown"} - {entry.Amount}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Generates a budget summary report
    /// </summary>
    /// <param name="entries">The budget entries to summarize</param>
    /// <returns>Budget summary information</returns>
    public async Task<BudgetSummary> GenerateBudgetSummaryAsync(IEnumerable<BudgetEntry> entries)
    {
        return await Task.Run(() =>
        {
            var entryList = entries?.ToList() ?? new List<BudgetEntry>();

            return new BudgetSummary
            {
                TotalBudget = CalculateTotalBudget(entryList),
                EntryCount = entryList.Count,
                FundCount = GroupByFund(entryList).Count,
                DepartmentCount = GroupByDepartment(entryList).Count,
                ValidationErrors = ValidateGASBCompliance(entryList)
            };
        });
    }
}

/// <summary>
/// Summary information for a budget
/// </summary>
public class BudgetSummary
{
    /// <summary>
    /// Gets or sets the total budget amount
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the number of budget entries
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Gets or sets the number of funds
    /// </summary>
    public int FundCount { get; set; }

    /// <summary>
    /// Gets or sets the number of departments
    /// </summary>
    public int DepartmentCount { get; set; }

    /// <summary>
    /// Gets or sets the validation errors
    /// </summary>
    public List<string> ValidationErrors { get; set; } = new();
}

/// <summary>
/// Service extension methods for budget analysis
/// </summary>
public static class MunicipalAccountingServiceExtensions
{
    /// <summary>
    /// Gets budget analysis for a specific period
    /// </summary>
    public static Task<dynamic> GetBudgetAnalysisAsync(this MunicipalAccountingService service, int periodId)
    {
        // Stub implementation - returns analysis structure
        dynamic result = new System.Dynamic.ExpandoObject();
        result.TotalAccounts = 0;
        result.TotalBudget = 0m;
        result.TotalBalance = 0m;
        result.Variance = 0m;
        result.KeyRatios = new Dictionary<string, decimal>();
        result.FundSummaries = new List<object>();
        result.DepartmentSummaries = new List<object>();
        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets budget variance analysis
    /// </summary>
    public static Task<dynamic> GetBudgetVarianceAnalysisAsync(this MunicipalAccountingService service, int periodId, decimal? threshold = null)
    {
        // Stub implementation - returns variance analysis structure
        dynamic result = new System.Dynamic.ExpandoObject();
        result.PeriodId = periodId;
        result.TotalVariance = 0m;
        result.VariancePercentage = 0m;
        result.Threshold = threshold ?? 0m;
        result.Message = "Variance analysis placeholder - implement with actual data";
        return Task.FromResult(result);
    }
}