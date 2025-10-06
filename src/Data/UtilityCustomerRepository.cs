using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System.Globalization;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for UtilityCustomer data operations
/// </summary>
public class UtilityCustomerRepository : IUtilityCustomerRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public UtilityCustomerRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets all utility customers
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetAllAsync()
    {
        // Simulate slow database query for testing timing
        await Task.Delay(1000);

        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a utility customer by ID
    /// </summary>
    public async Task<UtilityCustomer?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Gets a utility customer by account number
    /// </summary>
    public async Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber);
    }

    /// <summary>
    /// Gets customers by customer type
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .Where(c => c.CustomerType == customerType)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers by service location
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .Where(c => c.ServiceLocation == serviceLocation)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets active customers only
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .Where(c => c.Status == CustomerStatus.Active)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers with outstanding balances
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();

        var customers = await context.UtilityCustomers
            .AsNoTracking()
            .Where(c => c.CurrentBalance > 0)
            .ToListAsync();

        return customers
            .OrderByDescending(c => c.CurrentBalance)
            .ThenBy(c => c.LastName)
            .ToList();
    }

    /// <summary>
    /// Searches customers by name or account number
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        using var context = await _contextFactory.CreateDbContextAsync();

        var term = $"%{searchTerm}%";
        return await context.UtilityCustomers
            .AsNoTracking()
            .Where(c =>
                EF.Functions.Like(c.FirstName, term) ||
                EF.Functions.Like(c.LastName, term) ||
                (c.CompanyName != null && EF.Functions.Like(c.CompanyName, term)) ||
                EF.Functions.Like(c.AccountNumber, term) ||
                (c.MeterNumber != null && EF.Functions.Like(c.MeterNumber, term)))
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new utility customer
    /// </summary>
    public async Task<UtilityCustomer> AddAsync(UtilityCustomer customer)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        customer.CreatedDate = DateTime.Now;
        customer.LastModifiedDate = DateTime.Now;

        context.UtilityCustomers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Updates an existing utility customer
    /// </summary>
    public async Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        customer.LastModifiedDate = DateTime.Now;

        context.UtilityCustomers.Update(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Deletes a utility customer by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Fetch from database first; if not found, return false
        var entity = await context.UtilityCustomers.FindAsync(id);
        if (entity == null)
        {
            return false;
        }

        // If a different instance is tracked locally, detach it to avoid conflicts
        var local = context.UtilityCustomers.Local.FirstOrDefault(e => e.Id == id);
        if (local != null && !ReferenceEquals(local, entity))
        {
            context.Entry(local).State = EntityState.Detached;
        }

        context.UtilityCustomers.Remove(entity);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a customer exists by account number
    /// </summary>
    public async Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.UtilityCustomers.Where(c => c.AccountNumber == accountNumber);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of customers
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers.AsNoTracking().CountAsync();
    }

    /// <summary>
    /// Gets customers outside city limits
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .Where(c => c.ServiceLocation == ServiceLocation.OutsideCityLimits)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }
}