using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for Department data operations
/// </summary>
public interface IDepartmentRepository
{
    /// <summary>
    /// Gets all departments
    /// </summary>
    Task<IEnumerable<Department>> GetAllAsync();

    /// <summary>
    /// Gets a department by ID
    /// </summary>
    Task<Department?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a department by code
    /// </summary>
    Task<Department?> GetByCodeAsync(string code);

    /// <summary>
    /// Gets departments by fund type
    /// </summary>
    Task<IEnumerable<Department>> GetByFundAsync(FundType fund);

    /// <summary>
    /// Gets root departments (no parent)
    /// </summary>
    Task<IEnumerable<Department>> GetRootDepartmentsAsync();

    /// <summary>
    /// Gets child departments for a parent department
    /// </summary>
    Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentDepartmentId);

    /// <summary>
    /// Gets a department with all its child departments (hierarchical)
    /// </summary>
    Task<Department?> GetWithChildrenAsync(int id);

    /// <summary>
    /// Gets a department with all its accounts
    /// </summary>
    Task<Department?> GetWithAccountsAsync(int id);

    /// <summary>
    /// Adds a new department
    /// </summary>
    Task<Department> AddAsync(Department department);

    /// <summary>
    /// Updates an existing department
    /// </summary>
    Task<Department> UpdateAsync(Department department);

    /// <summary>
    /// Deletes a department by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if a department exists by code
    /// </summary>
    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null);

    /// <summary>
    /// Gets the total number of departments
    /// </summary>
    Task<int> GetCountAsync();
}
