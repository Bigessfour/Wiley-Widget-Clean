using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;

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
        await using var context = await _contextFactory.CreateDbContextAsync();
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
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Gets a utility customer by account number
    /// </summary>
    public async Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.AccountNumber == accountNumber);
    }

    /// <summary>
    /// Gets customers by customer type
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.CustomerType == customerType)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers by service location
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.ServiceLocation == serviceLocation)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets all active customers
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.Status == CustomerStatus.Active)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets customers with outstanding balances
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.CurrentBalance > 0)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Searches customers by name or account number
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => (c.CompanyName != null && c.CompanyName.Contains(searchTerm)) || 
                       ((c.FirstName + " " + c.LastName).Contains(searchTerm)) || 
                       c.AccountNumber.Contains(searchTerm))
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new utility customer
    /// </summary>
    public async Task<UtilityCustomer> AddAsync(UtilityCustomer customer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.UtilityCustomers.Add(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Updates an existing utility customer
    /// </summary>
    public async Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.UtilityCustomers.Update(customer);
        await context.SaveChangesAsync();
        return customer;
    }

    /// <summary>
    /// Deletes a utility customer by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var customer = await context.UtilityCustomers.FindAsync(id);
        if (customer == null)
            return false;

        context.UtilityCustomers.Remove(customer);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a customer exists by account number
    /// </summary>
    public async Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.UtilityCustomers.Where(c => c.AccountNumber == accountNumber);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of customers
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers.CountAsync();
    }

    /// <summary>
    /// Gets customers outside city limits
    /// </summary>
    public async Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.UtilityCustomers
            .Where(c => c.ServiceLocation == ServiceLocation.OutsideCityLimits)
            .AsNoTracking()
            .ToListAsync();
    }
}
