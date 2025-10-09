using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Syncfusion.SfSkinManager;
using Syncfusion.Windows.Shared;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.Services;
using WileyWidget.ViewModels;

namespace WileyWidget;

/// <summary>
/// Customer Management Window - Provides full CRUD interface for utility customers
/// </summary>
public partial class UtilityCustomerView : Window
{
    private IServiceScope? _viewScope;

    public UtilityCustomerView()
        : this(serviceProvider: null, viewModel: null)
    {
    }

    public UtilityCustomerView(IServiceProvider serviceProvider)
        : this(serviceProvider, viewModel: null)
    {
    }

    public UtilityCustomerView(UtilityCustomerViewModel viewModel)
        : this(serviceProvider: null, viewModel: viewModel)
    {
    }

    private UtilityCustomerView(IServiceProvider? serviceProvider, UtilityCustomerViewModel? viewModel)
    {
        InitializeComponent();

        // Apply current theme
        ThemeUtility.TryApplyTheme(this, SettingsService.Instance.Current.Theme);

        if (viewModel is not null)
        {
            _viewScope = null;
            DataContext = viewModel;
        }
        else
        {
            var resolvedProvider = ResolveServiceProvider(serviceProvider);
            DataContext = CreateViewModel(resolvedProvider);
        }

        if (_viewScope != null)
        {
            Closed += (_, _) =>
            {
                try
                {
                    _viewScope.Dispose();
                }
                catch
                {
                }
            };
        }

        Loaded += async (_, _) =>
        {
            if (DataContext is UtilityCustomerViewModel vm && vm.Customers.Count == 0)
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
        public Task<UtilityCustomer?> GetByAccountNumberAsync(string accountNumber) => Task.FromResult<UtilityCustomer?>(null);
        public Task<IEnumerable<UtilityCustomer>> GetByCustomerTypeAsync(CustomerType customerType) => Task.FromResult<IEnumerable<UtilityCustomer>>(Array.Empty<UtilityCustomer>());
        public Task<UtilityCustomer?> GetByIdAsync(int id) => Task.FromResult<UtilityCustomer?>(null);
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

    public new object? FindName(string name)
    {
        var result = base.FindName(name);
        if (result is not null)
        {
            return result;
        }

        return name switch
        {
            nameof(CustomerGrid) => CustomerGrid,
            nameof(CustomerRibbon) => CustomerRibbon,
            _ => null
        };
    }

    private IServiceProvider? ResolveServiceProvider(IServiceProvider? overrideProvider)
    {
        if (overrideProvider is not null)
        {
            return overrideProvider;
        }

        IServiceProvider? provider = null;

#if DEBUG
        try
        {
            var testDiSetupType = Type.GetType("WileyWidget.UiTests.TestDiSetup, WileyWidget.UiTests");
            if (testDiSetupType != null)
            {
                var serviceProviderProperty = testDiSetupType.GetProperty("ServiceProvider");
                provider = serviceProviderProperty?.GetValue(null) as IServiceProvider;
            }
        }
        catch
        {
            // Ignore if not in test environment.
        }
#endif

        if (provider is not null)
        {
            return provider;
        }

        if (App.ServiceProvider is not null)
        {
            return App.ServiceProvider;
        }

        return Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
    }

    private UtilityCustomerViewModel CreateViewModel(IServiceProvider? provider)
    {
        if (provider is not null)
        {
            try
            {
                _viewScope = provider.CreateScope();
                var scopedProvider = _viewScope.ServiceProvider;
                var customerRepository = scopedProvider.GetRequiredService<IUtilityCustomerRepository>();
                var logger = scopedProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UtilityCustomerViewModel>>();
                return new UtilityCustomerViewModel(customerRepository);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve utility customer dependencies; using in-memory fallback.");
                _viewScope?.Dispose();
                _viewScope = null;
            }
        }

        var fallbackLogger = App.ServiceProvider?.GetService<Microsoft.Extensions.Logging.ILogger<UtilityCustomerViewModel>>() ?? NullLogger<UtilityCustomerViewModel>.Instance;
        return new UtilityCustomerViewModel(new FallbackUtilityCustomerRepository());
    }
}