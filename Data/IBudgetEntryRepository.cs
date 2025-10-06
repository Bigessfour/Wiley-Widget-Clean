using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Repository interface for BudgetEntry data operations
/// </summary>
public interface IBudgetEntryRepository
{
    /// <summary>
    /// Gets all budget entries
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetAllAsync();

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    Task<BudgetEntry?> GetByIdAsync(int id);

    /// <summary>
    /// Gets budget entries for a specific budget period
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByPeriodAsync(int budgetPeriodId);

    /// <summary>
    /// Gets budget entries for a specific municipal account
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByAccountAsync(int municipalAccountId);

    /// <summary>
    /// Gets budget entries by year type
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByYearTypeAsync(YearType yearType);

    /// <summary>
    /// Gets budget entries by entry type
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByEntryTypeAsync(EntryType entryType);

    /// <summary>
    /// Gets budget entries for a specific account and period
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByAccountAndPeriodAsync(int municipalAccountId, int budgetPeriodId);

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    Task<BudgetEntry> AddAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    Task<BudgetEntry> UpdateAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Deletes a budget entry by ID
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Gets the total amount for a budget period
    /// </summary>
    Task<decimal> GetTotalAmountByPeriodAsync(int budgetPeriodId);

    /// <summary>
    /// Gets the total number of budget entries
    /// </summary>
    Task<int> GetCountAsync();

    /// <summary>
    /// Bulk adds multiple budget entries
    /// </summary>
    Task<IEnumerable<BudgetEntry>> AddRangeAsync(IEnumerable<BudgetEntry> budgetEntries);

    /// <summary>
    /// Bulk updates multiple budget entries
    /// </summary>
    Task<IEnumerable<BudgetEntry>> UpdateRangeAsync(IEnumerable<BudgetEntry> budgetEntries);
}
