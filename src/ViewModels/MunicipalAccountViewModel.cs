using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WileyWidget.Business.Interfaces;
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
        var constructorTimer = Stopwatch.StartNew();
        App.LogDebugEvent("VIEWMODEL_INIT", "MunicipalAccountViewModel constructor started");

        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _quickBooksService = quickBooksService;

        App.LogDebugEvent("VIEWMODEL_INIT", "Initializing MunicipalAccounts and BudgetAnalysis collections");
        MunicipalAccounts = new ObservableCollection<MunicipalAccount>();
        BudgetAnalysis = new ObservableCollection<MunicipalAccount>();

        constructorTimer.Stop();
        App.LogDebugEvent("VIEWMODEL_INIT", $"MunicipalAccountViewModel constructor completed in {constructorTimer.ElapsedMilliseconds}ms");
        App.LogStartupTiming("MunicipalAccountViewModel Constructor", constructorTimer.Elapsed);
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
    private MunicipalFundType selectedFundFilter = MunicipalFundType.General;

    /// <summary>
    /// Selected account type filter
    /// </summary>
    [ObservableProperty]
    private AccountType selectedTypeFilter = AccountType.Asset;

    /// <summary>
    /// Search text for filtering accounts
    /// </summary>
    [ObservableProperty]
    private string searchText = string.Empty;

    /// <summary>
    /// Whether advanced filters are expanded
    /// </summary>
    [ObservableProperty]
    private bool isAdvancedFiltersExpanded;

    /// <summary>
    /// Minimum balance filter
    /// </summary>
    [ObservableProperty]
    private decimal? minBalanceFilter;

    /// <summary>
    /// Maximum balance filter
    /// </summary>
    [ObservableProperty]
    private decimal? maxBalanceFilter;

    /// <summary>
    /// Selected department filter
    /// </summary>
    [ObservableProperty]
    private Department? selectedDepartmentFilter;

    /// <summary>
    /// Load all municipal accounts from database
    /// </summary>
    [RelayCommand]
    private async Task LoadAccountsAsync()
    {
        var loadTimer = Stopwatch.StartNew();
        App.LogDebugEvent("DATA_LOADING", "Starting municipal accounts load");

        try
        {
            App.LogDebugEvent("DATA_LOADING", "Setting busy state and status message");
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading accounts...";

            App.LogDebugEvent("DATA_LOADING", "Querying account repository");
            var accountsEnum = await _accountRepository.GetAllAsync();
            var accounts = accountsEnum.ToList();

            App.LogDebugEvent("DATA_LOADING", $"Retrieved {accounts.Count} accounts, clearing and repopulating collection");
            MunicipalAccounts.Clear();
            foreach (var account in accounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Loaded {accounts.Count} accounts successfully";
            App.LogDebugEvent("DATA_LOADING", $"Successfully loaded {accounts.Count} municipal accounts");
            Log.Information("Loaded {Count} municipal accounts", accounts.Count);
        }
        catch (Exception ex)
        {
            App.LogDebugEvent("DATA_LOADING_ERROR", $"Failed to load municipal accounts: {ex.Message}");
            ErrorMessage = $"Failed to load accounts: {ex.Message}";
            HasError = true;
            StatusMessage = "Load failed";
            Log.Error(ex, "Failed to load municipal accounts");
        }
        finally
        {
            App.LogDebugEvent("DATA_LOADING", "Setting IsBusy = false");
            IsBusy = false;

            loadTimer.Stop();
            App.LogDebugEvent("DATA_LOADING", $"Municipal accounts load completed in {loadTimer.ElapsedMilliseconds}ms");
            App.LogStartupTiming("Municipal Accounts Load", loadTimer.Elapsed);
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

            // Get budget analysis returns an object - just log it for now
            var analysisResult = await _accountRepository.GetBudgetAnalysisAsync(periodId: 1);
            
            // Since the method returns object, we can't iterate it
            // This might need to be refactored based on what the actual return type should be
            StatusMessage = "Budget analysis loaded";
            Log.Information("Loaded budget analysis");
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

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} {SelectedFundFilter} accounts";
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

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} {SelectedTypeFilter} accounts";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to filter accounts: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to filter accounts by type");
        }
    }

    /// <summary>
    /// Apply comprehensive search and filters
    /// </summary>
    [RelayCommand]
    private async Task ApplyFiltersAsync()
    {
        try
        {
            IsBusy = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Applying filters...";

            // Get all accounts first
            var allAccounts = await _accountRepository.GetAllAsync();
            var filteredAccounts = allAccounts.AsEnumerable();

            // Apply search text filter
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                filteredAccounts = filteredAccounts.Where(a =>
                    a.Name.ToLowerInvariant().Contains(searchLower) ||
                    a.AccountNumber.Value.Contains(searchLower) ||
                    a.FundDescription.ToLowerInvariant().Contains(searchLower) ||
                    a.TypeDescription.ToLowerInvariant().Contains(searchLower) ||
                    (a.Notes?.ToLowerInvariant().Contains(searchLower) ?? false) ||
                    (a.Department?.Name.ToLowerInvariant().Contains(searchLower) ?? false));
            }

            // Apply fund type filter
            if (SelectedFundFilter != MunicipalFundType.General) // Assuming General means "All"
            {
                filteredAccounts = filteredAccounts.Where(a => a.Fund == SelectedFundFilter);
            }

            // Apply account type filter
            if (SelectedTypeFilter != AccountType.Asset) // Assuming Asset means "All"
            {
                filteredAccounts = filteredAccounts.Where(a => a.Type == SelectedTypeFilter);
            }

            // Apply balance range filters
            if (MinBalanceFilter.HasValue)
            {
                filteredAccounts = filteredAccounts.Where(a => a.Balance >= MinBalanceFilter.Value);
            }
            if (MaxBalanceFilter.HasValue)
            {
                filteredAccounts = filteredAccounts.Where(a => a.Balance <= MaxBalanceFilter.Value);
            }

            // Apply department filter
            if (SelectedDepartmentFilter != null)
            {
                filteredAccounts = filteredAccounts.Where(a => a.DepartmentId == SelectedDepartmentFilter.Id);
            }

            // Update the collection
            MunicipalAccounts.Clear();
            foreach (var account in filteredAccounts)
            {
                MunicipalAccounts.Add(account);
            }

            StatusMessage = $"Filtered to {MunicipalAccounts.Count} accounts";
            Log.Information("Applied filters, showing {Count} accounts", MunicipalAccounts.Count);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to apply filters: {ex.Message}";
            HasError = true;
            StatusMessage = "Filter failed";
            Log.Error(ex, "Failed to apply filters");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Clear all filters and show all accounts
    /// </summary>
    [RelayCommand]
    private async Task ClearFiltersAsync()
    {
        try
        {
            SearchText = string.Empty;
            SelectedFundFilter = MunicipalFundType.General;
            SelectedTypeFilter = AccountType.Asset;
            MinBalanceFilter = null;
            MaxBalanceFilter = null;
            SelectedDepartmentFilter = null;
            IsAdvancedFiltersExpanded = false;

            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to clear filters: {ex.Message}";
            HasError = true;
            Log.Error(ex, "Failed to clear filters");
        }
    }

    /// <summary>
    /// Navigate back to the main dashboard or parent view
    /// </summary>
    [RelayCommand]
    private void NavigateBack()
    {
        try
        {
            // Find the MunicipalAccountView window and close it
            var currentWindow = Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this);

            if (currentWindow != null)
            {
                currentWindow.Close();
                Log.Information("MunicipalAccountView closed via NavigateBack command");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to navigate back from MunicipalAccountView");
            ErrorMessage = $"Navigation error: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Export accounts to Excel
    /// </summary>
    [RelayCommand]
    private void ExportToExcel()
    {
        try
        {
            StatusMessage = "Export to Excel feature coming soon...";
            Log.Information("Export to Excel requested");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export accounts");
            ErrorMessage = $"Export failed: {ex.Message}";
            HasError = true;
        }
    }

    /// <summary>
    /// Print account report
    /// </summary>
    [RelayCommand]
    private void PrintReport()
    {
        try
        {
            StatusMessage = "Print report feature coming soon...";
            Log.Information("Print report requested");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to print report");
            ErrorMessage = $"Print failed: {ex.Message}";
            HasError = true;
        }
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
