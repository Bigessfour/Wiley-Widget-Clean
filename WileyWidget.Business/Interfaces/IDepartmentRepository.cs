#nullable enable

using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for Department entities.
/// Defines data access operations for municipal departments.
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Gets all departments.
    /// </summary>
    Task<IEnumerable<Department>> GetAllAsync();

    /// <summary>
    /// Gets a department by ID.
    /// </summary>
    Task<Department?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a department by code.
    /// </summary>
    Task<Department?> GetByCodeAsync(string code);

    /// <summary>
    /// Adds a new department.
    /// </summary>
    Task AddAsync(Department department);

    /// <summary>
    /// Updates an existing department.
    /// </summary>
    Task UpdateAsync(Department department);

    /// <summary>
    /// Deletes a department by ID.
    /// </summary>
    Task DeleteAsync(int id);
}

