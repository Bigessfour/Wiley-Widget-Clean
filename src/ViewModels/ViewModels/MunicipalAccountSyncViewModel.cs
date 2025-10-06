using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;
using System.Threading.Tasks;

namespace WileyWidget.ViewModels;

/// <summary>
/// ViewModel for municipal account synchronization operations
/// Handles QuickBooks integration and external data synchronization
/// </summary>
public partial class MunicipalAccountSyncViewModel : AsyncViewModelBase
{
    private readonly IMunicipalAccountRepository _accountRepository;
    private readonly IQuickBooksService? _quickBooksService;

    /// <summary>
    /// Whether QuickBooks service is available
    /// </summary>
    public bool IsQuickBooksAvailable => _quickBooksService != null;

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
    /// Constructor with dependency injection
    /// </summary>
    public MunicipalAccountSyncViewModel(
        IMunicipalAccountRepository accountRepository,
        IQuickBooksService? quickBooksService,
        IDispatcherHelper dispatcherHelper,
        ILogger<MunicipalAccountSyncViewModel> logger)
        : base(dispatcherHelper, logger)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _quickBooksService = quickBooksService;
    }

    /// <summary>
    /// Sync accounts from QuickBooks
    /// </summary>
    [RelayCommand]
    public async Task SyncFromQuickBooksAsync()
    {
        if (_quickBooksService == null)
        {
            ErrorMessage = "QuickBooks service not configured";
            HasError = true;
            StatusMessage = "Service not available";
            Logger.LogWarning("QuickBooks sync attempted but service not configured");
            return;
        }

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            Logger.LogInformation("Starting QuickBooks synchronization");

            var qbAccounts = await _quickBooksService.GetChartOfAccountsAsync();
            Logger.LogInformation("Retrieved {Count} accounts from QuickBooks", qbAccounts?.Count ?? 0);

            if (qbAccounts != null)
            {
                await _accountRepository.SyncFromQuickBooksAsync(qbAccounts);
                Logger.LogInformation("QuickBooks synchronization completed successfully");
            }
            else
            {
                Logger.LogWarning("No accounts retrieved from QuickBooks");
                ErrorMessage = "No accounts retrieved from QuickBooks";
                HasError = true;
            }

        }, statusMessage: "Syncing from QuickBooks...");
    }

    /// <summary>
    /// Validate QuickBooks connection
    /// </summary>
    [RelayCommand]
    public async Task ValidateQuickBooksConnectionAsync()
    {
        if (_quickBooksService == null)
        {
            ErrorMessage = "QuickBooks service not configured";
            HasError = true;
            return;
        }

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var isConnected = await _quickBooksService.TestConnectionAsync();
            if (isConnected)
            {
                StatusMessage = "QuickBooks connection validated successfully";
                Logger.LogInformation("QuickBooks connection validation successful");
            }
            else
            {
                ErrorMessage = "QuickBooks connection validation failed";
                HasError = true;
                Logger.LogWarning("QuickBooks connection validation failed");
            }
        }, statusMessage: "Validating QuickBooks connection...");
    }
}