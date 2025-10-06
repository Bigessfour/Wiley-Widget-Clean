using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetEntry data operations
/// </summary>
public class BudgetEntryRepository : IBudgetEntryRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetEntryRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets all budget entries
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    public async Task<BudgetEntry?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    /// <summary>
    /// Gets budget entries for a specific budget period
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByPeriodAsync(int budgetPeriodId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .Where(b => b.BudgetPeriodId == budgetPeriodId)
            .OrderBy(b => b.MunicipalAccountId)
            .ToListAsync();
    }

    /// <summary>
    /// Gets budget entries for a specific municipal account
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByAccountAsync(int municipalAccountId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .Where(b => b.MunicipalAccountId == municipalAccountId)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets budget entries by year type
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByYearTypeAsync(YearType yearType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .Where(b => b.YearType == yearType)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets budget entries by entry type
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByEntryTypeAsync(EntryType entryType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .Where(b => b.EntryType == entryType)
            .OrderByDescending(b => b.CreatedDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets budget entries for a specific account and period
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByAccountAndPeriodAsync(int municipalAccountId, int budgetPeriodId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Include(b => b.MunicipalAccount)
            .Include(b => b.BudgetPeriod)
            .Where(b => b.MunicipalAccountId == municipalAccountId && b.BudgetPeriodId == budgetPeriodId)
            .OrderBy(b => b.YearType)
            .ThenBy(b => b.EntryType)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    public async Task<BudgetEntry> AddAsync(BudgetEntry budgetEntry)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetEntries.Add(budgetEntry);
        await context.SaveChangesAsync();
        return budgetEntry;
    }

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    public async Task<BudgetEntry> UpdateAsync(BudgetEntry budgetEntry)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Detach any existing tracked entity with the same key
        var existingEntry = context.ChangeTracker.Entries<BudgetEntry>()
            .FirstOrDefault(e => e.Entity.Id == budgetEntry.Id);
        if (existingEntry != null)
        {
            existingEntry.State = EntityState.Detached;
        }
        
        context.BudgetEntries.Update(budgetEntry);
        await context.SaveChangesAsync();
        return budgetEntry;
    }

    /// <summary>
    /// Deletes a budget entry by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var budgetEntry = await context.BudgetEntries.FindAsync(id);
        if (budgetEntry == null)
            return false;

        context.BudgetEntries.Remove(budgetEntry);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets the total amount for a budget period
    /// </summary>
    public async Task<decimal> GetTotalAmountByPeriodAsync(int budgetPeriodId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .Where(b => b.BudgetPeriodId == budgetPeriodId)
            .SumAsync(b => b.Amount);
    }

    /// <summary>
    /// Gets the total number of budget entries
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .AsNoTracking()
            .CountAsync();
    }

    /// <summary>
    /// Bulk adds multiple budget entries
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> AddRangeAsync(IEnumerable<BudgetEntry> budgetEntries)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entries = budgetEntries.ToList();
        await context.BudgetEntries.AddRangeAsync(entries);
        await context.SaveChangesAsync();
        return entries;
    }

    /// <summary>
    /// Bulk updates multiple budget entries
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> UpdateRangeAsync(IEnumerable<BudgetEntry> budgetEntries)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var entries = budgetEntries.ToList();
        
        // Detach any existing tracked entities
        foreach (var entry in entries)
        {
            var existingEntry = context.ChangeTracker.Entries<BudgetEntry>()
                .FirstOrDefault(e => e.Entity.Id == entry.Id);
            if (existingEntry != null)
            {
                existingEntry.State = EntityState.Detached;
            }
        }
        
        context.BudgetEntries.UpdateRange(entries);
        await context.SaveChangesAsync();
        return entries;
    }
}
