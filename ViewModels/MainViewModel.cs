using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Data;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Serilog;
using System;
using System.Threading;
using System.Diagnostics;

namespace WileyWidget.ViewModels;

/// <summary>
/// Main view model for managing municipal enterprises (Water, Sewer, Trash, Apartments)
/// Provides data binding for the main window's enterprise grid and operations.
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IQuickBooksService _quickBooksService;
    private readonly IEnterpriseRepository _enterpriseRepository;
    private readonly MunicipalAccountViewModel _municipalAccountViewModel;

    /// <summary>
    /// Semaphore to prevent concurrent loading operations
    /// </summary>
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    /// <summary>
    /// Executes an operation with retry logic and exponential backoff
    /// </summary>
    private async System.Threading.Tasks.Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, System.Threading.Tasks.Task<T>> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var delay = TimeSpan.FromMilliseconds(500);
        
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxRetries && 
                                     !(ex is OperationCanceledException))
            {
                Log.Warning(ex, "Attempt {Attempt} failed, retrying in {DelayMs}ms", 
                           attempt + 1, delay.TotalMilliseconds);
                await System.Threading.Tasks.Task.Delay(delay, cancellationToken);
                delay = delay * 2; // Exponential backoff
            }
        }
        
        throw new Exception($"Operation failed after {maxRetries + 1} attempts");
    }

    public ObservableCollection<Enterprise> Enterprises { get; } = new();

    public ObservableCollection<Customer> QuickBooksCustomers { get; } = new();
    public ObservableCollection<Invoice> QuickBooksInvoices { get; } = new();

    /// <summary>
    /// Municipal account view model for Chart of Accounts and Budget Analysis
    /// </summary>
    public MunicipalAccountViewModel MunicipalAccountViewModel => _municipalAccountViewModel;

    /// <summary>Currently selected enterprise in the grid (null when none selected).</summary>
    [ObservableProperty]
    private Enterprise selectedEnterprise;

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Current authenticated user name for display in UI
    /// </summary>
    [ObservableProperty]
    private string currentUserName = "Not signed in";

    [RelayCommand]
    /// <summary>
    /// Cycles to the next enterprise (wrap-around). If none selected, selects the first. Safe for empty list.
    /// </summary>
    private void SelectNext()
    {
        try
        {
            if (Enterprises.Count == 0)
                return;
            if (SelectedEnterprise == null)
            {
                SelectedEnterprise = Enterprises[0];
                return;
            }
            var idx = Enterprises.IndexOf(SelectedEnterprise);
            if (idx == -1)
            {
                // Selected enterprise not found in collection, select first
                Log.Warning("Selected enterprise not found in collection, selecting first enterprise");
                SelectedEnterprise = Enterprises[0];
                return;
            }
            idx = (idx + 1) % Enterprises.Count;
            SelectedEnterprise = Enterprises[idx];
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to select next enterprise");
            // Reset selection to first enterprise as fallback
            try
            {
                if (Enterprises.Count > 0)
                    SelectedEnterprise = Enterprises[0];
            }
            catch (Exception fallbackEx)
            {
                Log.Error(fallbackEx, "Failed to reset enterprise selection to first item");
            }
        }
    }

    [RelayCommand]
    /// <summary>
    /// Adds a new enterprise with default values for quick testing
    /// </summary>
    private void AddEnterprise()
    {
        try
        {
            var nextId = Enterprises.Count == 0 ? 1 : Enterprises[^1].Id + 1;
            var enterprise = new Enterprise
            {
                Id = nextId,
                Name = $"New Enterprise {nextId}",
                Type = "Utility",
                CurrentRate = 25.00M,
                MonthlyExpenses = 5000.00M,
                CitizenCount = 1000,
                TotalBudget = 60000.00M,
                Notes = "New municipal enterprise"
            };
            Enterprises.Add(enterprise);
            SelectedEnterprise = enterprise;
            Log.Information("Successfully added new enterprise: {Name} (ID: {Id})", enterprise.Name, enterprise.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to add new enterprise");
            // Could show user notification here if needed
        }
    }

    public MainViewModel(
        IEnterpriseRepository enterpriseRepository,
        IMunicipalAccountRepository municipalAccountRepository,
#pragma warning disable CS8632 // Nullable annotation is legitimate for optional QuickBooks service
        IQuickBooksService? quickBooksService)
#pragma warning restore CS8632
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
        _quickBooksService = quickBooksService;

        // Initialize Municipal Account View Model
        _municipalAccountViewModel = new MunicipalAccountViewModel(municipalAccountRepository, quickBooksService);

        // Load initial enterprise data with error handling
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await LoadEnterprisesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load enterprises during initialization");
            }
        });

        // Initialize municipal accounts with error handling
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await _municipalAccountViewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize municipal accounts during startup");
            }
        });
    }

    private async System.Threading.Tasks.Task LoadEnterprisesAsync(CancellationToken cancellationToken = default)
    {
        // Prevent concurrent loading operations
        if (!await _loadSemaphore.WaitAsync(0, cancellationToken))
        {
            Log.Information("Enterprise loading already in progress, skipping duplicate request");
            return;
        }
        
        try
        {
            IsLoading = true;
            
            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();
            
            var enterprises = await ExecuteWithRetryAsync(
                async (ct) => await _enterpriseRepository.GetAllAsync(),
                cancellationToken: cancellationToken);
            
            // Check for cancellation before updating UI
            cancellationToken.ThrowIfCancellationRequested();
            
            foreach (var enterprise in enterprises)
            {
                // Check for cancellation during UI updates
                cancellationToken.ThrowIfCancellationRequested();
                Enterprises.Add(enterprise);
            }

            // If no enterprises in database, add some sample data
            if (Enterprises.Count == 0)
            {
                var sampleEnterprises = new[]
                {
                    new Enterprise { Id = 1, Name = "Water Utility", Type = "Utility", CurrentRate = 25.00M, MonthlyExpenses = 15000.00M, CitizenCount = 2500, TotalBudget = 180000.00M, Notes = "Municipal water service" },
                    new Enterprise { Id = 2, Name = "Sewer Service", Type = "Utility", CurrentRate = 35.00M, MonthlyExpenses = 22000.00M, CitizenCount = 2500, TotalBudget = 264000.00M, Notes = "Wastewater treatment and sewer service" },
                    new Enterprise { Id = 3, Name = "Trash Collection", Type = "Service", CurrentRate = 15.00M, MonthlyExpenses = 8000.00M, CitizenCount = 2500, TotalBudget = 96000.00M, Notes = "Residential and commercial waste collection" },
                    new Enterprise { Id = 4, Name = "Municipal Apartments", Type = "Housing", CurrentRate = 450.00M, MonthlyExpenses = 12000.00M, CitizenCount = 150, TotalBudget = 144000.00M, Notes = "Low-income housing units" }
                };

                foreach (var enterprise in sampleEnterprises)
                {
                    // Check for cancellation during sample data loading
                    cancellationToken.ThrowIfCancellationRequested();
                    Enterprises.Add(enterprise);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected behavior
            Log.Information("Enterprise loading was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load enterprises");
            // Add fallback sample data
            var fallbackEnterprises = new[]
            {
                new Enterprise { Id = 1, Name = "Water Utility", Type = "Utility", CurrentRate = 25.00M, MonthlyExpenses = 15000.00M, CitizenCount = 2500, TotalBudget = 180000.00M },
                new Enterprise { Id = 2, Name = "Sewer Service", Type = "Utility", CurrentRate = 35.00M, MonthlyExpenses = 22000.00M, CitizenCount = 2500, TotalBudget = 264000.00M },
                new Enterprise { Id = 3, Name = "Trash Collection", Type = "Service", CurrentRate = 15.00M, MonthlyExpenses = 8000.00M, CitizenCount = 2500, TotalBudget = 96000.00M },
                new Enterprise { Id = 4, Name = "Municipal Apartments", Type = "Housing", CurrentRate = 450.00M, MonthlyExpenses = 12000.00M, CitizenCount = 150, TotalBudget = 144000.00M }
            };

            foreach (var enterprise in fallbackEnterprises)
            {
                Enterprises.Add(enterprise);
            }
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    [ObservableProperty]
    private bool quickBooksBusy;

    [ObservableProperty]
    private string quickBooksStatusMessage;

    [ObservableProperty]
    private string quickBooksErrorMessage;

    [ObservableProperty]
    private bool quickBooksHasError;

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync()
    {
        if (_quickBooksService == null)
        {
            QuickBooksErrorMessage = "QuickBooks service not configured. Please check settings.";
            QuickBooksHasError = true;
            Log.Warning("Attempted to load QuickBooks customers but service is not configured");
            return;
        }

        if (QuickBooksBusy) return;

        try
        {
            QuickBooksBusy = true;
            QuickBooksHasError = false;
            QuickBooksErrorMessage = null;
            QuickBooksStatusMessage = "Loading customers...";

            var items = await _quickBooksService.GetCustomersAsync();
            QuickBooksCustomers.Clear();
            foreach (var c in items) QuickBooksCustomers.Add(c);

            QuickBooksStatusMessage = $"Loaded {items.Count} customers successfully";
            Log.Information("Successfully loaded {Count} QuickBooks customers", items.Count);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("token") || ex.Message.Contains("authorization"))
        {
            QuickBooksErrorMessage = "QuickBooks authorization failed. Please re-authenticate in Settings.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Authorization error";
            Log.Error(ex, "QuickBooks authorization error while loading customers");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            QuickBooksErrorMessage = "Network error connecting to QuickBooks. Please check your internet connection.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Network error";
            Log.Error(ex, "Network error while loading QuickBooks customers");
        }
        catch (Exception ex)
        {
            QuickBooksErrorMessage = $"Failed to load customers: {ex.Message}";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Load failed";
            Log.Error(ex, "Unexpected error while loading QuickBooks customers");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task LoadQuickBooksInvoicesAsync()
    {
        if (_quickBooksService == null)
        {
            QuickBooksErrorMessage = "QuickBooks service not configured. Please check settings.";
            QuickBooksHasError = true;
            Log.Warning("Attempted to load QuickBooks invoices but service is not configured");
            return;
        }

        if (QuickBooksBusy) return;

        try
        {
            QuickBooksBusy = true;
            QuickBooksHasError = false;
            QuickBooksErrorMessage = null;
            QuickBooksStatusMessage = "Loading invoices...";

            var items = await _quickBooksService.GetInvoicesAsync();
            QuickBooksInvoices.Clear();
            foreach (var i in items) QuickBooksInvoices.Add(i);

            QuickBooksStatusMessage = $"Loaded {items.Count} invoices successfully";
            Log.Information("Successfully loaded {Count} QuickBooks invoices", items.Count);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("token") || ex.Message.Contains("authorization"))
        {
            QuickBooksErrorMessage = "QuickBooks authorization failed. Please re-authenticate in Settings.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Authorization error";
            Log.Error(ex, "QuickBooks authorization error while loading invoices");
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            QuickBooksErrorMessage = "Network error connecting to QuickBooks. Please check your internet connection.";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Network error";
            Log.Error(ex, "Network error while loading QuickBooks invoices");
        }
        catch (Exception ex)
        {
            QuickBooksErrorMessage = $"Failed to load invoices: {ex.Message}";
            QuickBooksHasError = true;
            QuickBooksStatusMessage = "Load failed";
            Log.Error(ex, "Unexpected error while loading QuickBooks invoices");
        }
        finally
        {
            QuickBooksBusy = false;
        }
    }

    [RelayCommand]
    private void ClearQuickBooksError()
    {
        QuickBooksErrorMessage = null;
        QuickBooksHasError = false;
        QuickBooksStatusMessage = "Error cleared";
        Log.Information("QuickBooks error cleared by user");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Refresh()
    {
        try
        {
            Log.Information("Manual refresh triggered - reloading all data");

            // Refresh enterprise data
            await LoadEnterprisesAsync();

            // Refresh QuickBooks data if service is available
            if (_quickBooksService != null)
            {
                await LoadQuickBooksCustomersAsync();
                await LoadQuickBooksInvoicesAsync();
            }

            Log.Information("Manual refresh completed successfully");
        }
        catch (OperationCanceledException)
        {
            Log.Information("Refresh operation was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to complete manual refresh operation");
            // Could show user notification here if needed
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Settings shortcut triggered");
    }

    [RelayCommand]
    private void OpenHelp()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Help shortcut triggered");
    }

    [RelayCommand]
    private void OpenEnterprise()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Enterprise shortcut triggered");
    }

    [RelayCommand]
    private void OpenBudget()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Budget shortcut triggered");
    }

    [RelayCommand]
    private void OpenDashboard()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("Dashboard shortcut triggered");
    }

    [RelayCommand]
    private void OpenAIAssist()
    {
        // This will be handled by the MainWindow event handler
        Log.Information("AI Assist shortcut triggered");
    }

    /// <summary>
    /// Disposes of managed resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes of managed and unmanaged resources
    /// </summary>
    /// <param name="disposing">True if called from Dispose(), false if called from finalizer</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadSemaphore?.Dispose();
        }
    }
}
