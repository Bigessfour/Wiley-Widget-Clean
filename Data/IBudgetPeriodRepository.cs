using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for BudgetPeriod data operations
/// </summary>
public interface IBudgetPeriodRepository
{
    /// <summary>
    /// Gets all budget periods
    /// </summary>
    Task<IEnumerable<BudgetPeriod>> GetAllAsync();

    /// <summary>
    /// Gets a budget period by ID
    /// </summary>
    Task<BudgetPeriod?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a budget period by year
    /// </summary>
    Task<BudgetPeriod?> GetByYearAsync(int year);

    /// <summary>
    /// Gets the active budget period
    /// </summary>
    Task<BudgetPeriod?> GetActiveAsync();

    /// <summary>
    /// Gets budget periods by status
    /// </summary>
    Task<IEnumerable<BudgetPeriod>> GetByStatusAsync(BudgetStatus status);

    /// <summary>
    /// Adds a new budget period
    /// </summary>
    Task<BudgetPeriod> AddAsync(BudgetPeriod budgetPeriod);

    /// <summary>
    /// Updates an existing budget period
    /// </summary>
    Task<BudgetPeriod> UpdateAsync(BudgetPeriod budgetPeriod);

    /// <summary>
    /// Deletes a budget period by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if a budget period exists for the given year
    /// </summary>
    Task<bool> ExistsForYearAsync(int year, int? excludeId = null);

    /// <summary>
    /// Sets a budget period as active (deactivates others)
    /// </summary>
    Task<bool> SetActiveAsync(int id);

    /// <summary>
    /// Gets the total number of budget periods
    /// </summary>
    Task<int> GetCountAsync();
}
