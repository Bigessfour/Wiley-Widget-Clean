using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Threading.Tasks;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Logging;
using WileyWidget.Services.Threading;

namespace WileyWidget.ViewModels;

/// <summary>
/// View model for managing utility customers
/// Provides data binding for customer CRUD operations and search functionality
/// </summary>
public partial class UtilityCustomerViewModel : AsyncViewModelBase
{
    private readonly IUtilityCustomerRepository _customerRepository;

    /// <summary>
    /// Collection of all customers for data binding
    /// </summary>
    public ThreadSafeObservableCollection<UtilityCustomer> Customers { get; }

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
    private UtilityCustomer? selectedCustomer;

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
    /// Constructor with dependency injection
    /// </summary>
    public UtilityCustomerViewModel(
        IUtilityCustomerRepository customerRepository,
        IDispatcherHelper dispatcherHelper,
        Microsoft.Extensions.Logging.ILogger logger)
        : base(dispatcherHelper, logger)
    {
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        Customers = new ThreadSafeObservableCollection<UtilityCustomer>();
    }

    /// <summary>
    /// Loads all customers from the database
    /// </summary>
    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var customers = await _customerRepository.GetAllAsync();
            await Customers.ReplaceAllAsync(customers);
            UpdateSummaryText();
        }, statusMessage: "Loading customers...");
    }

    /// <summary>
    /// Loads active customers only
    /// </summary>
    [RelayCommand]
    public async Task LoadActiveCustomersAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var customers = await _customerRepository.GetActiveCustomersAsync();
            await Customers.ReplaceAllAsync(customers);
            UpdateSummaryText();
        }, statusMessage: "Loading active customers...");
    }

    /// <summary>
    /// Loads customers outside city limits
    /// </summary>
    [RelayCommand]
    public async Task LoadCustomersOutsideCityLimitsAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var customers = await _customerRepository.GetCustomersOutsideCityLimitsAsync();
            await Customers.ReplaceAllAsync(customers);
            UpdateSummaryText();
        }, statusMessage: "Loading customers outside city limits...");
    }

    /// <summary>
    /// Searches customers based on the search term
    /// </summary>
    [RelayCommand]
    public async Task SearchCustomersAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var customers = await _customerRepository.SearchAsync(SearchTerm);
            await Customers.ReplaceAllAsync(customers);
            UpdateSummaryText();
        }, statusMessage: $"Searching for '{SearchTerm}'...");
    }

    /// <summary>
    /// Adds a new customer
    /// </summary>
    [RelayCommand]
    private async Task AddCustomerAsync()
    {
        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
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
            await Customers.AddAsync(addedCustomer);
            SelectedCustomer = addedCustomer;
            UpdateSummaryText();
        }, statusMessage: "Adding new customer...");
    }

    /// <summary>
    /// Saves changes to the selected customer
    /// </summary>
    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            // Validate account number uniqueness
            if (await _customerRepository.ExistsByAccountNumberAsync(SelectedCustomer.AccountNumber, SelectedCustomer.Id))
            {
                throw new InvalidOperationException("Account number already exists. Please choose a different account number.");
            }

            await _customerRepository.UpdateAsync(SelectedCustomer);
        }, statusMessage: "Saving customer...");
    }

    /// <summary>
    /// Deletes the selected customer
    /// </summary>
    [RelayCommand]
    private async Task DeleteCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        var customerToDelete = SelectedCustomer;

        await ExecuteAsyncOperation(async (cancellationToken) =>
        {
            var result = await _customerRepository.DeleteAsync(customerToDelete.Id);
            if (result)
            {
                await Customers.RemoveAsync(customerToDelete);
                SelectedCustomer = null;
                UpdateSummaryText();
            }
            else
            {
                throw new InvalidOperationException("Failed to delete customer - customer may not exist or may be referenced by other records.");
            }
        }, statusMessage: "Deleting customer...");
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
        Log.Information("Error cleared by user");
    }
}