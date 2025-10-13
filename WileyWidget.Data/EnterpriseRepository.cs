using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

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
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    public async Task<Enterprise?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    /// Gets enterprises by type
    /// </summary>
    public async Task<IEnumerable<Enterprise>> GetByTypeAsync(string type)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .Where(e => e.Type == type)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    public async Task<Enterprise> AddAsync(Enterprise enterprise)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Enterprises.Add(enterprise);
        await context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Updates an existing enterprise
    /// </summary>
    public async Task<Enterprise> UpdateAsync(Enterprise enterprise)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Enterprises.Update(enterprise);
        await context.SaveChangesAsync();
        return enterprise;
    }

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var enterprise = await context.Enterprises.FindAsync(id);
        if (enterprise == null)
            return false;

        context.Enterprises.Remove(enterprise);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets the total count of enterprises
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises.CountAsync();
    }

    /// <summary>
    /// Creates an enterprise from header mapping
    /// </summary>
    public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap)
    {
        if (headerValueMap == null)
            throw new ArgumentNullException(nameof(headerValueMap));

        var enterprise = new Enterprise();

        foreach (var kvp in headerValueMap)
        {
            var key = kvp.Key.ToLowerInvariant();
            var value = kvp.Value?.Trim();

            switch (key)
            {
                case "name":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Name = value;
                    break;
                case "description":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Description = value;
                    break;
                case "currentrate":
                case "rate":
                    if (decimal.TryParse(value, out var rate))
                        enterprise.CurrentRate = rate;
                    break;
                case "monthlyexpenses":
                case "expenses":
                    if (decimal.TryParse(value, out var expenses))
                        enterprise.MonthlyExpenses = expenses;
                    break;
                case "citizencount":
                case "citizens":
                    if (int.TryParse(value, out var count))
                        enterprise.CitizenCount = count;
                    break;
                case "type":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Type = value;
                    break;
                case "notes":
                    if (!string.IsNullOrEmpty(value))
                        enterprise.Notes = value;
                    break;
            }
        }

        return enterprise;
    }
}
