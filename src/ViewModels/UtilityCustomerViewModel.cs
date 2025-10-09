using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing utility customers
/// Provides data binding for customer CRUD operations and search functionality
/// </summary>
public partial class UtilityCustomerViewModel : ObservableObject
{
    private readonly IUtilityCustomerRepository _customerRepository;

    /// <summary>
    /// Collection of all customers for data binding
    /// </summary>
    public ObservableCollection<UtilityCustomer> Customers { get; } = new();

    /// <summary>
    /// Collection of customer types for UI binding
    /// </summary>
    public IEnumerable<CustomerType> CustomerTypes { get; } = Enum.GetValues(typeof(CustomerType)).Cast<CustomerType>();

    /// <summary>
    /// Collection of service locations for UI binding
    /// </summary>
    public IEnumerable<ServiceLocation> ServiceLocations { get; } = Enum.GetValues(typeof(ServiceLocation)).Cast<ServiceLocation>();

    /// <summary>
    /// Collection of customer statuses for UI binding
    /// </summary>
    public IEnumerable<CustomerStatus> CustomerStatuses { get; } = Enum.GetValues(typeof(CustomerStatus)).Cast<CustomerStatus>();

    /// <summary>
    /// Currently selected customer in the UI
    /// </summary>
    [ObservableProperty]
    private UtilityCustomer selectedCustomer;

    /// <summary>
    /// Loading state for async operations
    /// </summary>
    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Search term for filtering customers
    /// </summary>
    [ObservableProperty]
    private string searchTerm = string.Empty;

    /// <summary>
    /// Summary text for display
    /// </summary>
    [ObservableProperty]
    private string summaryText = "No customer data available";

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
    /// Status message presented in the UI
    /// </summary>
    [ObservableProperty]
    private string statusMessage = "Ready";

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public UtilityCustomerViewModel(IUtilityCustomerRepository customerRepository)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        selectedCustomer = new UtilityCustomer(); // Initialize to avoid null warning
    }

    /// <summary>
    /// Loads all customers from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading customers...";

            var customers = await _customerRepository.GetAllAsync();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} customers.";
            Log.Information("Successfully loaded {Count} customers", customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load customers");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads active customers only
    /// </summary>
    [RelayCommand]
    public async Task LoadActiveCustomersAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading active customers...";

            var customers = await _customerRepository.GetActiveCustomersAsync();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} active customers.";
            Log.Information("Successfully loaded {Count} active customers", customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load active customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load active customers");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads customers outside city limits
    /// </summary>
    [RelayCommand]
    public async Task LoadCustomersOutsideCityLimitsAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Loading customers outside city limits...";

            var customers = await _customerRepository.GetCustomersOutsideCityLimitsAsync();

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Loaded {Customers.Count} customers outside city limits.";
            Log.Information("Successfully loaded {Count} customers outside city limits", customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load customers outside city limits: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to load customers outside city limits");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Searches customers based on the search term
    /// </summary>
    [RelayCommand]
    public async Task SearchCustomersAsync()
    {
        try
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Searching customers...";

            var customers = await _customerRepository.SearchAsync(SearchTerm);

            Customers.Clear();
            foreach (var customer in customers)
            {
                Customers.Add(customer);
            }

            UpdateSummaryText();
            StatusMessage = $"Found {Customers.Count} customers matching '{SearchTerm}'.";
            Log.Information("Successfully searched customers with term '{SearchTerm}', found {Count} results", SearchTerm, customers.Count());
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to search customers: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to search customers with term '{SearchTerm}'", SearchTerm);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Adds a new customer
    /// </summary>
    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        try
        {
            StatusMessage = "Adding new customer...";
            var newCustomer = new UtilityCustomer
            {
                AccountNumber = await GenerateNextAccountNumberAsync(),
                FirstName = "New",
                LastName = "Customer",
                ServiceAddress = "Enter service address",
                ServiceCity = "City",
                ServiceState = "ST",
                ServiceZipCode = "12345",
                CustomerType = CustomerType.Residential,
                ServiceLocation = ServiceLocation.InsideCityLimits,
                Status = CustomerStatus.Active,
                AccountOpenDate = DateTime.Now,
                Notes = "New customer - update details"
            };

            var addedCustomer = await _customerRepository.AddAsync(newCustomer);
            Customers.Add(addedCustomer);
            SelectedCustomer = addedCustomer;
            UpdateSummaryText();
            StatusMessage = $"Customer {addedCustomer.AccountNumber} added.";
            Log.Information("Successfully added new customer with account number {AccountNumber}", addedCustomer.AccountNumber);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to add customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to add new customer");
        }
    }

    /// <summary>
    /// Saves changes to the selected customer
    /// </summary>
    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            StatusMessage = $"Saving customer {SelectedCustomer.AccountNumber}...";
            // Validate account number uniqueness
            if (await _customerRepository.ExistsByAccountNumberAsync(SelectedCustomer.AccountNumber, SelectedCustomer.Id))
            {
                ErrorMessage = "Account number already exists. Please choose a different account number.";
                HasError = true;
                StatusMessage = ErrorMessage;
                Log.Warning("Attempted to save customer with duplicate account number {AccountNumber}", SelectedCustomer.AccountNumber);
                return;
            }

            await _customerRepository.UpdateAsync(SelectedCustomer);
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = $"Customer {SelectedCustomer.AccountNumber} saved.";
            Log.Information("Successfully saved customer {AccountNumber}", SelectedCustomer.AccountNumber);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to save customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            Log.Error(ex, "Failed to save customer {AccountNumber}", SelectedCustomer.AccountNumber);
        }
    }

    /// <summary>
    /// Deletes the selected customer
    /// </summary>
    [RelayCommand]
    private async Task DeleteCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            StatusMessage = $"Deleting customer {SelectedCustomer.AccountNumber}...";
            var accountNumber = SelectedCustomer.AccountNumber;
            var result = await _customerRepository.DeleteAsync(SelectedCustomer.Id);
            if (result)
            {
                Customers.Remove(SelectedCustomer);
                SelectedCustomer = null;
                UpdateSummaryText();
                HasError = false;
                ErrorMessage = string.Empty;
                StatusMessage = $"Customer {accountNumber} deleted.";
                Log.Information("Successfully deleted customer {AccountNumber}", accountNumber);
            }
            else
            {
                ErrorMessage = "Failed to delete customer - customer may not exist or may be referenced by other records.";
                HasError = true;
                StatusMessage = ErrorMessage;
                Log.Warning("Failed to delete customer {AccountNumber} - repository returned false", accountNumber);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to delete customer: {ex.Message}";
            HasError = true;
            StatusMessage = ErrorMessage;
            var accountNumber = SelectedCustomer?.AccountNumber ?? "unknown";
            Log.Error(ex, "Failed to delete customer {AccountNumber}", accountNumber);
        }
    }

    /// <summary>
    /// Generates the next available account number
    /// </summary>
    private async Task<string> GenerateNextAccountNumberAsync()
    {
        var count = await _customerRepository.GetCountAsync();
        return $"C{(count + 1):D6}"; // C000001, C000002, etc.
    }

    /// <summary>
    /// Updates the summary text based on current data
    /// </summary>
    private void UpdateSummaryText()
    {
        var totalCustomers = Customers.Count;
        var activeCustomers = Customers.Count(c => c.IsActive);
        var outsideCityLimits = Customers.Count(c => c.ServiceLocation == ServiceLocation.OutsideCityLimits);
        var totalBalance = Customers.Sum(c => c.CurrentBalance);

        SummaryText = $"{totalCustomers} customers ({activeCustomers} active), " +
                     $"{outsideCityLimits} outside city limits, " +
                     $"Total balance: {totalBalance:C}";
    }

    /// <summary>
    /// Clears the search and reloads all customers
    /// </summary>
    [RelayCommand]
    public async Task ClearSearchAsync()
    {
        SearchTerm = string.Empty;
        StatusMessage = "Clearing search results...";
        await LoadCustomersAsync();
    }

    /// <summary>
    /// Clears any error state
    /// </summary>
    [RelayCommand]
    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        StatusMessage = "Ready";
        Log.Information("Error cleared by user");
    }
}