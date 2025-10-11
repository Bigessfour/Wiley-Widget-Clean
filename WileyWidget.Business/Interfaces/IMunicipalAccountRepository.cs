#nullable enable

using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for MunicipalAccount entities.
/// Defines data access operations for municipal accounting accounts.
/// </summary>
public interface IMunicipalAccountRepository
{
    /// <summary>
    /// Gets all municipal accounts.
    /// </summary>
    Task<IEnumerable<MunicipalAccount>> GetAllAsync();

    /// <summary>
    /// Gets a municipal account by ID.
    /// </summary>
    Task<MunicipalAccount?> GetByIdAsync(int id);

    /// <summary>
    /// Gets accounts by account number.
    /// </summary>
    Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber);

    /// <summary>
    /// Gets accounts by department.
    /// </summary>
    Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId);

    /// <summary>
    /// Adds a new municipal account.
    /// </summary>
    Task<MunicipalAccount> AddAsync(MunicipalAccount account);

    /// <summary>
    /// Updates an existing municipal account.
    /// </summary>
    Task<MunicipalAccount> UpdateAsync(MunicipalAccount account);

    /// <summary>
    /// Deletes a municipal account by ID.
    /// </summary>
    Task<bool> DeleteAsync(int id);
}

