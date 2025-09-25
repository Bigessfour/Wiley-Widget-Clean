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
            public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap) => new Enterprise { Id = 1, Name = headerValueMap.Values.FirstOrDefault() ?? "Test" };
            public Task<IEnumerable<Enterprise>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<Enterprise> GetByIdAsync(int id) => Task.FromResult<Enterprise>(null!);
            public Task<Enterprise> GetByNameAsync(string name) => Task.FromResult<Enterprise>(null!);
            public Task<IEnumerable<Enterprise>> GetWithInteractionsAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<Enterprise> UpdateAsync(Enterprise enterprise) => Task.FromResult(enterprise);
            public Task<bool> ExistsByNameAsync(string name, int? excludeId = null) => Task.FromResult(false);
        }

        private class TestMunicipalAccountRepository : IMunicipalAccountRepository
        {
            public Task<MunicipalAccount> AddAsync(MunicipalAccount account) => Task.FromResult(account);
            public Task DeleteAsync(int id) => Task.CompletedTask;
            public Task<List<MunicipalAccount>> GetAllAsync() => Task.FromResult(new List<MunicipalAccount>());
            public Task<MunicipalAccount?> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<MunicipalAccount?>(null);
            public Task<MunicipalAccount?> GetByIdAsync(int id) => Task.FromResult<MunicipalAccount?>(null);
            public Task<List<MunicipalAccount>> GetActiveAsync() => Task.FromResult(new List<MunicipalAccount>());
            public Task<List<MunicipalAccount>> GetByFundAsync(FundType fund) => Task.FromResult(new List<MunicipalAccount>());
            public Task<List<MunicipalAccount>> GetByTypeAsync(AccountType type) => Task.FromResult(new List<MunicipalAccount>());
            public Task<List<MunicipalAccount>> GetBudgetAnalysisAsync() => Task.FromResult(new List<MunicipalAccount>());
            public Task SyncFromQuickBooksAsync(List<Intuit.Ipp.Data.Account> qbAccounts) => Task.CompletedTask;
            public Task<MunicipalAccount> UpdateAsync(MunicipalAccount account) => Task.FromResult(account);
        }

        private class TestAIService : IAIService
        {
            public Task<string> GetInsightsAsync(string context, string question) => Task.FromResult(string.Empty);
            public Task<string> AnalyzeDataAsync(string data, string analysisType) => Task.FromResult(string.Empty);
            public Task<string> ReviewApplicationAreaAsync(string areaName, string currentState) => Task.FromResult(string.Empty);
            public Task<string> GenerateMockDataSuggestionsAsync(string dataType, string requirements) => Task.FromResult(string.Empty);
        }
    }
}
