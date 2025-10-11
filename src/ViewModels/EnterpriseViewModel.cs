using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.Business.Interfaces;
using System.Threading.Tasks;
using System;
using System.Linq;
using Serilog;
using System.Threading;
using System.Globalization;
using System.Diagnostics;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing municipal enterprises (Phase 1)
/// Provides data binding for enterprise CRUD operations and budget calculations
/// </summary>
public partial class EnterpriseViewModel : ObservableObject, IDisposable
{
    private readonly IEnterpriseRepository _enterpriseRepository;

    /// <summary>
    /// Collection of all enterprises for data binding
    /// </summary>
    public ObservableCollection<Enterprise> Enterprises { get; } = new();

    /// <summary>
    /// Currently selected enterprise in the UI
    /// </summary>
    private Enterprise _selectedEnterprise;
    public Enterprise SelectedEnterprise
    {
        get => _selectedEnterprise;
        set
        {
            if (_selectedEnterprise != value)
            {
                _selectedEnterprise = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Budget summary text for display
    /// </summary>
    private string _budgetSummaryText = "No budget data available";
    public string BudgetSummaryText
    {
        get => _budgetSummaryText;
        set
        {
            if (_budgetSummaryText != value)
            {
                _budgetSummaryText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Semaphore to prevent concurrent loading operations
    /// </summary>
    private readonly SemaphoreSlim _loadSemaphore = new(1, 1);

    /// <summary>
    /// Executes an operation with retry logic and exponential backoff
    /// </summary>
    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
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

    /// <summary>
    /// Loads all enterprises from the database (public for View access)
    /// </summary>
    [RelayCommand]
    public async Task LoadEnterprisesAsync(CancellationToken cancellationToken = default)
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
            
            Enterprises.Clear();
            foreach (var enterprise in enterprises)
            {
                // Check for cancellation during UI updates
                cancellationToken.ThrowIfCancellationRequested();
                Enterprises.Add(enterprise);
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected behavior
            Log.Information("Enterprise loading was cancelled");
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Log.Error(ex, "Error loading enterprises");
        }
        finally
        {
            IsLoading = false;
            _loadSemaphore.Release();
        }
    }

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public EnterpriseViewModel(IEnterpriseRepository enterpriseRepository)
    {
        _enterpriseRepository = enterpriseRepository ?? throw new ArgumentNullException(nameof(enterpriseRepository));
    }

    /// <summary>
    /// Adds a new enterprise
    /// </summary>
    [RelayCommand]
    private async Task AddEnterpriseAsync()
    {
        try
        {
            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 0.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 0,
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await _enterpriseRepository.AddAsync(newEnterprise);
            Enterprises.Add(addedEnterprise);
            SelectedEnterprise = addedEnterprise;
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Log.Error(ex, "Error adding enterprise");
        }
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task SaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        try
        {
            // MonthlyRevenue is now automatically calculated from CitizenCount * CurrentRate
            await _enterpriseRepository.UpdateAsync(SelectedEnterprise);
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Log.Error(ex, "Error saving enterprise");
        }
    }

    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task DeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null) return;

        try
        {
            var success = await _enterpriseRepository.DeleteAsync(SelectedEnterprise.Id);
            if (success)
            {
                Enterprises.Remove(SelectedEnterprise);
                SelectedEnterprise = Enterprises.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            // TODO: Add proper error handling/logging
            Log.Error(ex, "Error deleting enterprise");
        }
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    [RelayCommand]
    private void UpdateBudgetSummary()
    {
        BudgetSummaryText = GetBudgetSummary();
    }

    /// <summary>
    /// Calculates and displays budget summary
    /// </summary>
    public string GetBudgetSummary()
    {
        if (!Enterprises.Any())
            return "No enterprises loaded";

        var totalRevenue = Enterprises.Sum(e => e.MonthlyRevenue);
        var totalExpenses = Enterprises.Sum(e => e.MonthlyExpenses);
        var totalBalance = totalRevenue - totalExpenses;
        var totalCitizens = Enterprises.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Total Expenses: ${totalExpenses.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Monthly Balance: ${totalBalance.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
}    /// <summary>
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
