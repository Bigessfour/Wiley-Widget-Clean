using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetPeriod data operations
/// </summary>
public class BudgetPeriodRepository : IBudgetPeriodRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetPeriodRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets all budget periods
    /// </summary>
    public async Task<IEnumerable<BudgetPeriod>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .OrderByDescending(b => b.Year)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a budget period by ID
    /// </summary>
    public async Task<BudgetPeriod?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    /// <summary>
    /// Gets a budget period by year
    /// </summary>
    public async Task<BudgetPeriod?> GetByYearAsync(int year)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Year == year);
    }

    /// <summary>
    /// Gets the active budget period
    /// </summary>
    public async Task<BudgetPeriod?> GetActiveAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.IsActive);
    }

    /// <summary>
    /// Gets budget periods by status
    /// </summary>
    public async Task<IEnumerable<BudgetPeriod>> GetByStatusAsync(BudgetStatus status)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .Where(b => b.Status == status)
            .OrderByDescending(b => b.Year)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new budget period
    /// </summary>
    public async Task<BudgetPeriod> AddAsync(BudgetPeriod budgetPeriod)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetPeriods.Add(budgetPeriod);
        await context.SaveChangesAsync();
        return budgetPeriod;
    }

    /// <summary>
    /// Updates an existing budget period
    /// </summary>
    public async Task<BudgetPeriod> UpdateAsync(BudgetPeriod budgetPeriod)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Detach any existing tracked entity with the same key
        var existingEntry = context.ChangeTracker.Entries<BudgetPeriod>()
            .FirstOrDefault(e => e.Entity.Id == budgetPeriod.Id);
        if (existingEntry != null)
        {
            existingEntry.State = EntityState.Detached;
        }
        
        context.BudgetPeriods.Update(budgetPeriod);
        await context.SaveChangesAsync();
        return budgetPeriod;
    }

    /// <summary>
    /// Deletes a budget period by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var budgetPeriod = await context.BudgetPeriods.FindAsync(id);
        if (budgetPeriod == null)
            return false;

        context.BudgetPeriods.Remove(budgetPeriod);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a budget period exists for the given year
    /// </summary>
    public async Task<bool> ExistsForYearAsync(int year, int? excludeId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.BudgetPeriods.Where(b => b.Year == year);
        
        if (excludeId.HasValue)
        {
            query = query.Where(b => b.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }

    /// <summary>
    /// Sets a budget period as active (deactivates others)
    /// </summary>
    public async Task<bool> SetActiveAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Deactivate all budget periods
        var allPeriods = await context.BudgetPeriods.ToListAsync();
        foreach (var period in allPeriods)
        {
            period.IsActive = false;
        }
        
        // Activate the specified budget period
        var targetPeriod = allPeriods.FirstOrDefault(b => b.Id == id);
        if (targetPeriod == null)
            return false;
        
        targetPeriod.IsActive = true;
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets the total number of budget periods
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetPeriods
            .AsNoTracking()
            .CountAsync();
    }
}
