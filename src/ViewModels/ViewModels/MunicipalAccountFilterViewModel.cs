using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for municipal account filtering operations
/// Handles filtering by fund type, account type, and other criteria
/// </summary>
public partial class MunicipalAccountFilterViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;

    /// <summary>
    /// Filtered collection of municipal accounts
    /// </summary>
    public ThreadSafeObservableCollection<MunicipalAccount> FilteredAccounts { get; } = new();

    /// <summary>
    /// Selected fund filter
    /// </summary>
    [ObservableProperty]
    private FundType? selectedFundFilter;

    /// <summary>
    /// Selected account type filter
    /// </summary>
    [ObservableProperty]
    private AccountType? selectedTypeFilter;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public MunicipalAccountFilterViewModel(
        IMunicipalAccountRepository accountRepository,
        IDispatcherHelper dispatcherHelper,
        ILogger<MunicipalAccountFilterViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    /// <summary>
    /// Filter accounts by fund type
    /// </summary>
    [RelayCommand]
    public async Task FilterByFundAsync()
    {
        if (SelectedFundFilter == null)
        {
            Logger.LogWarning("No fund filter selected");
            return;
        }

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetByFundAsync(SelectedFundFilter.Value);
            await FilteredAccounts.ReplaceAllAsync(accounts);
        }, statusMessage: $"Filtering by {SelectedFundFilter} fund...");
    }

    /// <summary>
    /// Filter accounts by account type
    /// </summary>
    [RelayCommand]
    public async Task FilterByTypeAsync()
    {
        if (SelectedTypeFilter == null)
        {
            Logger.LogWarning("No type filter selected");
            return;
        }

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetByTypeAsync(SelectedTypeFilter.Value);
            await FilteredAccounts.ReplaceAllAsync(accounts);
        }, statusMessage: $"Filtering by {SelectedTypeFilter} type...");
    }

    /// <summary>
    /// Clear all filters and show all accounts
    /// </summary>
    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        SelectedFundFilter = null;
        SelectedTypeFilter = null;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var accounts = await _accountRepository.GetActiveAsync();
            await FilteredAccounts.ReplaceAllAsync(accounts);
        }, statusMessage: "Clearing filters...");
    }
}