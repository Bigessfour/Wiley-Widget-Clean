using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;
using Account = Intuit.Ipp.Data.Account;

namespace WileyWidget.Data;

/// <summary>
/// Interface for municipal account repository operations
/// </summary>
public interface IMunicipalAccountRepository
{
    /// <summary>
    /// Gets all municipal accounts
    /// </summary>
    /// <returns>A collection of all municipal accounts</returns>
    Task<IEnumerable<MunicipalAccount>> GetAllAsync();

    /// <summary>
    /// Gets a municipal account by ID
    /// </summary>
    /// <param name="id">The account ID</param>
    /// <returns>The municipal account, or null if not found</returns>
    Task<MunicipalAccount?> GetByIdAsync(int id);

    /// <summary>
    /// Gets municipal accounts by account number
    /// </summary>
    /// <param name="accountNumber">The account number</param>
    /// <returns>A collection of municipal accounts with the specified account number</returns>
    Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber);

    /// <summary>
    /// Gets municipal accounts by department
    /// </summary>
    /// <param name="departmentId">The department ID</param>
    /// <returns>A collection of municipal accounts in the specified department</returns>
    Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId);

    /// <summary>
    /// Gets municipal accounts by fund class
    /// </summary>
    /// <param name="fundClass">The fund class</param>
    /// <returns>A collection of municipal accounts in the specified fund class</returns>
    Task<IEnumerable<MunicipalAccount>> GetByFundClassAsync(FundClass fundClass);

    /// <summary>
    /// Gets municipal accounts by account type
    /// </summary>
    /// <param name="accountType">The account type</param>
    /// <returns>A collection of municipal accounts of the specified type</returns>
    Task<IEnumerable<MunicipalAccount>> GetByAccountTypeAsync(AccountType accountType);

    /// <summary>
    /// Gets child accounts for a parent account
    /// </summary>
    /// <param name="parentAccountId">The parent account ID</param>
    /// <returns>A collection of child accounts</returns>
    Task<IEnumerable<MunicipalAccount>> GetChildAccountsAsync(int parentAccountId);

    /// <summary>
    /// Gets the account hierarchy starting from a root account
    /// </summary>
    /// <param name="rootAccountId">The root account ID</param>
    /// <returns>A collection of accounts in the hierarchy</returns>
    Task<IEnumerable<MunicipalAccount>> GetAccountHierarchyAsync(int rootAccountId);

    /// <summary>
    /// Searches for municipal accounts by name
    /// </summary>
    /// <param name="searchTerm">The search term</param>
    /// <returns>A collection of matching municipal accounts</returns>
    Task<IEnumerable<MunicipalAccount>> SearchByNameAsync(string searchTerm);

    /// <summary>
    /// Adds a new municipal account
    /// </summary>
    /// <param name="account">The account to add</param>
    /// <returns>The added account with updated ID</returns>
    Task<MunicipalAccount> AddAsync(MunicipalAccount account);

    /// <summary>
    /// Updates an existing municipal account
    /// </summary>
    /// <param name="account">The account to update</param>
    /// <returns>A task representing the async operation</returns>
    Task<MunicipalAccount> UpdateAsync(MunicipalAccount account);

    /// <summary>
    /// Deletes a municipal account
    /// </summary>
    /// <param name="id">The account ID to delete</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// Checks if an account number already exists
    /// </summary>
    /// <param name="accountNumber">The account number to check</param>
    /// <param name="excludeId">Optional ID to exclude from the check (for updates)</param>
    /// <returns>True if the account number exists, false otherwise</returns>
    Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null);

    /// <summary>
    /// Gets the total count of municipal accounts
    /// </summary>
    /// <returns>The total count of accounts</returns>
    Task<int> GetCountAsync();

    /// <summary>
    /// Gets accounts that have budget entries for a specific budget period
    /// </summary>
    /// <param name="budgetPeriodId">The budget period ID</param>
    /// <returns>A collection of accounts with budget entries for the period</returns>
    Task<IEnumerable<MunicipalAccount>> GetAccountsWithBudgetEntriesAsync(int budgetPeriodId);

    /// <summary>
    /// Gets municipal accounts by fund
    /// </summary>
    /// <param name="fund">The fund name or identifier</param>
    /// <returns>A collection of municipal accounts in the specified fund</returns>
    Task<IEnumerable<MunicipalAccount>> GetByFundAsync(FundType fund);

    /// <summary>
    /// Gets active municipal accounts
    /// </summary>
    /// <returns>A collection of active municipal accounts</returns>
    Task<IEnumerable<MunicipalAccount>> GetActiveAsync();

    /// <summary>
    /// Synchronizes accounts from QuickBooks
    /// </summary>
    /// <returns>A task representing the async operation</returns>
    Task SyncFromQuickBooksAsync();

    /// <summary>
    /// Synchronizes accounts from QuickBooks with provided account list
    /// </summary>
    /// <param name="qbAccounts">List of QuickBooks accounts to sync</param>
    /// <returns>A task representing the async operation</returns>
    Task SyncFromQuickBooksAsync(List<Account> qbAccounts);

    /// <summary>
    /// Gets budget analysis data
    /// </summary>
    /// <param name="periodId">The budget period ID</param>
    /// <returns>Budget analysis results</returns>
    Task<object> GetBudgetAnalysisAsync(int periodId);

    /// <summary>
    /// Gets municipal accounts by type
    /// </summary>
    /// <param name="type">The account type</param>
    /// <returns>A collection of municipal accounts of the specified type</returns>
    Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type);
}