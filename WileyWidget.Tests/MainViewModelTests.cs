using System.Collections.ObjectModel;
using Xunit;
using Moq;
using WileyWidget.ViewModels;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.Business.Interfaces;
using Intuit.Ipp.Data;
using System.Threading.Tasks;
using Serilog;
using EnterpriseRepo = WileyWidget.Business.Interfaces.IEnterpriseRepository;
using MunicipalRepo = WileyWidget.Business.Interfaces.IMunicipalAccountRepository;
using Enterprise = WileyWidget.Models.Enterprise;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for MainViewModel functionality
/// Tests enterprise management, QuickBooks integration, navigation, and error handling
/// </summary>
public class MainViewModelTests : TestApplication
{
    private readonly Mock<EnterpriseRepo> _mockEnterpriseRepository;
    private readonly Mock<MunicipalRepo> _mockMunicipalAccountRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IQuickBooksService> _mockQuickBooksService;
    private readonly Mock<IAIService> _mockAIService;
    private readonly MainViewModel _viewModel;

    public MainViewModelTests()
    {
        _mockEnterpriseRepository = new Mock<EnterpriseRepo>();
        _mockMunicipalAccountRepository = new Mock<MunicipalRepo>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUnitOfWork.Setup(u => u.Enterprises).Returns(_mockEnterpriseRepository.Object);
        _mockUnitOfWork.Setup(u => u.MunicipalAccounts).Returns(_mockMunicipalAccountRepository.Object);
        _mockQuickBooksService = new Mock<IQuickBooksService>();
        _mockAIService = new Mock<IAIService>();

        // Setup default mock behaviors
        _mockEnterpriseRepository.Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(new List<Enterprise>());
        _mockQuickBooksService.Setup(qb => qb.GetCustomersAsync())
            .ReturnsAsync(new List<Customer>());
        _mockQuickBooksService.Setup(qb => qb.GetInvoicesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<Intuit.Ipp.Data.Invoice>());

        _viewModel = new MainViewModel(
            _mockUnitOfWork.Object,
            _mockQuickBooksService.Object,
            _mockAIService.Object,
            false);
    }

    /// <summary>
    /// Helper method to invoke private methods on ViewModels for testing
    /// </summary>
    private static async System.Threading.Tasks.Task InvokePrivateAsync<T>(T instance, string methodName, params object[] parameters)
    {
        var method = typeof(T).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Private method '{methodName}' not found on type {typeof(T).Name}");

        var result = method.Invoke(instance, parameters);
        if (result is System.Threading.Tasks.Task task)
            await task;
    }

    private static void InvokePrivate<T>(T instance, string methodName, params object[] parameters)
    {
        var method = typeof(T).GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method == null)
            throw new InvalidOperationException($"Private method '{methodName}' not found on type {typeof(T).Name}");

        method.Invoke(instance, parameters);
    }

    [Fact]
    public void Constructor_WithNullEnterpriseRepository_ThrowsArgumentNullException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.Enterprises).Returns((IEnterpriseRepository)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(_mockUnitOfWork.Object, _mockQuickBooksService.Object, _mockAIService.Object, false));
    }

    [Fact]
    public void Constructor_WithNullMunicipalAccountRepository_ThrowsArgumentNullException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.MunicipalAccounts).Returns((IMunicipalAccountRepository)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MainViewModel(_mockUnitOfWork.Object, _mockQuickBooksService.Object, _mockAIService.Object, false));
    }
    [Fact]
    public void Constructor_WithNullQuickBooksService_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        using var viewModel = new MainViewModel(_mockUnitOfWork.Object, null, _mockAIService.Object, false);
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesProperties()
    {
        // Assert
        Assert.NotNull(_viewModel.Enterprises);
        Assert.IsType<ObservableCollection<Enterprise>>(_viewModel.Enterprises);
        Assert.NotNull(_viewModel.QuickBooksCustomers);
        Assert.IsType<ObservableCollection<Customer>>(_viewModel.QuickBooksCustomers);
        Assert.NotNull(_viewModel.QuickBooksInvoices);
        Assert.IsType<ObservableCollection<Intuit.Ipp.Data.Invoice>>(_viewModel.QuickBooksInvoices);
        Assert.NotNull(_viewModel.MunicipalAccountViewModel);
        Assert.Null(_viewModel.SelectedEnterprise);
        Assert.False(_viewModel.QuickBooksBusy);
        Assert.Null(_viewModel.QuickBooksStatusMessage);
        Assert.Null(_viewModel.QuickBooksErrorMessage);
        Assert.False(_viewModel.QuickBooksHasError);
    }

    [Fact]
    public void SelectNext_WithEmptyEnterprises_DoesNothing()
    {
        // Arrange
        _viewModel.Enterprises.Clear();

        // Act
        _viewModel.SelectNextCommand.Execute(null);

        // Assert
        Assert.Null(_viewModel.SelectedEnterprise);
    }

    [Fact]
    public void SelectNext_WithNoSelection_SelectsFirstEnterprise()
    {
        // Arrange
        _viewModel.Enterprises.Clear();
        var enterprise1 = new Enterprise { Id = 1, Name = "Test Enterprise 1" };
        var enterprise2 = new Enterprise { Id = 2, Name = "Test Enterprise 2" };
        _viewModel.Enterprises.Add(enterprise1);
        _viewModel.Enterprises.Add(enterprise2);
        _viewModel.SelectedEnterprise = null;

        // Act
        _viewModel.SelectNextCommand.Execute(null);

        // Assert
        Assert.Equal(enterprise1, _viewModel.SelectedEnterprise);
    }

    [Fact]
    public void SelectNext_WithLastSelected_SelectsFirstEnterprise()
    {
        // Arrange
        _viewModel.Enterprises.Clear();
        var enterprise1 = new Enterprise { Id = 1, Name = "Test Enterprise 1" };
        var enterprise2 = new Enterprise { Id = 2, Name = "Test Enterprise 2" };
        _viewModel.Enterprises.Add(enterprise1);
        _viewModel.Enterprises.Add(enterprise2);
        _viewModel.SelectedEnterprise = enterprise2;

        // Act
        _viewModel.SelectNextCommand.Execute(null);

        // Assert
        Assert.Equal(enterprise1, _viewModel.SelectedEnterprise);
    }

    [Fact]
    public void AddEnterprise_AddsNewEnterpriseWithCorrectDefaults()
    {
        // Arrange
        var initialCount = _viewModel.Enterprises.Count;

        // Act
        _viewModel.AddTestEnterpriseCommand.Execute(null);

        // Assert
        Assert.Equal(initialCount + 1, _viewModel.Enterprises.Count);
        var addedEnterprise = _viewModel.Enterprises.Last();
        Assert.Equal($"New Enterprise {addedEnterprise.Id}", addedEnterprise.Name);
        Assert.Equal("Utility", addedEnterprise.Type);
        Assert.Equal(25.00M, addedEnterprise.CurrentRate);
        Assert.Equal(5000.00M, addedEnterprise.MonthlyExpenses);
        Assert.Equal(1000, addedEnterprise.CitizenCount);
        Assert.Equal(60000.00M, addedEnterprise.TotalBudget);
        Assert.Equal("New municipal enterprise", addedEnterprise.Notes);
        Assert.Equal(addedEnterprise, _viewModel.SelectedEnterprise);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_WithoutServiceConfigured_SetsError()
    {
        // Arrange
        using var viewModel = new MainViewModel(
            _mockUnitOfWork.Object,
            null!, // No QuickBooks service
            _mockAIService.Object,
            autoInitialize: false);

        // Act
        await InvokePrivateAsync(viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        Assert.True(viewModel.QuickBooksHasError);
        Assert.Contains("not configured", viewModel.QuickBooksErrorMessage);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_WhenBusy_DoesNothing()
    {
        // Arrange
    _viewModel.QuickBooksBusy = true;

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        _mockQuickBooksService.Verify(qb => qb.GetCustomersAsync(), Times.Never);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_SuccessfulLoad_UpdatesCollections()
    {
        // Arrange
        var customers = new List<Customer>
        {
            new Customer { Id = "1" }, // Note: Using Id instead of Name as Customer.Name may not exist
            new Customer { Id = "2" }
        };
        _mockQuickBooksService.Setup(qb => qb.GetCustomersAsync())
            .ReturnsAsync(customers);

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        Assert.Equal(2, _viewModel.QuickBooksCustomers.Count);
        Assert.Equal("Loaded 2 customers successfully", _viewModel.QuickBooksStatusMessage);
        Assert.False(_viewModel.QuickBooksHasError);
        Assert.False(_viewModel.QuickBooksBusy);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_AuthorizationError_SetsError()
    {
        // Arrange
        _mockQuickBooksService.Setup(qb => qb.GetCustomersAsync())
            .ThrowsAsync(new InvalidOperationException("Invalid token"));

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        Assert.True(_viewModel.QuickBooksHasError);
        Assert.Contains("authorization failed", _viewModel.QuickBooksErrorMessage);
        Assert.Equal("Authorization error", _viewModel.QuickBooksStatusMessage);
        Assert.False(_viewModel.QuickBooksBusy);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_NetworkError_SetsError()
    {
        // Arrange
        _mockQuickBooksService.Setup(qb => qb.GetCustomersAsync())
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Network error"));

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        Assert.True(_viewModel.QuickBooksHasError);
        Assert.Contains("Network error", _viewModel.QuickBooksErrorMessage);
        Assert.Equal("Network error", _viewModel.QuickBooksStatusMessage);
        Assert.False(_viewModel.QuickBooksBusy);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksCustomersAsync_GeneralError_SetsError()
    {
        // Arrange
        _mockQuickBooksService.Setup(qb => qb.GetCustomersAsync())
            .ThrowsAsync(new Exception("General error"));

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksCustomersAsync");

        // Assert
        Assert.True(_viewModel.QuickBooksHasError);
        Assert.Contains("General error", _viewModel.QuickBooksErrorMessage);
        Assert.Equal("Load failed", _viewModel.QuickBooksStatusMessage);
        Assert.False(_viewModel.QuickBooksBusy);
    }

    [Fact]
    public async System.Threading.Tasks.Task LoadQuickBooksInvoicesAsync_SuccessfulLoad_UpdatesCollections()
    {
        // Arrange
        var invoices = new List<Intuit.Ipp.Data.Invoice>
        {
            new Intuit.Ipp.Data.Invoice { Id = "1", DocNumber = "INV-001" },
            new Intuit.Ipp.Data.Invoice { Id = "2", DocNumber = "INV-002" }
        };
        _mockQuickBooksService.Setup(qb => qb.GetInvoicesAsync(It.IsAny<string>()))
            .ReturnsAsync(invoices);

        // Act
        await InvokePrivateAsync(_viewModel, "LoadQuickBooksInvoicesAsync");

        // Assert
        Assert.Equal(2, _viewModel.QuickBooksInvoices.Count);
        Assert.Equal("Loaded 2 invoices successfully", _viewModel.QuickBooksStatusMessage);
        Assert.False(_viewModel.QuickBooksHasError);
        Assert.False(_viewModel.QuickBooksBusy);
    }

    [Fact]
    public void ClearQuickBooksError_ClearsErrorState()
    {
        // Arrange
        _viewModel.QuickBooksErrorMessage = "Test error";
        _viewModel.QuickBooksHasError = true;

        // Act
        InvokePrivate(_viewModel, "ClearQuickBooksError");

        // Assert
        Assert.Null(_viewModel.QuickBooksErrorMessage);
        Assert.False(_viewModel.QuickBooksHasError);
        Assert.Equal("Error cleared", _viewModel.QuickBooksStatusMessage);
    }

    [Fact]
    public void Refresh_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "Refresh");
    }

    [Fact]
    public void OpenSettings_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenSettings");
    }

    [Fact]
    public void OpenHelp_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenHelp");
    }

    [Fact]
    public void OpenEnterprise_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenEnterprise");
    }

    [Fact]
    public void OpenBudget_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenBudget");
    }

    [Fact]
    public void OpenDashboard_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenDashboard");
    }

    [StaFact]
    public void OpenAIAssist_ExecutesWithoutError()
    {
        // Act & Assert - Should not throw any exceptions
        InvokePrivate(_viewModel, "OpenAIAssist");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel?.Dispose();
        }
        base.Dispose(disposing);
    }
}