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
using System.ComponentModel;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing municipal enterprises (Phase 1)
/// Provides data binding for enterprise CRUD operations and budget calculations
/// </summary>
public partial class EnterpriseViewModel : ObservableObject, IDisposable, IDataErrorInfo
{
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Collection of all enterprises for data binding
    /// </summary>
    public ObservableCollection<Enterprise> EnterpriseList { get; } = new();

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
                SelectionChangedCommand?.Execute(null);
            }
        }
    }

    /// <summary>
    /// Status message for user feedback
    /// </summary>
    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
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
    /// Selection changed command (no drill-down implementation)
    /// </summary>
    [RelayCommand]
    private void SelectionChanged()
    {
        // No drill-down implementation as requested
        // Could be used for future navigation or status updates
    }

    /// <summary>
    /// Navigate to BudgetView command
    /// </summary>
    [RelayCommand]
    private void NavigateToBudgetView()
    {
        // Navigation to BudgetView - implementation depends on navigation service
        // This could use messaging, navigation service, or window management
        // For now, this is a stub that can be implemented based on the app's navigation pattern
    }

    /// <summary>
    /// Export to Excel command
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAsync()
    {
        try
        {
            // This will be handled by the View - the command triggers UI interaction
            await Task.CompletedTask; // Placeholder
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in Excel export command");
        }
    }

    /// <summary>
    /// Export to PDF report command
    /// </summary>
    [RelayCommand]
    private async Task ExportToPdfReportAsync()
    {
        try
        {
            // TODO: Implement PDF report export
            // This would generate a comprehensive PDF with charts, summaries, etc.
            StatusMessage = "PDF report export feature coming soon...";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in PDF report export command");
        }
    }

    /// <summary>
    /// Export to Excel advanced command
    /// </summary>
    [RelayCommand]
    private async Task ExportToExcelAdvancedAsync()
    {
        try
        {
            // TODO: Implement advanced Excel export with formatting
            StatusMessage = "Advanced Excel export feature coming soon...";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in Excel advanced export command");
        }
    }

    /// <summary>
    /// Export to CSV command
    /// </summary>
    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        try
        {
            // TODO: Implement CSV export
            StatusMessage = "CSV export feature coming soon...";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in CSV export command");
        }
    }

    /// <summary>
    /// Export selection command
    /// </summary>
    [RelayCommand]
    private async Task ExportSelectionAsync()
    {
        try
        {
            // TODO: Implement selection export
            StatusMessage = "Selection export feature coming soon...";
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in selection export command");
        }
    }

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
                async (ct) => await _unitOfWork.Enterprises.GetAllAsync(),
                cancellationToken: cancellationToken);
            
            // Check for cancellation before updating UI
            cancellationToken.ThrowIfCancellationRequested();
            
            EnterpriseList.Clear();
            foreach (var enterprise in enterprises)
            {
                // Check for cancellation during UI updates
                cancellationToken.ThrowIfCancellationRequested();
                EnterpriseList.Add(enterprise);
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
    public EnterpriseViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

            var addedEnterprise = await _unitOfWork.Enterprises.AddAsync(newEnterprise);
            EnterpriseList.Add(addedEnterprise);
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
            await _unitOfWork.Enterprises.UpdateAsync(SelectedEnterprise);
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
            var success = await _unitOfWork.Enterprises.DeleteAsync(SelectedEnterprise.Id);
            if (success)
            {
                EnterpriseList.Remove(SelectedEnterprise);
                SelectedEnterprise = EnterpriseList.FirstOrDefault();
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
        if (!EnterpriseList.Any())
            return "No enterprises loaded";

        var totalRevenue = EnterpriseList.Sum(e => e.MonthlyRevenue);
        var totalExpenses = EnterpriseList.Sum(e => e.MonthlyExpenses);
        var totalBalance = totalRevenue - totalExpenses;
        var totalCitizens = EnterpriseList.Sum(e => e.CitizenCount);

        return $"Total Revenue: ${totalRevenue.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Total Expenses: ${totalExpenses.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Monthly Balance: ${totalBalance.ToString("N2", CultureInfo.InvariantCulture)}\n" +
               $"Citizens Served: {totalCitizens}\n" +
               $"Status: {(totalBalance >= 0 ? "Surplus" : "Deficit")}";
    }

    /// <summary>
    /// IDataErrorInfo implementation - validation stubs
    /// </summary>
    public string Error => null;

    /// <summary>
    /// IDataErrorInfo implementation - property-level validation
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                // Validation stubs - implement as needed
                "SelectedEnterprise.Name" => string.IsNullOrWhiteSpace(SelectedEnterprise?.Name) ? "Name is required" : null,
                "SelectedEnterprise.CurrentRate" => SelectedEnterprise?.CurrentRate < 0 ? "Rate cannot be negative" : null,
                "SelectedEnterprise.MonthlyExpenses" => SelectedEnterprise?.MonthlyExpenses < 0 ? "Expenses cannot be negative" : null,
                "SelectedEnterprise.CitizenCount" => SelectedEnterprise?.CitizenCount < 0 ? "Citizen count cannot be negative" : null,
                _ => null
            };
        }
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
