using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Business.Interfaces;

/// <summary>
/// Repository interface for BudgetEntry operations
/// </summary>
public interface IBudgetRepository
{
    /// <summary>
    /// Gets budget hierarchy for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetBudgetHierarchyAsync(int fiscalYear);

    /// <summary>
    /// Gets all budget entries for a fiscal year
    /// </summary>
    Task<IEnumerable<BudgetEntry>> GetByFiscalYearAsync(int fiscalYear);

    /// <summary>
    /// Gets a budget entry by ID
    /// </summary>
    Task<BudgetEntry?> GetByIdAsync(int id);

    /// <summary>
    /// Adds a new budget entry
    /// </summary>
    Task AddAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Updates an existing budget entry
    /// </summary>
    Task UpdateAsync(BudgetEntry budgetEntry);

    /// <summary>
    /// Deletes a budget entry
    /// </summary>
    Task DeleteAsync(int id);
}