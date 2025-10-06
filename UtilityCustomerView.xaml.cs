using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using WileyWidget.Data;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using Serilog;
using WileyWidget.Models;

namespace WileyWidget;

/// <summary>
/// Customer Management Window - Provides full CRUD interface for utility customers
/// </summary>
public partial class UtilityCustomerView : Window
{
    private IServiceScope _viewScope;

    public UtilityCustomerView()
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

        // Create a scope for the view and resolve the repository from the scope
        IServiceProvider provider = null;

        // Try to get service provider from various sources (test environment, app, etc.)
#if DEBUG
        // For UI tests, try to get from TestDiSetup first
        try
        {
            var testDiSetupType = Type.GetType("WileyWidget.UiTests.TestDiSetup, WileyWidget.UiTests");
            if (testDiSetupType != null)
            {
                var serviceProviderProperty = testDiSetupType.GetProperty("ServiceProvider");
                provider = serviceProviderProperty?.GetValue(null) as IServiceProvider;
            }
        }
        catch { /* Ignore if not in test environment */ }
#endif

        // Fallback to app service provider
        if (provider == null)
        {
            provider = App.ServiceProvider ?? Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
        }

        if (provider == null)
            throw new InvalidOperationException("ServiceProvider is not available for UtilityCustomerView");

        UtilityCustomerViewModel viewModel;

        try
        {
            _viewScope = provider.CreateScope();
            var customerRepository = _viewScope.ServiceProvider.GetRequiredService<IUtilityCustomerRepository>();
            viewModel = new UtilityCustomerViewModel(customerRepository);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to resolve utility customer repository; using in-memory fallback.");
            _viewScope?.Dispose();
            _viewScope = null;
            viewModel = new UtilityCustomerViewModel(new FallbackUtilityCustomerRepository());
        }

        DataContext = viewModel;

        // Dispose the scope when the window is closed (only if we created one)
        if (_viewScope != null)
        {
            this.Closed += (_, _) => { try { _viewScope.Dispose(); } catch { } };
        }

        // Load customers when window opens
        Loaded += async (s, e) =>
        {
            if (DataContext is UtilityCustomerViewModel vm)
            {
                await vm.LoadCustomersAsync();
            }
        };
    }

    private sealed class FallbackUtilityCustomerRepository : IUtilityCustomerRepository
    {
        public Task<UtilityCustomer> AddAsync(UtilityCustomer customer) => Task.FromResult(customer);
        public Task<bool> DeleteAsync(int id) => Task.FromResult(false);
        public Task<bool> ExistsByAccountNumberAsync(string accountNumber, int? excludeId = null) => Task.FromResult(false);
        public Task<IEnumerable<UtilityCustomer>> GetActiveCustomersAsync() => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<IEnumerable<UtilityCustomer>> GetAllAsync() => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<UtilityCustomer> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<UtilityCustomer>(null);
        public Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType) => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<UtilityCustomer> GetByIdAsync(int id) => Task.FromResult<UtilityCustomer>(null);
        public Task<IEnumerable<UtilityCustomer>> GetByServiceLocationAsync(ServiceLocation serviceLocation) => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<int> GetCountAsync() => Task.FromResult(0);
        public Task<IEnumerable<UtilityCustomer>> GetCustomersOutsideCityLimitsAsync() => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<IEnumerable<UtilityCustomer>> GetCustomersWithBalanceAsync() => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<IEnumerable<UtilityCustomer>> SearchAsync(string searchTerm) => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<UtilityCustomer> UpdateAsync(UtilityCustomer customer) => Task.FromResult(customer);
    }

    /// <summary>
    /// Show the Customer Management window
    /// </summary>
    public static void ShowCustomerWindow()
    {
        var window = new UtilityCustomerView();
        window.Show();
    }

    /// <summary>
    /// Show the Customer Management window as dialog
    /// </summary>
    public static bool? ShowCustomerDialog()
    {
        var window = new UtilityCustomerView();
        return window.ShowDialog();
    }
}