using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Models;

#nullable enable

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel
    {
        /// <summary>
        /// Parameterless constructor used by tests and simple instantiation scenarios.
        /// Provides minimal in-memory implementations so tests can construct the ViewModel
        /// without full DI wiring or external services.
        /// </summary>
        public MainViewModel()
            : this(new TestEnterpriseRepository(), new TestMunicipalAccountRepository(), null, new TestAIService(), autoInitialize: false)
        {
        }

        // Minimal test/dummy implementations used only for test-time construction.
        private class TestEnterpriseRepository : IEnterpriseRepository
        {
            public Task<Enterprise> AddAsync(Enterprise enterprise) => Task.FromResult(enterprise);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task<bool> SoftDeleteAsync(int id) => Task.FromResult(true);
            public Task<bool> RestoreAsync(int id) => Task.FromResult(true);
            public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap) => new Enterprise { Id = 1, Name = headerValueMap.Values.FirstOrDefault() ?? "Test" };
            public Task<IEnumerable<Enterprise>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<IEnumerable<Enterprise>> GetAllIncludingDeletedAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<Enterprise> GetByIdAsync(int id) => Task.FromResult<Enterprise>(null!);
            public Task<Enterprise> GetByNameAsync(string name) => Task.FromResult<Enterprise>(null!);
            public Task<IEnumerable<Enterprise>> GetWithInteractionsAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<Enterprise> UpdateAsync(Enterprise enterprise) => Task.FromResult(enterprise);
            public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) => Task.FromResult(false);
            public Task<IEnumerable<Models.DTOs.EnterpriseSummary>> GetSummariesAsync() => Task.FromResult(Enumerable.Empty<Models.DTOs.EnterpriseSummary>());
            public Task<IEnumerable<Models.DTOs.EnterpriseSummary>> GetActiveSummariesAsync() => Task.FromResult(Enumerable.Empty<Models.DTOs.EnterpriseSummary>());
        }

        private class TestMunicipalAccountRepository : IMunicipalAccountRepository
        {
            public Task<MunicipalAccount> AddAsync(MunicipalAccount account) => Task.FromResult(account);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task<IEnumerable<MunicipalAccount>> GetAllAsync() => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<MunicipalAccount?>(null);
            public Task<MunicipalAccount?> GetByIdAsync(int id) => Task.FromResult<MunicipalAccount?>(null);
            public Task<IEnumerable<MunicipalAccount>> GetActiveAsync() => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByFundAsync(FundType fund) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByTypeAsync(AccountType type) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<object> GetBudgetAnalysisAsync(int periodId) => Task.FromResult<object>(new { });
            public Task SyncFromQuickBooksAsync() => Task.CompletedTask;
            public Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts) => Task.CompletedTask;
            public Task<MunicipalAccount> UpdateAsync(MunicipalAccount account) => Task.FromResult(account);
            public Task<IEnumerable<MunicipalAccount>> GetByDepartmentAsync(int departmentId) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByFundClassAsync(FundClass fundClass) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetByAccountTypeAsync(AccountType accountType) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetChildAccountsAsync(int parentAccountId) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> GetAccountHierarchyAsync(int rootAccountId) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<IEnumerable<MunicipalAccount>> SearchByNameAsync(string searchTerm) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
            public Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null) => Task.FromResult(false);
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<IEnumerable<MunicipalAccount>> GetAccountsWithBudgetEntriesAsync(int budgetPeriodId) => Task.FromResult<IEnumerable<MunicipalAccount>>(new List<MunicipalAccount>());
        }

        private class TestAIService : IAIService
        {
            public Task<string> GetInsightsAsync(string context, string question, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> AnalyzeDataAsync(string data, string analysisType, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
            public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        }
    }
}
