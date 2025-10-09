using WileyWidget.Models;
using WileyWidget.Models.DTOs;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for Enterprise data operations
/// </summary>
public interface IEnterpriseRepository
{
    /// <summary>
    /// Gets all enterprises
    /// </summary>
    Task<IEnumerable<Enterprise>> GetAllAsync();

    /// <summary>
    /// Gets an enterprise by ID
    /// </summary>
    Task<Enterprise?> GetByIdAsync(int id);

    /// <summary>
    /// Gets an enterprise by name
    /// </summary>
    Task<Enterprise?> GetByNameAsync(string name);

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    Task<Enterprise> AddAsync(Enterprise enterprise);

    /// <summary>
    /// Updates an existing enterprise
    /// </summary>
    Task<Enterprise> UpdateAsync(Enterprise enterprise);

    /// <summary>
    /// Deletes an enterprise by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if an enterprise exists by name
    /// </summary>
    Task<bool> ExistsByNameAsync(string name, int? excludeId = null);

    /// <summary>
    /// Gets the total number of enterprises
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets enterprises with their budget interactions
    /// </summary>
    Task<IEnumerable<Enterprise>> GetWithInteractionsAsync();

    /// <summary>
    /// Creates a new Enterprise instance by mapping values from headers to properties
    /// </summary>
    /// <param name="headerValueMap">Dictionary mapping header names to values</param>
    /// <returns>A new Enterprise instance with mapped properties</returns>
    Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap);

    /// <summary>
    /// Gets enterprise summaries (lightweight DTOs for dashboards)
    /// </summary>
    Task<IEnumerable<EnterpriseSummary>> GetSummariesAsync();

    /// <summary>
    /// Gets summaries for active (non-deleted) enterprises only
    /// </summary>
    Task<IEnumerable<EnterpriseSummary>> GetActiveSummariesAsync();

    /// <summary>
    /// Soft deletes an enterprise (sets IsDeleted = true)
    /// </summary>
    Task<bool> SoftDeleteAsync(int id);

    /// <summary>
    /// Restores a soft-deleted enterprise
    /// </summary>
    Task<bool> RestoreAsync(int id);

    /// <summary>
    /// Gets all enterprises including soft-deleted ones
    /// </summary>
    Task<IEnumerable<Enterprise>> GetAllIncludingDeletedAsync();
}
