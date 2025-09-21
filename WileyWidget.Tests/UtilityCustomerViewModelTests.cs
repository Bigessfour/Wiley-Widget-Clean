using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.ViewModels;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the UtilityCustomerViewModel
/// Tests CRUD operations, data binding, search functionality, and UI interactions
/// </summary>
public class UtilityCustomerViewModelTests
{
    private readonly Mock<IUtilityCustomerRepository> _mockRepository;
    private readonly UtilityCustomerViewModel _viewModel;

    public UtilityCustomerViewModelTests()
    {
        _mockRepository = new Mock<IUtilityCustomerRepository>();
        _viewModel = new UtilityCustomerViewModel(_mockRepository.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UtilityCustomerViewModel(null));
    }

    [Fact]
    public void Constructor_InitializesCollectionsCorrectly()
    {
        // Assert
        Assert.NotNull(_viewModel.Customers);
        Assert.IsType<ObservableCollection<UtilityCustomer>>(_viewModel.Customers);
        Assert.NotNull(_viewModel.CustomerTypes);
        Assert.NotNull(_viewModel.ServiceLocations);
        Assert.NotNull(_viewModel.CustomerStatuses);
        Assert.Equal(string.Empty, _viewModel.SearchTerm);
        Assert.Equal("No customer data available", _viewModel.SummaryText);
        Assert.Null(_viewModel.SelectedCustomer);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadCustomersAsync_LoadsAndDisplaysCustomers()
    {
        // Arrange
        var customers = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m),
            CreateTestCustomer("C000002", "Jane", "Smith", CustomerStatus.Inactive, 50.00m)
        };

        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(customers);

        // Act
        await _viewModel.LoadCustomersAsync();

        // Assert
        Assert.Equal(2, _viewModel.Customers.Count);
        Assert.Contains(_viewModel.Customers, c => c.AccountNumber == "C000001");
        Assert.Contains(_viewModel.Customers, c => c.AccountNumber == "C000002");
        Assert.Contains("2 customers (1 active)", _viewModel.SummaryText);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadCustomersAsync_HandlesExceptionGracefully()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllAsync()).ThrowsAsync(new Exception("Database error"));

        // Act
        await _viewModel.LoadCustomersAsync();

        // Assert
        Assert.Empty(_viewModel.Customers);
        Assert.Equal("No customer data available", _viewModel.SummaryText);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadActiveCustomersAsync_LoadsOnlyActiveCustomers()
    {
        // Arrange
        var activeCustomers = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m),
            CreateTestCustomer("C000002", "Jane", "Smith", CustomerStatus.Active, 200.00m)
        };

        _mockRepository.Setup(r => r.GetActiveCustomersAsync()).ReturnsAsync(activeCustomers);

        // Act
        await _viewModel.LoadActiveCustomersAsync();

        // Assert
        Assert.Equal(2, _viewModel.Customers.Count);
        Assert.All(_viewModel.Customers, c => Assert.True(c.IsActive));
        Assert.Contains("2 customers (2 active)", _viewModel.SummaryText);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task LoadCustomersOutsideCityLimitsAsync_LoadsOnlyOutsideCustomers()
    {
        // Arrange
        var outsideCustomers = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m, ServiceLocation.OutsideCityLimits),
            CreateTestCustomer("C000002", "Jane", "Smith", CustomerStatus.Active, 200.00m, ServiceLocation.OutsideCityLimits)
        };

        _mockRepository.Setup(r => r.GetCustomersOutsideCityLimitsAsync()).ReturnsAsync(outsideCustomers);

        // Act
        await _viewModel.LoadCustomersOutsideCityLimitsAsync();

        // Assert
        Assert.Equal(2, _viewModel.Customers.Count);
        Assert.All(_viewModel.Customers, c => Assert.Equal(ServiceLocation.OutsideCityLimits, c.ServiceLocation));
        Assert.Contains("2 outside city limits", _viewModel.SummaryText);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task SearchCustomersAsync_SearchesAndDisplaysResults()
    {
        // Arrange
        var searchResults = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m)
        };

        _mockRepository.Setup(r => r.SearchAsync("John")).ReturnsAsync(searchResults);

        // Act
        _viewModel.SearchTerm = "John";
        await _viewModel.SearchCustomersAsync();

        // Assert
        Assert.Single(_viewModel.Customers);
        Assert.Equal("C000001", _viewModel.Customers[0].AccountNumber);
        Assert.False(_viewModel.IsLoading);
    }

    [Fact]
    public async Task ClearSearchAsync_ClearsSearchAndReloadsAllCustomers()
    {
        // Arrange
        var customers = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m)
        };

        _viewModel.SearchTerm = "test search";
        _mockRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(customers);

        // Act
        await _viewModel.ClearSearchAsync();

        // Assert
        Assert.Equal(string.Empty, _viewModel.SearchTerm);
        Assert.Single(_viewModel.Customers);
        _mockRepository.Verify(r => r.GetAllAsync(), Times.Once);
    }

    [Fact]
    public void UpdateSummaryText_CalculatesCorrectSummary()
    {
        // Arrange
        var customers = new List<UtilityCustomer>
        {
            CreateTestCustomer("C000001", "John", "Doe", CustomerStatus.Active, 100.00m, ServiceLocation.InsideCityLimits),
            CreateTestCustomer("C000002", "Jane", "Smith", CustomerStatus.Inactive, 50.00m, ServiceLocation.OutsideCityLimits),
            CreateTestCustomer("C000003", "Bob", "Johnson", CustomerStatus.Active, 75.00m, ServiceLocation.OutsideCityLimits)
        };

        foreach (var customer in customers)
        {
            _viewModel.Customers.Add(customer);
        }

        // Act - This is called internally by the view model methods
        // We'll simulate it by calling the private method via reflection for testing
        var method = typeof(UtilityCustomerViewModel).GetMethod("UpdateSummaryText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(_viewModel, null);

        // Assert
        Assert.Contains("3 customers (2 active)", _viewModel.SummaryText);
        Assert.Contains("2 outside city limits", _viewModel.SummaryText);
        Assert.Contains("Total balance: $225.00", _viewModel.SummaryText);
    }

    [Fact]
    public async Task GenerateNextAccountNumberAsync_GeneratesCorrectFormat()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetCountAsync()).ReturnsAsync(5);

        // Act
        var method = typeof(UtilityCustomerViewModel).GetMethod("GenerateNextAccountNumberAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var task = (Task<string>)method?.Invoke(_viewModel, null);
        var result = await task;

        // Assert
        Assert.Equal("C000006", result);
    }

    [Fact]
    public void CustomerTypes_Property_ReturnsAllEnumValues()
    {
        // Act
        var customerTypes = _viewModel.CustomerTypes.ToList();

        // Assert
        Assert.Contains(CustomerType.Residential, customerTypes);
        Assert.Contains(CustomerType.Commercial, customerTypes);
        Assert.Contains(CustomerType.Industrial, customerTypes);
        Assert.Contains(CustomerType.Institutional, customerTypes);
        Assert.Contains(CustomerType.Government, customerTypes);
        Assert.Contains(CustomerType.MultiFamily, customerTypes);
    }

    [Fact]
    public void ServiceLocations_Property_ReturnsAllEnumValues()
    {
        // Act
        var serviceLocations = _viewModel.ServiceLocations.ToList();

        // Assert
        Assert.Contains(ServiceLocation.InsideCityLimits, serviceLocations);
        Assert.Contains(ServiceLocation.OutsideCityLimits, serviceLocations);
    }

    [Fact]
    public void CustomerStatuses_Property_ReturnsAllEnumValues()
    {
        // Act
        var customerStatuses = _viewModel.CustomerStatuses.ToList();

        // Assert
        Assert.Contains(CustomerStatus.Active, customerStatuses);
        Assert.Contains(CustomerStatus.Inactive, customerStatuses);
        Assert.Contains(CustomerStatus.Suspended, customerStatuses);
        Assert.Contains(CustomerStatus.Closed, customerStatuses);
    }

    private static UtilityCustomer CreateTestCustomer(
        string accountNumber,
        string firstName,
        string lastName,
        CustomerStatus status,
        decimal balance,
        ServiceLocation serviceLocation = ServiceLocation.InsideCityLimits)
    {
        return new UtilityCustomer
        {
            Id = int.Parse(accountNumber.Replace("C", "")),
            AccountNumber = accountNumber,
            FirstName = firstName,
            LastName = lastName,
            ServiceAddress = "123 Test St",
            ServiceCity = "Test City",
            ServiceState = "TS",
            ServiceZipCode = "12345",
            CustomerType = CustomerType.Residential,
            ServiceLocation = serviceLocation,
            Status = status,
            AccountOpenDate = DateTime.Now,
            CurrentBalance = balance
        };
    }
}