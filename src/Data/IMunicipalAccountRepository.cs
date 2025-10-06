#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Data
{
    /// <summary>
    /// Interface for MunicipalAccount data operations
    /// </summary>
    public interface IMunicipalAccountRepository
    {
        Task<List<MunicipalAccount>> GetAllAsync();
        Task<List<MunicipalAccount>> GetActiveAsync();
        Task<List<MunicipalAccount>> GetByFundAsync(FundType fund);
        Task<List<MunicipalAccount>> GetByTypeAsync(AccountType type);
        Task<MunicipalAccount?> GetByIdAsync(int id);
        Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber);
        Task<MunicipalAccount> AddAsync(MunicipalAccount account);
        Task<MunicipalAccount> UpdateAsync(MunicipalAccount account);
        Task DeleteAsync(int id);
        Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts);
        Task<List<MunicipalAccount>> GetBudgetAnalysisAsync();
    }
}