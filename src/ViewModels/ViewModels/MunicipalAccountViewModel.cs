#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Events;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing municipal accounts and budget analysis
/// </summary>
public partial class MunicipalAccountViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly IQuickBooksService? _quickBooksService;

    /// <summary>
    /// Collection of all municipal accounts (flat list)
    /// </summary>
    public ThreadSafeObservableCollection<MunicipalAccount> MunicipalAccounts { get; }

    /// <summary>
    /// Collection of accounts for budget analysis
    /// </summary>
    public ThreadSafeObservableCollection<MunicipalAccount> BudgetAnalysis { get; }

    /// <summary>
    /// Collection of departments
    /// </summary>
    public ObservableCollection<Department> Departments { get; } = new();

    /// <summary>
    /// Collection of root-level accounts (hierarchical structure)
    /// </summary>
    public ObservableCollection<MunicipalAccount> RootAccounts { get; } = new();

    /// <summary>
    /// Currently selected account in the grid
    /// </summary>
    [ObservableProperty]
    private MunicipalAccount? selectedAccount;

    /// <summary>
    /// Currently selected department for filtering
    /// </summary>
    [ObservableProperty]
    private Department? selectedDepartment;

    /// <summary>
    /// Whether QuickBooks operations are busy
    /// </summary>
    [ObservableProperty]
    private bool isBusy;

    /// <summary>
    /// Status message for operations
    /// </summary>
    [ObservableProperty]
    private string statusMessage = "Ready";

    /// <summary>
    /// Whether there's an error
    /// </summary>
    [ObservableProperty]
    private bool hasError;

    /// <summary>
    /// Error message if any
    /// </summary>
    [ObservableProperty]
    private string errorMessage = string.Empty;

    /// <summary>
    /// Selected fund type filter
    /// </summary>
    [ObservableProperty]
    private FundType selectedFundFilter = FundType.General;

    /// <summary>
    /// Selected account type filter
    /// </summary>
    [ObservableProperty]
    private AccountType selectedTypeFilter = AccountType.Cash;

    /// <summary>
    /// Progress percentage for long-running operations
    /// </summary>
    [ObservableProperty]
    private double operationProgress;

    public MunicipalAccountViewModel(
        IMunicipalAccountRepository accountRepository,
        IQuickBooksService? quickBooksService,
        IDispatcherHelper dispatcherHelper,
        Microsoft.Extensions.Logging.ILogger logger)
        : base(dispatcherHelper, logger)
    {
        var constructorTimer = Stopwatch.StartNew();
        App.LogDebugEvent("VIEWMODEL_INIT", "MunicipalAccountViewModel constructor started");

        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _quickBooksService = quickBooksService;

        App.LogDebugEvent("VIEWMODEL_INIT", "Initializing MunicipalAccounts and BudgetAnalysis collections");
        MunicipalAccounts = new ThreadSafeObservableCollection<MunicipalAccount>();
        BudgetAnalysis = new ThreadSafeObservableCollection<MunicipalAccount>();

        constructorTimer.Stop();
        App.LogDebugEvent("VIEWMODEL_INIT", $"MunicipalAccountViewModel constructor completed in {constructorTimer.ElapsedMilliseconds}ms");
        App.LogStartupTiming("MunicipalAccountViewModel Constructor", constructorTimer.Elapsed);
    }

    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        var progressReporter = new ProgressReporter((message, percentage) => OperationProgress = percentage);

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {

            // Load departments from accounts (since accounts include department navigation)
            var departments = MunicipalAccounts
                .Where(a => a.Department != null)
                .Select(a => a.Department!)
                .Distinct()
                .OrderBy(d => d.Name);
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                Departments.Clear();
                foreach (var dept in departments.OrderBy(d => d.Name))
                {
                    Departments.Add(dept);
                }
            });

            progressReporter.ReportProgress(25);

            // Load all accounts with hierarchical relationships
            var accounts = await _accountRepository.GetActiveAsync();
            await MunicipalAccounts.ReplaceAllAsync(accounts);

            progressReporter.ReportProgress(75);

            // Build hierarchical structure
            await DispatcherHelper.ExecuteOnUIThreadAsync(() =>
            {
                RootAccounts.Clear();
                var rootAccounts = accounts.Where(a => a.ParentAccountId == null)
                                          .OrderBy(a => a.AccountNumber.Value);
                foreach (var account in rootAccounts)
                {
                    RootAccounts.Add(account);
                }
            });

            progressReporter.ReportProgress(100);

        }, progressReporter, "Loading accounts and building hierarchy...");
    }

    /// <summary>
    /// Sync accounts from QuickBooks
    /// </summary>
    [RelayCommand]
    private async Task SyncFromQuickBooksAsync()
    {
        if (_quickBooksService == null)
        {
            ErrorMessage = "QuickBooks service not configured";
            HasError = true;
            StatusMessage = "Service not available";
            return;
        }

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var qbAccounts = await _quickBooksService.GetChartOfAccountsAsync();
            await _accountRepository.SyncFromQuickBooksAsync(qbAccounts);

            // Reload accounts after sync
            await LoadAccountsAsync();
        }, statusMessage: "Syncing from QuickBooks...");
    }

    /// <summary>
    /// Load budget analysis data
    /// </summary>
    [RelayCommand]
    private async Task LoadBudgetAnalysisAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetBudgetAnalysisAsync();
            await BudgetAnalysis.ReplaceAllAsync(accounts);
        }, statusMessage: "Loading budget analysis...");
    }

    /// <summary>
    /// Filter accounts by fund type
    /// </summary>
    [RelayCommand]
    private async Task FilterByFundAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetByFundAsync(SelectedFundFilter);
            await MunicipalAccounts.ReplaceAllAsync(accounts);
        }, statusMessage: $"Filtering by {SelectedFundFilter} fund...");
    }

    /// <summary>
    /// Filter accounts by account type
    /// </summary>
    [RelayCommand]
    private async Task FilterByTypeAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetByTypeAsync(SelectedTypeFilter);
            await MunicipalAccounts.ReplaceAllAsync(accounts);
        }, statusMessage: $"Filtering by {SelectedTypeFilter} type...");
    }

    /// <summary>
    /// Clear error state
    /// </summary>
    [RelayCommand]
    private void ClearError()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = "Ready";
    }

    /// <summary>
    /// Gets child accounts for a given parent account
    /// </summary>
    public IEnumerable<MunicipalAccount> GetChildAccounts(MunicipalAccount parentAccount)
    {
        return MunicipalAccounts.Where(a => a.ParentAccountId == parentAccount.Id)
                               .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Gets accounts for a specific department
    /// </summary>
    public IEnumerable<MunicipalAccount> GetAccountsForDepartment(Department department)
    {
        return MunicipalAccounts.Where(a => a.DepartmentId == department.Id)
                               .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Gets root accounts for a specific department
    /// </summary>
    public IEnumerable<MunicipalAccount> GetRootAccountsForDepartment(Department department)
    {
        return RootAccounts.Where(a => a.DepartmentId == department.Id)
                          .OrderBy(a => a.AccountNumber.Value);
    }

    /// <summary>
    /// Initialize the view model
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadAccountsAsync();
        await LoadBudgetAnalysisAsync();
    }
}