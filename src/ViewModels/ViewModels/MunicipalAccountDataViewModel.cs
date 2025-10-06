using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for municipal account data loading and basic operations
/// Handles loading accounts, departments, and budget analysis data
/// </summary>
public partial class MunicipalAccountDataViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;

    /// <summary>
    /// Collection of all municipal accounts (flat list)
    /// </summary>
    public ThreadSafeObservableCollection<MunicipalAccount> MunicipalAccounts { get; } = new();

    /// <summary>
    /// Collection of accounts for budget analysis
    /// </summary>
    public ThreadSafeObservableCollection<MunicipalAccount> BudgetAnalysis { get; } = new();

    /// <summary>
    /// Progress percentage for long-running operations
    /// </summary>
    [ObservableProperty]
    private double operationProgress;

    /// <summary>
    /// Collection of departments
    /// </summary>
    public ObservableCollection<Department> Departments { get; } = new();

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public MunicipalAccountDataViewModel(
        IMunicipalAccountRepository accountRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<MunicipalAccountDataViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    /// <summary>
    /// Load all accounts and build department list
    /// </summary>
    [RelayCommand]
    public async Task LoadAccountsAsync()
    {
        var progressReporter = new ProgressReporter((message, percentage) => OperationProgress = percentage);

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // Load all accounts
            var accounts = await _accountRepository.GetActiveAsync();
            await MunicipalAccounts.ReplaceAllAsync(accounts);

            progressReporter.ReportProgress(50);

            // Load departments from accounts
            var departments = MunicipalAccounts
                .Where(a => a.Department != null)
                .Select(a => a.Department!)
                .Distinct()
                .OrderBy(d => d.Name);

            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                Departments.Clear();
                foreach (var dept in departments)
                {
                    Departments.Add(dept);
                }
            });

            progressReporter.ReportProgress(100);

        }, progressReporter, "Loading accounts...");
    }

    /// <summary>
    /// Load budget analysis data
    /// </summary>
    [RelayCommand]
    public async Task LoadBudgetAnalysisAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetBudgetAnalysisAsync();
            await BudgetAnalysis.ReplaceAllAsync(accounts);
        }, statusMessage: "Loading budget analysis...");
    }

    /// <summary>
    /// Initialize the data ViewModel
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadAccountsAsync();
        await LoadBudgetAnalysisAsync();
    }
}