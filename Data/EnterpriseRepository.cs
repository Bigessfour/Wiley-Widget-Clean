using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System.Globalization;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Enterprise data operations
/// </summary>
public class EnterpriseRepository : IEnterpriseRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets all enterprises
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetAllAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    public async Task<Enterprise> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    /// Gets an enterprise by name
    /// </summary>
    public async Task<Enterprise> GetByNameAsync(string name)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Name.ToLower(CultureInfo.InvariantCulture) == name.ToLower(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    public async Task<Enterprise> AddAsync(Enterprise enterprise)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Enterprises.Add(enterprise);
        await context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Updates an existing enterprise
    /// </summary>
    public async Task<Enterprise> UpdateAsync(Enterprise enterprise)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Enterprises.Update(enterprise);
        await context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Find the entity from the database; if not found, return false
        var entity = await context.Enterprises.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        // Ensure we are not double-tracking a different instance
        var local = context.Enterprises.Local.FirstOrDefault(e => e.Id == id);
        if (local != null && !ReferenceEquals(local, entity))
        {
            context.Entry(local).State = EntityState.Detached;
        }

        context.Enterprises.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if an enterprise exists by name
    /// </summary>
    public async Task<bool> ExistsByNameAsync(string name, int? excludeId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Enterprises.Where(e => e.Name.ToLower(CultureInfo.InvariantCulture) == name.ToLower(CultureInfo.InvariantCulture));

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of enterprises
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises.AsNoTracking().CountAsync();
    }

    /// <summary>
    /// Gets enterprises with their budget interactions
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetWithInteractionsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .Include(e => e.BudgetInteractions)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}
