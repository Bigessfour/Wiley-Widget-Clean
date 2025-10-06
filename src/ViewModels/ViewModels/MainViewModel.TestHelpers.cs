using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Models;

#nullable enable

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel
    {
        // Minimal test/dummy implementations used only for test-time construction.

        private class TestLogger<T> : ILogger<T>
        {
            IDisposable ILogger.BeginScope<TState>(TState state) => new TestDisposable();
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Do nothing for tests
            }
        }

        private class TestDisposable : IDisposable
        {
            public void Dispose() { }
        }

        /// <summary>
        /// Parameterless constructor used by tests and simple instantiation scenarios.
        /// Provides minimal in-memory implementations so tests can construct the ViewModel
        /// without full DI wiring or external services.
        /// </summary>
        public MainViewModel()
            : this(new TestEnterpriseRepository(), new TestMunicipalAccountRepository(), null, new TestAIService(), new ProgressViewModel(new TestLogger<ProgressViewModel>()), new TestDispatcherHelper(), new TestLogger<MainViewModel>(), null, null, autoInitialize: false)
        {
        }
        private class TestEnterpriseRepository : IEnterpriseRepository
        {
            public Task<Enterprise> AddAsync(Enterprise enterprise) => Task.FromResult(enterprise);
            public Task<bool> DeleteAsync(int id) => Task.FromResult(true);
            public Enterprise CreateFromHeaderMapping(IDictionary<string, string> headerValueMap) => new Enterprise { Id = 1, Name = headerValueMap.Values.FirstOrDefault() ?? "Test" };
            public Task<IEnumerable<Enterprise>> GetAllAsync() => Task.FromResult(Enumerable.Empty<Enterprise>());
            public Task<int> GetCountAsync() => Task.FromResult(0);
            public Task<Enterprise?> GetByIdAsync(int id) => Task.FromResult<Enterprise?>(null);
            public Task<Enterprise?> GetByNameAsync(string name) => Task.FromResult<Enterprise?>(null);
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

        private class TestDispatcherHelper : Services.Threading.IDispatcherHelper
        {
            public System.Windows.Threading.Dispatcher Dispatcher => System.Windows.Threading.Dispatcher.CurrentDispatcher;
            public bool CheckAccess() => true;
            public Task InvokeAsync(Action action, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
            {
                action();
                return Task.CompletedTask;
            }
            public Task<T> InvokeAsync<T>(Func<T> func, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
                => Task.FromResult(func());
            public Task InvokeAsync(Func<Task> asyncAction, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
                => asyncAction();
            public Task<T> InvokeAsync<T>(Func<Task<T>> asyncFunc, System.Windows.Threading.DispatcherPriority priority = System.Windows.Threading.DispatcherPriority.Normal)
                => asyncFunc();
        }

        private class TestGrokSupercomputer : IGrokSupercomputer
        {
            public Task<ReportDataModel> FetchEnterpriseDataAsync(int? enterpriseId = null, DateTime? start = null, DateTime? end = null, string filter = "") =>
                Task.FromResult(new ReportDataModel(enterpriseId, start, end, filter, new List<EnterpriseMetric>()));
            public Task<AnalyticsResult> RunReportCalcsAsync(ReportDataModel data) =>
                Task.FromResult(new AnalyticsResult(data, new List<ChartSeries>(), new List<KpiMetric>(), null, null));
            public Task<double> CalculateAsync(string expression) => Task.FromResult(0.0);
            public Task<CalculationResult> CalculateAsync(string expression, string context = "") =>
                Task.FromResult(new CalculationResult());
            public Task<FinancialCalculationResult> CalculateFinancialAsync(decimal principal, decimal rate, int periods, string calculationType = "compound_interest") =>
                Task.FromResult(new FinancialCalculationResult());
            public Task<StatisticalAnalysisResult> AnalyzeStatisticsAsync(decimal[] data, string analysisType = "descriptive") =>
                Task.FromResult(new StatisticalAnalysisResult());
            public Task<OptimizationResult> OptimizeAsync(string objective, string[] constraints, string context = "") =>
                Task.FromResult(new OptimizationResult());
        }

        private class TestReportExportService : IReportExportService
        {
            public Task ExportToPdfAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task ExportToExcelAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
            public Task ExportToRdlAsync(ReportDataModel reportData, string filePath, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }
    }
}
