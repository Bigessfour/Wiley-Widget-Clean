#nullable enable
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for managing municipal accounts and budget analysis
/// </summary>
public partial class MunicipalAccountViewModel : ObservableObject
{
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly IQuickBooksService? _quickBooksService;

    public MunicipalAccountViewModel(
        IMunicipalAccountRepository accountRepository,
        IQuickBooksService? quickBooksService)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _quickBooksService = quickBooksService;

        MunicipalAccounts = new ObservableCollection<MunicipalAccount>();
        BudgetAnalysis = new ObservableCollection<MunicipalAccount>();
    }

    /// <summary>
    /// Collection of all municipal accounts
    /// </summary>
    public ObservableCollection<MunicipalAccount> MunicipalAccounts { get; }

    /// <summary>
    /// Collection of accounts for budget analysis
    /// </summary>
    public ObservableCollection<MunicipalAccount> BudgetAnalysis { get; }

    /// <summary>
    /// Currently selected account in the grid
    /// </summary>
    [ObservableProperty]
    private MunicipalAccount? selectedAccount;

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
    private AccountType selectedTypeFilter = AccountType.Asset;

    /// <summary>
    /// Load all municipal accounts from database
    /// </summary>
    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading accounts...";

            var accounts = await _accountRepository.GetActiveAsync();
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Loaded {accounts.Count} accounts successfully";
            Log.Information("Loaded {Count} municipal accounts", accounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load accounts: {ex.Message}";
            HasError = true;
            StatusMessage = "Load failed";
            Log.Error(ex, "Failed to load municipal accounts");
        }
        finally
        {
            IsBusy = false;
        }
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

        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Syncing from QuickBooks...";

            var qbAccounts = await _quickBooksService.GetChartOfAccountsAsync();
            await _accountRepository.SyncFromQuickBooksAsync(qbAccounts);

            // Reload accounts after sync
            await LoadAccountsAsync();

            StatusMessage = $"Synced {qbAccounts.Count} accounts from QuickBooks";
            Log.Information("Synced {Count} accounts from QuickBooks", qbAccounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to sync from QuickBooks: {ex.Message}";
            HasError = true;
            StatusMessage = "Sync failed";
            Log.Error(ex, "Failed to sync accounts from QuickBooks");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Load budget analysis data
    /// </summary>
    [RelayCommand]
    private async Task LoadBudgetAnalysisAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading budget analysis...";

            var accounts = await _accountRepository.GetBudgetAnalysisAsync();
            BudgetAnalysis.Clear();
            foreach (var account in accounts)
            {
                BudgetAnalysis.Add(account);
            }

            StatusMessage = $"Loaded budget analysis for {accounts.Count} accounts";
            Log.Information("Loaded budget analysis for {Count} accounts", accounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load budget analysis: {ex.Message}";
            HasError = true;
            StatusMessage = "Load failed";
            Log.Error(ex, "Failed to load budget analysis");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Filter accounts by fund type
    /// </summary>
    [RelayCommand]
    private async Task FilterByFundAsync()
    {
        try
        {
            var accounts = await _accountRepository.GetByFundAsync(SelectedFundFilter);
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Filtered to {accounts.Count} {SelectedFundFilter} accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to filter accounts: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to filter accounts by fund");
        }
    }

    /// <summary>
    /// Filter accounts by account type
    /// </summary>
    [RelayCommand]
    private async Task FilterByTypeAsync()
    {
        try
        {
            var accounts = await _accountRepository.GetByTypeAsync(SelectedTypeFilter);
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Filtered to {accounts.Count} {SelectedTypeFilter} accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to filter accounts: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to filter accounts by type");
        }
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
    /// Initialize the view model
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadAccountsAsync();
        await LoadBudgetAnalysisAsync();
    }
}