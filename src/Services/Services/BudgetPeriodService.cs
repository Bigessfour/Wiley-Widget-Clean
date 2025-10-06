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
/// Service for managing budget periods and multi-year data
/// </summary>
public class BudgetPeriodService
{
    private readonly ILogger<BudgetPeriodService> _logger;
    private readonly AppDbContext _context;

    public BudgetPeriodService(
        ILogger<BudgetPeriodService> logger,
        AppDbContext context)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get or create a budget period for the specified year
    /// </summary>
    public async Task<BudgetPeriod> GetOrCreateBudgetPeriodAsync(int year, string? name = null)
    {
        var periodName = name ?? $"{year} Budget";

        var existingPeriod = await _context.BudgetPeriods
            .FirstOrDefaultAsync(bp => bp.Year == year && bp.Name == periodName);

        if (existingPeriod != null)
            return existingPeriod;

        var newPeriod = new BudgetPeriod
        {
            Year = year,
            Name = periodName,
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31),
            IsActive = true
        };

        _context.BudgetPeriods.Add(newPeriod);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created new budget period: {Name} ({Year})", periodName, year);
        return newPeriod;
    }

    /// <summary>
    /// Get all active budget periods
    /// </summary>
    public async Task<List<BudgetPeriod>> GetActiveBudgetPeriodsAsync()
    {
        return await _context.BudgetPeriods
            .Where(bp => bp.IsActive)
            .OrderByDescending(bp => bp.Year)
            .ToListAsync();
    }

    /// <summary>
    /// Get budget period by ID
    /// </summary>
    public async Task<BudgetPeriod?> GetBudgetPeriodByIdAsync(int id)
    {
        return await _context.BudgetPeriods.FindAsync(id);
    }

    /// <summary>
    /// Create multi-year budget entries for an account
    /// </summary>
    public async Task CreateMultiYearBudgetAsync(MunicipalAccount account, MultiYearBudgetData budgetData)
    {
        // Create entries for each year
        var budgetEntries = new List<BudgetEntry>();

        if (budgetData.PriorYear.HasValue)
        {
            budgetEntries.Add(new BudgetEntry
            {
                MunicipalAccountId = account.Id,
                BudgetPeriodId = account.BudgetPeriodId,
                YearType = YearType.Prior,
                Amount = budgetData.PriorYear.Value,
                EntryType = EntryType.Actual
            });
        }

        if (budgetData.SevenMonth.HasValue)
        {
            budgetEntries.Add(new BudgetEntry
            {
                MunicipalAccountId = account.Id,
                BudgetPeriodId = account.BudgetPeriodId,
                YearType = YearType.Current,
                Amount = budgetData.SevenMonth.Value,
                EntryType = EntryType.Actual
            });
        }

        if (budgetData.Estimate.HasValue)
        {
            budgetEntries.Add(new BudgetEntry
            {
                MunicipalAccountId = account.Id,
                BudgetPeriodId = account.BudgetPeriodId,
                YearType = YearType.Current,
                Amount = budgetData.Estimate.Value,
                EntryType = EntryType.Estimate
            });
        }

        if (budgetData.Budget.HasValue)
        {
            budgetEntries.Add(new BudgetEntry
            {
                MunicipalAccountId = account.Id,
                BudgetPeriodId = account.BudgetPeriodId,
                YearType = YearType.Budget,
                Amount = budgetData.Budget.Value,
                EntryType = EntryType.Budget
            });
        }

        await _context.BudgetEntries.AddRangeAsync(budgetEntries);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} budget entries for account {AccountNumber}",
            budgetEntries.Count, account.AccountNumber.Value);
    }

    /// <summary>
    /// Get budget summary for a specific period
    /// </summary>
    public async Task<BudgetSummary> GetBudgetSummaryAsync(int budgetPeriodId)
    {
        var accounts = await _context.MunicipalAccounts
            .Where(a => a.BudgetPeriodId == budgetPeriodId)
            .Include(a => a.BudgetEntries)
            .ToListAsync();

        var summary = new BudgetSummary
        {
            BudgetPeriodId = budgetPeriodId,
            TotalAccounts = accounts.Count,
            TotalBudgetAmount = accounts.Sum(a => a.BudgetAmount),
            TotalBalance = accounts.Sum(a => a.Balance)
        };

        // Calculate by fund type
        summary.FundSummaries = accounts
            .GroupBy(a => a.Fund)
            .Select(g => new FundSummary
            {
                Fund = g.Key,
                AccountCount = g.Count(),
                TotalBudget = g.Sum(a => a.BudgetAmount),
                TotalBalance = g.Sum(a => a.Balance)
            })
            .ToList();

        return summary;
    }
}

/// <summary>
/// Budget summary information
/// </summary>
public class BudgetSummary
{
    public int BudgetPeriodId { get; set; }
    public int TotalAccounts { get; set; }
    public decimal TotalBudgetAmount { get; set; }
    public decimal TotalBalance { get; set; }
    public List<FundSummary> FundSummaries { get; set; } = new();
}