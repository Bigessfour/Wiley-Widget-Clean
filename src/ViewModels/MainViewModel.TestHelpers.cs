using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

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
            : this(new TestUnitOfWork(), null, new TestAIService(), autoInitialize: false)
        {
        }

        // Minimal test/dummy implementations used only for test-time construction.
        private class TestUnitOfWork : IUnitOfWork
        {
            public Business.Interfaces.IEnterpriseRepository Enterprises => new TestEnterpriseRepository();
            public Business.Interfaces.IMunicipalAccountRepository MunicipalAccounts => new TestMunicipalAccountRepository();
            public Business.Interfaces.IUtilityCustomerRepository UtilityCustomers => new TestUtilityCustomerRepository();
            public Task<FiscalYearSettings?> GetFiscalYearSettingsAsync() => Task.FromResult<FiscalYearSettings?>(null);
            public Task SaveFiscalYearSettingsAsync(FiscalYearSettings settings) => Task.CompletedTask;
            public Task<int> SaveChangesAsync() => Task.FromResult(0);
            public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
            public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default) => operation();
            public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private class TestEnterpriseRepository : Business.Interfaces.IEnterpriseRepository
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
            public Task<IEnumerable<Enterprise>> GetByTypeAsync(string type) => Task.FromResult(Enumerable.Empty<Enterprise>());
        }

        private class TestMunicipalAccountRepository : Business.Interfaces.IMunicipalAccountRepository
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

        private class TestUtilityCustomerRepository : Business.Interfaces.IUtilityCustomerRepository
        {
            public Task<IEnumerable<UtilityCustomer>> GetAllAsync() => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<UtilityCustomer?> GetByIdAsync(int id) => Task.FromResult<UtilityCustomer?>(null);
            public Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<UtilityCustomer?>(null);
            public Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType) => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation) => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync() => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync() => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm) => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
            public Task<UtilityCustomer> AddAsync(UtilityCustomer customer) => Task.FromResult(customer);
            public Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer) => Task.FromResult(customer);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null) => Task.FromResult(false);
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync() => Task.FromResult(Enumerable.Empty<UtilityCustomer>());
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
