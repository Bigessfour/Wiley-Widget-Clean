using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Services;

/// <summary>
/// Service for municipal accounting operations and budget analysis.
/// </summary>
public class MunicipalAccountingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<MunicipalAccountingService> _logger;

    /// <summary>
    /// Initializes a new instance of the MunicipalAccountingService class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">Logger instance for logging operations.</param>
    public MunicipalAccountingService(AppDbContext context, ILogger<MunicipalAccountingService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets budget analysis for a specific period.
    /// </summary>
    /// <param name="budgetPeriodId">The budget period ID.</param>
    /// <returns>Task that completes with the budget analysis.</returns>
    public async Task<BudgetAnalysis> GetBudgetAnalysisAsync(int budgetPeriodId)
    {
        _logger.LogInformation("Generating budget analysis for period {PeriodId}", budgetPeriodId);

        var accounts = await _context.MunicipalAccounts
            .Include(a => a.Department)
            .Where(a => a.BudgetPeriodId == budgetPeriodId)
            .ToListAsync();

        var analysis = new BudgetAnalysis
        {
            BudgetPeriodId = budgetPeriodId,
            TotalAccounts = accounts.Count,
            GeneratedAt = DateTime.UtcNow
        };

        // Calculate fund summaries
        analysis.FundSummaries = await GetFundSummariesAsync(accounts);

        // Calculate department summaries
        analysis.DepartmentSummaries = GetDepartmentSummaries(accounts);

        // Calculate overall totals
        analysis.TotalBudget = accounts.Sum(a => a.BudgetAmount);
        analysis.TotalBalance = accounts.Sum(a => a.Balance);
        analysis.Variance = analysis.TotalBalance - analysis.TotalBudget;

        // Calculate key ratios
        analysis.KeyRatios = CalculateKeyRatios(accounts);

        _logger.LogInformation("Budget analysis completed: {Funds} funds, {Departments} departments",
            analysis.FundSummaries.Count, analysis.DepartmentSummaries.Count);

        return analysis;
    }

    /// <summary>
    /// Gets fund summaries for the given accounts.
    /// </summary>
    /// <param name="accounts">The accounts to analyze.</param>
    /// <returns>Task that completes with the fund summaries.</returns>
    private async Task<List<FundSummary>> GetFundSummariesAsync(List<MunicipalAccount> accounts)
    {
        var fundSummaries = new List<FundSummary>();

        var accountsByFund = accounts.GroupBy(a => a.Fund).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var fundGroup in accountsByFund)
        {
            var fund = fundGroup.Key;
            var fundAccounts = fundGroup.Value;

            var summary = new FundSummary
            {
                Fund = fund,
                AccountCount = fundAccounts.Count,
                TotalBudget = fundAccounts.Sum(a => a.BudgetAmount),
                TotalBalance = fundAccounts.Sum(a => a.Balance),
                Variance = fundAccounts.Sum(a => a.Balance - a.BudgetAmount)
            };

            // Calculate fund-specific metrics
            summary.Metrics = await CalculateFundMetricsAsync(fund, fundAccounts);

            fundSummaries.Add(summary);
        }

        return fundSummaries.OrderBy(s => s.Fund.ToString()).ToList();
    }

    /// <summary>
    /// Gets department summaries for the given accounts.
    /// </summary>
    /// <param name="accounts">The accounts to analyze.</param>
    /// <returns>The department summaries.</returns>
    private List<DepartmentSummary> GetDepartmentSummaries(List<MunicipalAccount> accounts)
    {
        var departmentSummaries = new List<DepartmentSummary>();

        var accountsByDepartment = accounts
            .Where(a => a.Department != null)
            .GroupBy(a => a.Department!)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var deptGroup in accountsByDepartment)
        {
            var department = deptGroup.Key;
            var deptAccounts = deptGroup.Value;

            var summary = new DepartmentSummary
            {
                Department = department,
                AccountCount = deptAccounts.Count,
                TotalBudget = deptAccounts.Sum(a => a.BudgetAmount),
                TotalBalance = deptAccounts.Sum(a => a.Balance),
                Variance = deptAccounts.Sum(a => a.Balance - a.BudgetAmount)
            };

            // Calculate department metrics
            summary.Metrics = CalculateDepartmentMetrics(deptAccounts);

            departmentSummaries.Add(summary);
        }

        return departmentSummaries.OrderBy(s => s.Department?.Code).ToList();
    }

    /// <summary>
    /// Calculates fund-specific metrics.
    /// </summary>
    /// <param name="fund">The fund type.</param>
    /// <param name="accounts">The accounts in the fund.</param>
    /// <returns>Task that completes with the fund metrics.</returns>
    private Task<Dictionary<string, decimal>> CalculateFundMetricsAsync(FundType fund, List<MunicipalAccount> accounts)
    {
        var metrics = new Dictionary<string, decimal>();

        switch (fund)
        {
            case FundType.General:
                // General fund health metrics
                var revenue = accounts.Where(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                           a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                           a.Type == AccountType.Sales).Sum(a => a.BudgetAmount);
                var expenses = accounts.Where(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                           a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                           a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                           a.Type == AccountType.Depreciation).Sum(a => a.BudgetAmount);
                var assets = accounts.Where(a => a.Type == AccountType.Cash || a.Type == AccountType.Investments ||
                                          a.Type == AccountType.Receivables || a.Type == AccountType.Inventory ||
                                          a.Type == AccountType.FixedAssets).Sum(a => a.Balance);

                if (revenue > 0)
                    metrics["RevenueCoverage"] = (revenue - expenses) / revenue * 100;

                if (assets > 0)
                    metrics["AssetRatio"] = assets / revenue * 100;

                break;

            case FundType.Enterprise:
                // Enterprise fund self-sufficiency
                var operatingRevenue = accounts.Where(a => (a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                                             a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                                             a.Type == AccountType.Sales) &&
                                                         !a.Name.Contains("Transfer")).Sum(a => a.BudgetAmount);
                var operatingExpenses = accounts.Where(a => (a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                                          a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                                          a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                                          a.Type == AccountType.Depreciation) &&
                                                          !a.Name.Contains("Transfer")).Sum(a => a.BudgetAmount);

                if (operatingRevenue > 0)
                    metrics["OperatingMargin"] = (operatingRevenue - operatingExpenses) / operatingRevenue * 100;

                break;

            case FundType.Utility:
            case FundType.SpecialRevenue:
                // Utility fund metrics
                var utilityRevenue = accounts.Where(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                                           a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                                           a.Type == AccountType.Sales).Sum(a => a.BudgetAmount);
                var utilityExpenses = accounts.Where(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                                          a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                                          a.Type == AccountType.Insurance ||
                                                          a.Type == AccountType.Depreciation).Sum(a => a.BudgetAmount);
                var utilityAssets = accounts.Where(a => a.Type == AccountType.Cash || a.Type == AccountType.Investments ||
                                                        a.Type == AccountType.Receivables || a.Type == AccountType.Inventory ||
                                                        a.Type == AccountType.FixedAssets).Sum(a => a.Balance);

                if (utilityRevenue > 0)
                {
                    metrics["ExpenseRatio"] = utilityExpenses / utilityRevenue * 100;
                    metrics["AssetEfficiency"] = utilityRevenue / utilityAssets * 100;
                }

                break;
        }

        return Task.FromResult(metrics);
    }

    /// <summary>
    /// Calculates department-specific metrics.
    /// </summary>
    /// <param name="accounts">The accounts in the department.</param>
    /// <returns>The department metrics.</returns>
    private Dictionary<string, decimal> CalculateDepartmentMetrics(List<MunicipalAccount> accounts)
    {
        var metrics = new Dictionary<string, decimal>();

        var totalBudget = accounts.Sum(a => a.BudgetAmount);
        var totalBalance = accounts.Sum(a => a.Balance);

        if (totalBudget > 0)
        {
            metrics["BudgetUtilization"] = totalBalance / totalBudget * 100;
            metrics["VariancePercentage"] = (totalBalance - totalBudget) / totalBudget * 100;
        }

        // Department efficiency metrics
        var personnelCosts = accounts.Where(a => a.Name?.Contains("Personnel") == true ||
                                               a.Name?.Contains("Salary") == true).Sum(a => a.BudgetAmount);

        var totalExpenses = accounts.Where(a => IsExpenseAccountType(a.Type)).Sum(a => a.BudgetAmount);

        if (totalExpenses > 0)
            metrics["PersonnelRatio"] = personnelCosts / totalExpenses * 100;

        return metrics;
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
    /// Calculates key financial ratios.
    /// </summary>
    /// <param name="accounts">The accounts to analyze.</param>
    /// <returns>The key ratios.</returns>
    private Dictionary<string, decimal> CalculateKeyRatios(List<MunicipalAccount> accounts)
    {
        var ratios = new Dictionary<string, decimal>();

        // Overall financial health ratios
        var totalRevenue = accounts.Where(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                           a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                           a.Type == AccountType.Sales).Sum(a => a.BudgetAmount);
        var totalExpenses = accounts.Where(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                           a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                           a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                           a.Type == AccountType.Depreciation).Sum(a => a.BudgetAmount);
        var totalAssets = accounts.Where(a => a.Type == AccountType.Cash || a.Type == AccountType.Investments ||
                                          a.Type == AccountType.Receivables || a.Type == AccountType.Inventory ||
                                          a.Type == AccountType.FixedAssets).Sum(a => a.Balance);
        var totalLiabilities = accounts.Where(a => a.Type == AccountType.Payables || a.Type == AccountType.Debt ||
                                               a.Type == AccountType.AccruedLiabilities).Sum(a => a.Balance);

        if (totalRevenue > 0)
        {
            ratios["ExpenseRatio"] = totalExpenses / totalRevenue * 100;
            ratios["NetMargin"] = (totalRevenue - totalExpenses) / totalRevenue * 100;
        }

        if (totalAssets > 0)
        {
            ratios["LiabilityRatio"] = totalLiabilities / totalAssets * 100;
            ratios["EquityRatio"] = (totalAssets - totalLiabilities) / totalAssets * 100;
        }

        // Fund balance ratios
        var generalFundBalance = accounts.Where(a => a.Fund == FundType.General).Sum(a => a.Balance);
        if (totalExpenses > 0)
            ratios["GeneralFundRatio"] = generalFundBalance / totalExpenses * 100;

        return ratios;
    }

    /// <summary>
    /// Gets budget variance analysis.
    /// </summary>
    /// <param name="budgetPeriodId">The budget period ID.</param>
    /// <param name="threshold">The variance threshold percentage.</param>
    /// <returns>Task that completes with the variance analysis.</returns>
    public async Task<BudgetVarianceAnalysis> GetBudgetVarianceAnalysisAsync(int budgetPeriodId, decimal threshold = 10.0m)
    {
        _logger.LogInformation("Generating budget variance analysis for period {PeriodId} with {Threshold}% threshold",
            budgetPeriodId, threshold);

        var accounts = await _context.MunicipalAccounts
            .Include(a => a.Department)
            .Where(a => a.BudgetPeriodId == budgetPeriodId)
            .ToListAsync();

        var analysis = new BudgetVarianceAnalysis
        {
            BudgetPeriodId = budgetPeriodId,
            Threshold = threshold,
            GeneratedAt = DateTime.UtcNow
        };

        // Calculate variances
        var variances = new List<AccountVariance>();
        foreach (var account in accounts)
        {
            if (account.BudgetAmount == 0)
                continue;

            var varianceAmount = account.Balance - account.BudgetAmount;
            var variancePercent = Math.Abs(varianceAmount / account.BudgetAmount) * 100;

            if (variancePercent >= threshold)
            {
                variances.Add(new AccountVariance
                {
                    Account = account,
                    VarianceAmount = varianceAmount,
                    VariancePercent = variancePercent,
                    IsOverBudget = varianceAmount > 0
                });
            }
        }

        analysis.Variances = variances.OrderByDescending(v => Math.Abs(v.VariancePercent)).ToList();
        analysis.TotalAccountsAnalyzed = accounts.Count;
        analysis.AccountsOverThreshold = variances.Count;

        // Summary statistics
        if (variances.Any())
        {
            analysis.AverageVariancePercent = variances.Average(v => Math.Abs(v.VariancePercent));
            analysis.MaxVariancePercent = variances.Max(v => Math.Abs(v.VariancePercent));
            analysis.OverBudgetCount = variances.Count(v => v.IsOverBudget);
            analysis.UnderBudgetCount = variances.Count(v => !v.IsOverBudget);
        }

        _logger.LogInformation("Variance analysis completed: {TotalAccounts} analyzed, {OverThreshold} over {Threshold}% threshold",
            analysis.TotalAccountsAnalyzed, analysis.AccountsOverThreshold, threshold);

        return analysis;
    }

    /// <summary>
    /// Gets department performance analysis.
    /// </summary>
    /// <param name="budgetPeriodId">The budget period ID.</param>
    /// <returns>Task that completes with the department performance analysis.</returns>
    public async Task<DepartmentPerformanceAnalysis> GetDepartmentPerformanceAnalysisAsync(int budgetPeriodId)
    {
        _logger.LogInformation("Generating department performance analysis for period {PeriodId}", budgetPeriodId);

        var accounts = await _context.MunicipalAccounts
            .Include(a => a.Department)
            .Where(a => a.BudgetPeriodId == budgetPeriodId && a.Department != null)
            .ToListAsync();

        var analysis = new DepartmentPerformanceAnalysis
        {
            BudgetPeriodId = budgetPeriodId,
            GeneratedAt = DateTime.UtcNow
        };

        var departmentPerformances = new List<DepartmentPerformance>();

        var accountsByDepartment = accounts
            .Where(a => a.Department != null)
            .GroupBy(a => a.Department!)
            .ToDictionary(g => g.Key!, g => g.ToList());

        foreach (var deptGroup in accountsByDepartment)
        {
            var department = deptGroup.Key;
            var deptAccounts = deptGroup.Value;

            var performance = new DepartmentPerformance
            {
                Department = department,
                AccountCount = deptAccounts.Count,
                TotalBudget = deptAccounts.Sum(a => a.BudgetAmount),
                TotalActual = deptAccounts.Sum(a => a.Balance),
                Variance = deptAccounts.Sum(a => a.Balance - a.BudgetAmount)
            };

            // Calculate performance metrics
            if (performance.TotalBudget > 0)
            {
                performance.BudgetUtilization = performance.TotalActual / performance.TotalBudget * 100;
                performance.VariancePercentage = performance.Variance / performance.TotalBudget * 100;
            }

            // Department efficiency metrics
            var revenue = deptAccounts.Where(a => a.Type == AccountType.Taxes || a.Type == AccountType.Fees ||
                                           a.Type == AccountType.Grants || a.Type == AccountType.Interest ||
                                           a.Type == AccountType.Sales).Sum(a => a.BudgetAmount);
            var expenses = deptAccounts.Where(a => a.Type == AccountType.Salaries || a.Type == AccountType.Supplies ||
                                           a.Type == AccountType.Services || a.Type == AccountType.Utilities ||
                                           a.Type == AccountType.Maintenance || a.Type == AccountType.Insurance ||
                                           a.Type == AccountType.Depreciation).Sum(a => a.BudgetAmount);

            if (revenue > 0)
                performance.RevenueEfficiency = (revenue - expenses) / revenue * 100;

            departmentPerformances.Add(performance);
        }

        analysis.DepartmentPerformances = departmentPerformances
            .OrderByDescending(p => Math.Abs(p.VariancePercentage))
            .ToList();

        analysis.TotalDepartments = departmentPerformances.Count;
        analysis.DepartmentsOnBudget = departmentPerformances.Count(p => Math.Abs(p.VariancePercentage) <= 5);
        analysis.DepartmentsOverBudget = departmentPerformances.Count(p => p.VariancePercentage > 5);
        analysis.DepartmentsUnderBudget = departmentPerformances.Count(p => p.VariancePercentage < -5);

        _logger.LogInformation("Department performance analysis completed: {Departments} departments analyzed",
            analysis.TotalDepartments);

        return analysis;
    }
}

/// <summary>
/// Budget analysis result.
/// </summary>
public class BudgetAnalysis
{
    /// <summary>
    /// Gets or sets the budget period ID.
    /// </summary>
    public int BudgetPeriodId { get; set; }

    /// <summary>
    /// Gets or sets the total number of accounts.
    /// </summary>
    public int TotalAccounts { get; set; }

    /// <summary>
    /// Gets or sets the total budget amount.
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the total balance.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Gets or sets the total variance.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the fund summaries.
    /// </summary>
    public List<FundSummary> FundSummaries { get; set; } = new();

    /// <summary>
    /// Gets or sets the department summaries.
    /// </summary>
    public List<DepartmentSummary> DepartmentSummaries { get; set; } = new();

    /// <summary>
    /// Gets or sets the key financial ratios.
    /// </summary>
    public Dictionary<string, decimal> KeyRatios { get; set; } = new();

    /// <summary>
    /// Gets or sets when the analysis was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Fund summary.
/// </summary>
public class FundSummary
{
    /// <summary>
    /// Gets or sets the fund type.
    /// </summary>
    public FundType Fund { get; set; }

    /// <summary>
    /// Gets or sets the account count.
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the total budget.
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the total balance.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Gets or sets the variance.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the fund metrics.
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}

/// <summary>
/// Department summary.
/// </summary>
public class DepartmentSummary
{
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the account count.
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the total budget.
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the total balance.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Gets or sets the variance.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the department metrics.
    /// </summary>
    public Dictionary<string, decimal> Metrics { get; set; } = new();
}

/// <summary>
/// Budget variance analysis.
/// </summary>
public class BudgetVarianceAnalysis
{
    /// <summary>
    /// Gets or sets the budget period ID.
    /// </summary>
    public int BudgetPeriodId { get; set; }

    /// <summary>
    /// Gets or sets the variance threshold percentage.
    /// </summary>
    public decimal Threshold { get; set; }

    /// <summary>
    /// Gets or sets the total accounts analyzed.
    /// </summary>
    public int TotalAccountsAnalyzed { get; set; }

    /// <summary>
    /// Gets or sets the accounts over threshold.
    /// </summary>
    public int AccountsOverThreshold { get; set; }

    /// <summary>
    /// Gets or sets the average variance percentage.
    /// </summary>
    public decimal AverageVariancePercent { get; set; }

    /// <summary>
    /// Gets or sets the maximum variance percentage.
    /// </summary>
    public decimal MaxVariancePercent { get; set; }

    /// <summary>
    /// Gets or sets the over budget count.
    /// </summary>
    public int OverBudgetCount { get; set; }

    /// <summary>
    /// Gets or sets the under budget count.
    /// </summary>
    public int UnderBudgetCount { get; set; }

    /// <summary>
    /// Gets or sets the account variances.
    /// </summary>
    public List<AccountVariance> Variances { get; set; } = new();

    /// <summary>
    /// Gets or sets when the analysis was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Account variance.
/// </summary>
public class AccountVariance
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public MunicipalAccount? Account { get; set; }

    /// <summary>
    /// Gets or sets the variance amount.
    /// </summary>
    public decimal VarianceAmount { get; set; }

    /// <summary>
    /// Gets or sets the variance percentage.
    /// </summary>
    public decimal VariancePercent { get; set; }

    /// <summary>
    /// Gets or sets whether the account is over budget.
    /// </summary>
    public bool IsOverBudget { get; set; }
}

/// <summary>
/// Department performance analysis.
/// </summary>
public class DepartmentPerformanceAnalysis
{
    /// <summary>
    /// Gets or sets the budget period ID.
    /// </summary>
    public int BudgetPeriodId { get; set; }

    /// <summary>
    /// Gets or sets the total departments.
    /// </summary>
    public int TotalDepartments { get; set; }

    /// <summary>
    /// Gets or sets the departments on budget.
    /// </summary>
    public int DepartmentsOnBudget { get; set; }

    /// <summary>
    /// Gets or sets the departments over budget.
    /// </summary>
    public int DepartmentsOverBudget { get; set; }

    /// <summary>
    /// Gets or sets the departments under budget.
    /// </summary>
    public int DepartmentsUnderBudget { get; set; }

    /// <summary>
    /// Gets or sets the department performances.
    /// </summary>
    public List<DepartmentPerformance> DepartmentPerformances { get; set; } = new();

    /// <summary>
    /// Gets or sets when the analysis was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Department performance.
/// </summary>
public class DepartmentPerformance
{
    /// <summary>
    /// Gets or sets the department.
    /// </summary>
    public Department? Department { get; set; }

    /// <summary>
    /// Gets or sets the account count.
    /// </summary>
    public int AccountCount { get; set; }

    /// <summary>
    /// Gets or sets the total budget.
    /// </summary>
    public decimal TotalBudget { get; set; }

    /// <summary>
    /// Gets or sets the total actual.
    /// </summary>
    public decimal TotalActual { get; set; }

    /// <summary>
    /// Gets or sets the variance.
    /// </summary>
    public decimal Variance { get; set; }

    /// <summary>
    /// Gets or sets the budget utilization percentage.
    /// </summary>
    public decimal BudgetUtilization { get; set; }

    /// <summary>
    /// Gets or sets the variance percentage.
    /// </summary>
    public decimal VariancePercentage { get; set; }

    /// <summary>
    /// Gets or sets the revenue efficiency.
    /// </summary>
    public decimal RevenueEfficiency { get; set; }
}