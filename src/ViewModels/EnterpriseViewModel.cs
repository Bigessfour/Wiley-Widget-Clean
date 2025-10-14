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
    /// Error message for repository operations
    /// </summary>
    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
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

    // Missing properties for view bindings
    private int _currentPageIndex;
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (_currentPageIndex != value)
            {
                _currentPageIndex = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<Enterprise> Enterprises => EnterpriseList;

    private int _pageCount;
    public int PageCount
    {
        get => _pageCount;
        set
        {
            if (_pageCount != value)
            {
                _pageCount = value;
                OnPropertyChanged();
            }
        }
    }

    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (_pageSize != value)
            {
                _pageSize = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<Enterprise> PagedHierarchicalEnterprises { get; } = new();

    private decimal _progressPercentage;
    public decimal ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (_progressPercentage != value)
            {
                _progressPercentage = value;
                OnPropertyChanged();
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
            }
        }
    }

    private object _selectedNode;
    public object SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (_selectedNode != value)
            {
                _selectedNode = value;
                OnPropertyChanged();
            }
        }
    }

    private string _selectedStatusFilter = "All";
    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter != value)
            {
                _selectedStatusFilter = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<string> StatusOptions { get; } = new() { "All", "Active", "Inactive", "Pending" };

    private Enterprise _enterprise;
    public Enterprise Enterprise
    {
        get => _enterprise;
        set
        {
            if (_enterprise != value)
            {
                _enterprise = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _value;
    public decimal Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Selection changed command - updates budget summary and enables drill-down navigation
    /// </summary>
    [RelayCommand]
    private void SelectionChanged()
    {
        if (SelectedEnterprise != null)
        {
            // Update budget summary when selection changes
            BudgetSummaryText = $"Selected: {SelectedEnterprise.Name}\n" +
                               $"Monthly Revenue: {SelectedEnterprise.MonthlyRevenue:C2}\n" +
                               $"Monthly Expenses: {SelectedEnterprise.MonthlyExpenses:C2}\n" +
                               $"Monthly Balance: {SelectedEnterprise.MonthlyBalance:C2}\n" +
                               $"Citizens Served: {SelectedEnterprise.CitizenCount:N0}";
            
            StatusMessage = $"Selected: {SelectedEnterprise.Name}";
            ErrorMessage = string.Empty;
            
            Log.Debug("Enterprise selected: {EnterpriseName} (ID: {EnterpriseId})", 
                     SelectedEnterprise.Name, SelectedEnterprise.Id);
        }
        else
        {
            BudgetSummaryText = GetBudgetSummary();
            StatusMessage = "Ready";
        }
    }

    /// <summary>
    /// Navigate to enterprise details view
    /// </summary>
    [RelayCommand]
    private void NavigateToDetails(int enterpriseId)
    {
        try
        {
            // Find enterprise by ID
            var enterprise = EnterpriseList.FirstOrDefault(e => e.Id == enterpriseId);
            if (enterprise != null)
            {
                SelectedEnterprise = enterprise;
                StatusMessage = $"Viewing details for: {enterprise.Name}";
                Log.Information("Navigated to enterprise details: {EnterpriseId}", enterpriseId);
            }
            else
            {
                ErrorMessage = $"Enterprise with ID {enterpriseId} not found";
                Log.Warning("Navigation failed: Enterprise ID {EnterpriseId} not found", enterpriseId);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Navigation failed: {ex.Message}";
            Log.Error(ex, "Error navigating to enterprise details for ID: {EnterpriseId}", enterpriseId);
        }
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
        ErrorMessage = string.Empty;
        
        try
        {
            IsLoading = true;
            StatusMessage = "Creating new enterprise...";

            var newEnterprise = new Enterprise
            {
                Name = "New Enterprise",
                CurrentRate = 5.00m,
                MonthlyExpenses = 0.00m,
                CitizenCount = 1,
                Status = EnterpriseStatus.Active,
                TotalBudget = 0.00m,
                Type = "Utility",
                Notes = "New enterprise - update details"
            };

            var addedEnterprise = await Task.Run(() => _unitOfWork.Enterprises.AddAsync(newEnterprise));
            EnterpriseList.Add(addedEnterprise);
            SelectedEnterprise = addedEnterprise;
            
            StatusMessage = $"Enterprise '{addedEnterprise.Name}' created successfully";
            Log.Information("Created new enterprise with ID: {EnterpriseId}", addedEnterprise.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to create enterprise: {ex.Message}";
            StatusMessage = "Error creating enterprise";
            Log.Error(ex, "Error adding enterprise");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Saves changes to the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task SaveEnterpriseAsync()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected";
            return;
        }

        // Validate before saving
        var validationError = this[nameof(SelectedEnterprise.Name)] ?? 
                            this[nameof(SelectedEnterprise.CurrentRate)] ?? 
                            this[nameof(SelectedEnterprise.CitizenCount)];
        
        if (!string.IsNullOrEmpty(validationError))
        {
            ErrorMessage = $"Validation failed: {validationError}";
            StatusMessage = "Cannot save: validation errors";
            return;
        }

        ErrorMessage = string.Empty;

        try
        {
            IsLoading = true;
            StatusMessage = $"Saving '{SelectedEnterprise.Name}'...";

            await Task.Run(() => _unitOfWork.Enterprises.UpdateAsync(SelectedEnterprise));
            
            StatusMessage = $"Enterprise '{SelectedEnterprise.Name}' saved successfully";
            Log.Information("Updated enterprise with ID: {EnterpriseId}", SelectedEnterprise.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save enterprise: {ex.Message}";
            StatusMessage = "Error saving enterprise";
            Log.Error(ex, "Error saving enterprise {EnterpriseId}", SelectedEnterprise?.Id);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Deletes the selected enterprise
    /// </summary>
    [RelayCommand]
    private async Task DeleteEnterpriseAsync()
    {
        if (SelectedEnterprise == null)
        {
            StatusMessage = "No enterprise selected";
            return;
        }

        ErrorMessage = string.Empty;
        var enterpriseName = SelectedEnterprise.Name;
        var enterpriseId = SelectedEnterprise.Id;

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting '{enterpriseName}'...";

            var success = await Task.Run(() => _unitOfWork.Enterprises.DeleteAsync(enterpriseId));
            
            if (success)
            {
                EnterpriseList.Remove(SelectedEnterprise);
                SelectedEnterprise = EnterpriseList.FirstOrDefault();
                StatusMessage = $"Enterprise '{enterpriseName}' deleted successfully";
                Log.Information("Deleted enterprise with ID: {EnterpriseId}", enterpriseId);
            }
            else
            {
                ErrorMessage = $"Failed to delete enterprise '{enterpriseName}'";
                StatusMessage = "Delete operation failed";
                Log.Warning("Delete operation returned false for enterprise ID: {EnterpriseId}", enterpriseId);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete enterprise: {ex.Message}";
            StatusMessage = "Error deleting enterprise";
            Log.Error(ex, "Error deleting enterprise {EnterpriseId}", enterpriseId);
        }
        finally
        {
            IsLoading = false;
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
    /// Bulk update enterprises
    /// </summary>
    [RelayCommand]
    private async Task BulkUpdateAsync()
    {
        // TODO: Implement bulk update functionality
        StatusMessage = "Bulk update feature coming soon...";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clear filters
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = "All";
        StatusMessage = "Filters cleared";
    }

    /// <summary>
    /// Clear grouping
    /// </summary>
    [RelayCommand]
    private void ClearGrouping()
    {
        // TODO: Implement clear grouping
        StatusMessage = "Grouping cleared";
    }

    /// <summary>
    /// Copy to clipboard
    /// </summary>
    [RelayCommand]
    private void CopyToClipboard()
    {
        // TODO: Implement copy to clipboard
        StatusMessage = "Copy to clipboard feature coming soon...";
    }

    /// <summary>
    /// Edit enterprise
    /// </summary>
    [RelayCommand]
    private void EditEnterprise()
    {
        if (SelectedEnterprise != null)
        {
            StatusMessage = $"Editing {SelectedEnterprise.Name}";
        }
    }

    /// <summary>
    /// Generate enterprise report
    /// </summary>
    [RelayCommand]
    private async Task GenerateEnterpriseReport()
    {
        // TODO: Implement report generation
        StatusMessage = "Report generation feature coming soon...";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Group by status
    /// </summary>
    [RelayCommand]
    private void GroupByStatus()
    {
        // TODO: Implement grouping by status
        StatusMessage = "Grouped by status";
    }

    /// <summary>
    /// Group by type
    /// </summary>
    [RelayCommand]
    private void GroupByType()
    {
        // TODO: Implement grouping by type
        StatusMessage = "Grouped by type";
    }

    /// <summary>
    /// Import data
    /// </summary>
    [RelayCommand]
    private async Task ImportData()
    {
        // TODO: Implement data import
        StatusMessage = "Data import feature coming soon...";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Load enterprises incrementally
    /// </summary>
    [RelayCommand]
    private async Task LoadEnterprisesIncrementalAsync()
    {
        // TODO: Implement incremental loading
        await LoadEnterprisesAsync();
    }

    /// <summary>
    /// Rate analysis
    /// </summary>
    [RelayCommand]
    private void RateAnalysis()
    {
        // TODO: Implement rate analysis
        StatusMessage = "Rate analysis feature coming soon...";
    }

    /// <summary>
    /// View audit history
    /// </summary>
    [RelayCommand]
    private async Task ViewAuditHistoryAsync()
    {
        // TODO: Implement audit history view
        StatusMessage = "Audit history feature coming soon...";
        await Task.CompletedTask;
    }

    /// <summary>
    /// Show advanced filter
    /// </summary>
    [RelayCommand]
    private void ShowAdvancedFilter()
    {
        // TODO: Implement advanced filter
        StatusMessage = "Advanced filter feature coming soon...";
    }

    /// <summary>
    /// Show tree map
    /// </summary>
    [RelayCommand]
    private void ShowTreeMap()
    {
        // TODO: Implement tree map view
        StatusMessage = "Tree map view feature coming soon...";
    }

    /// <summary>
    /// Show tree view
    /// </summary>
    [RelayCommand]
    private void ShowTreeView()
    {
        // TODO: Implement tree view
        StatusMessage = "Tree view feature coming soon...";
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
    public string Error
    {
        get
        {
            if (SelectedEnterprise == null) return null;

            var errors = new System.Collections.Generic.List<string>();

            if (string.IsNullOrWhiteSpace(SelectedEnterprise.Name))
                errors.Add("Name is required");

            if (SelectedEnterprise.CurrentRate <= 0)
                errors.Add("Rate must be greater than 0");

            if (SelectedEnterprise.MonthlyExpenses < 0)
                errors.Add("Expenses cannot be negative");

            if (SelectedEnterprise.CitizenCount < 0)
                errors.Add("Citizen count cannot be negative");

            if (SelectedEnterprise.TotalBudget < 0)
                errors.Add("Budget cannot be negative");

            return errors.Count > 0 ? string.Join("; ", errors) : null;
        }
    }

    /// <summary>
    /// IDataErrorInfo implementation - property-level validation
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            if (SelectedEnterprise == null) return null;

            return columnName switch
            {
                nameof(SelectedEnterprise.Name) => 
                    string.IsNullOrWhiteSpace(SelectedEnterprise.Name) 
                        ? "Name is required" 
                        : SelectedEnterprise.Name.Length > 100 
                            ? "Name cannot exceed 100 characters" 
                            : null,

                nameof(SelectedEnterprise.CurrentRate) => 
                    SelectedEnterprise.CurrentRate <= 0 
                        ? "Rate must be greater than 0" 
                        : SelectedEnterprise.CurrentRate > 9999.99m 
                            ? "Rate cannot exceed $9,999.99" 
                            : null,

                nameof(SelectedEnterprise.MonthlyExpenses) => 
                    SelectedEnterprise.MonthlyExpenses < 0 
                        ? "Expenses cannot be negative" 
                        : null,

                nameof(SelectedEnterprise.CitizenCount) => 
                    SelectedEnterprise.CitizenCount < 0 
                        ? "Citizen count cannot be negative" 
                        : SelectedEnterprise.CitizenCount < 1 
                            ? "At least one citizen must be served" 
                            : null,

                nameof(SelectedEnterprise.TotalBudget) => 
                    SelectedEnterprise.TotalBudget < 0 
                        ? "Budget cannot be negative" 
                        : null,

                "SelectedEnterprise.Name" => 
                    string.IsNullOrWhiteSpace(SelectedEnterprise?.Name) 
                        ? "Name is required" 
                        : null,

                "SelectedEnterprise.CurrentRate" => 
                    SelectedEnterprise?.CurrentRate < 0 
                        ? "Rate cannot be negative" 
                        : null,

                "SelectedEnterprise.MonthlyExpenses" => 
                    SelectedEnterprise?.MonthlyExpenses < 0 
                        ? "Expenses cannot be negative" 
                        : null,

                "SelectedEnterprise.CitizenCount" => 
                    SelectedEnterprise?.CitizenCount < 0 
                        ? "Citizen count cannot be negative" 
                        : null,

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
