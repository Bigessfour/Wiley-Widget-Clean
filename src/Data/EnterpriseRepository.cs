using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
using System.Globalization;
using Azure.Identity;
using Azure;

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
        try
        {
            // Simulate slow database query for testing timing
            await Task.Delay(1200);

            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Enterprises
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToListAsync();
        }
        catch (Azure.Identity.AuthenticationFailedException ex)
        {
            // Log authentication timeout/failure and rethrow for retry logic
            App.LogDebugEvent("AZURE_AUTH_ERROR", $"Azure authentication failed in GetAllAsync: {ex.Message}");
            throw;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            // Log Azure authorization errors and rethrow
            App.LogDebugEvent("AZURE_AUTH_ERROR", $"Azure authorization failed in GetAllAsync (Status: {ex.Status}): {ex.Message}");
            throw;
        }
        catch (TimeoutException ex)
        {
            // Log timeout errors and rethrow
            App.LogDebugEvent("DATABASE_TIMEOUT", $"Database operation timed out in GetAllAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    public async Task<Enterprise?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Enterprises
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    /// <summary>
    /// Gets an enterprise by name
    /// </summary>
    public async Task<Enterprise?> GetByNameAsync(string name)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        // Use client-side evaluation for case-insensitive comparison to ensure cross-provider compatibility
        return await Task.Run(() =>
            context.Enterprises
                .AsNoTracking()
                .AsEnumerable() // Switch to client-side evaluation
                .FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)));
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
        // Use client-side evaluation for case-insensitive comparison to ensure cross-provider compatibility
        var enterprises = await context.Enterprises
            .AsNoTracking()
            .ToListAsync();

        return enterprises.Any(e =>
            string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase) &&
            (!excludeId.HasValue || e.Id != excludeId.Value));
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

    /// <summary>
    /// Creates a new Enterprise instance by mapping values from headers to properties
    /// </summary>
    /// <param name="headerValueMap">Dictionary mapping header names to values</param>
    /// <returns>A new Enterprise instance with mapped properties</returns>
    public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap)
    {
        if (headerValueMap == null)
            throw new ArgumentNullException(nameof(headerValueMap));

        var enterprise = new Enterprise();

        // Define explicit mappings from common header names to property names
        var propertyMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Direct property names
            { "Name", "Name" },
            { "Description", "Description" },
            { "Type", "Type" },
            { "Notes", "Notes" },
            { "CitizenCount", "CitizenCount" },
            { "CurrentRate", "CurrentRate" },
            { "MonthlyExpenses", "MonthlyExpenses" },
            { "TotalBudget", "TotalBudget" },
            { "BudgetAmount", "BudgetAmount" },
            { "LastModified", "LastModified" },

            // Common alternative header names
            { "Enterprise Name", "Name" },
            { "EnterpriseName", "Name" },
            { "Service Name", "Name" },
            { "ServiceName", "Name" },
            { "Utility Name", "Name" },
            { "UtilityName", "Name" },

            { "Rate", "CurrentRate" },
            { "Monthly Rate", "CurrentRate" },
            { "MonthlyRate", "CurrentRate" },
            { "Current Rate", "CurrentRate" },

            { "Expenses", "MonthlyExpenses" },
            { "Monthly Expenses", "MonthlyExpenses" },
            { "Operating Expenses", "MonthlyExpenses" },
            { "OperatingExpenses", "MonthlyExpenses" },

            { "Citizens", "CitizenCount" },
            { "Citizen Count", "CitizenCount" },
            { "Population", "CitizenCount" },
            { "Customer Count", "CitizenCount" },
            { "CustomerCount", "CitizenCount" },

            { "Budget", "TotalBudget" },
            { "Total Budget", "TotalBudget" },
            { "Allocated Budget", "TotalBudget" },
            { "AllocatedBudget", "TotalBudget" },

            { "Budget Amount", "BudgetAmount" },

            { "Modified", "LastModified" },
            { "Last Modified", "LastModified" },
            { "Date Modified", "LastModified" },
            { "DateModified", "LastModified" },
            { "Updated", "LastModified" },
            { "Last Updated", "LastModified" }
        };

        foreach (var kvp in headerValueMap)
        {
            var headerName = kvp.Key;
            var value = kvp.Value;

            // Skip empty headers or values
            if (string.IsNullOrWhiteSpace(headerName) || string.IsNullOrWhiteSpace(value))
                continue;

            // Try to find the property mapping
            if (propertyMappings.TryGetValue(headerName, out var propertyName))
            {
                SetPropertyValue(enterprise, propertyName, value);
            }
            else
            {
                // Try direct property name match using reflection
                SetPropertyValue(enterprise, headerName, value);
            }
        }

        return enterprise;
    }

    /// <summary>
    /// Sets a property value on an Enterprise instance with type conversion
    /// </summary>
    private void SetPropertyValue(Enterprise enterprise, string propertyName, string value)
    {
        var property = typeof(Enterprise).GetProperty(propertyName);
        if (property == null || !property.CanWrite)
            return; // Property doesn't exist or is read-only

        try
        {
            object? convertedValue = null;

            // Handle different property types
            if (property.PropertyType == typeof(string))
            {
                convertedValue = value;
            }
            else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
            {
                if (int.TryParse(value, out var intValue))
                    convertedValue = intValue;
            }
            else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
            {
                if (decimal.TryParse(value, NumberStyles.Currency | NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
                    convertedValue = decimalValue;
            }
            else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    convertedValue = dateValue;
            }

            // Set the value if conversion was successful
            if (convertedValue != null)
            {
                property.SetValue(enterprise, convertedValue);
            }
        }
        catch
        {
            // Silently ignore conversion errors - the property won't be set
        }
    }
}
