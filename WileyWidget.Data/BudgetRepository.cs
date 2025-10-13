#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for BudgetEntry data operations
/// </summary>
public class BudgetRepository : IBudgetRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public BudgetRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.GetBudgetHierarchy(fiscalYear).ToListAsync();
    }

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    public async Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.FiscalYear == fiscalYear)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    public async Task<BudgetEntry?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.BudgetEntries
            .Include(be => be.Parent)
            .Include(be => be.Children)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .AsNoTracking()
            .FirstOrDefaultAsync(be => be.Id == id);
    }

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    public async Task AddAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetEntries.Add(budgetEntry);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    public async Task UpdateAsync(BudgetEntry budgetEntry)
    {
        if (budgetEntry == null)
            throw new ArgumentNullException(nameof(budgetEntry));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.BudgetEntries.Update(budgetEntry);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var budgetEntry = await context.BudgetEntries.FindAsync(id);
        if (budgetEntry != null)
        {
            context.BudgetEntries.Remove(budgetEntry);
            await context.SaveChangesAsync();
        }
    }
}