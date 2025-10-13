using System;
using System.Collections.Generic;
using System.Threading;
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
using WileyWidget.Business.Interfaces;
using BusinessInterfaces = WileyWidget.Business.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

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

    private sealed class FallbackUnitOfWork : IUnitOfWork
    {
        public FallbackUnitOfWork()
        {
            UtilityCustomers = new FallbackUtilityCustomerRepository();
        }

        public BusinessInterfaces.IEnterpriseRepository Enterprises => throw new NotSupportedException("Fallback unit of work does not provide enterprise repository support.");
        public BusinessInterfaces.IMunicipalAccountRepository MunicipalAccounts => throw new NotSupportedException("Fallback unit of work does not provide municipal account repository support.");
        public BusinessInterfaces.IBudgetRepository Budgets => throw new NotSupportedException("Fallback unit of work does not provide budget repository support.");
        public BusinessInterfaces.IDepartmentRepository Departments => throw new NotSupportedException("Fallback unit of work does not provide department repository support.");
        public IUtilityCustomerRepository UtilityCustomers { get; }

        public Task<FiscalYearSettings?> GetFiscalYearSettingsAsync() => Task.FromResult<FiscalYearSettings?>(null);
        public Task SaveFiscalYearSettingsAsync(FiscalYearSettings settings) => Task.CompletedTask;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Fallback unit of work does not support transactions.");
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException("Fallback unit of work does not support transactional execution.");
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => throw new NotSupportedException("Fallback unit of work does not support transactional execution.");
        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
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
                var unitOfWork = scopedProvider.GetRequiredService<IUnitOfWork>();
                return new UtilityCustomerViewModel(unitOfWork);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to resolve utility customer dependencies; using in-memory fallback.");
                _viewScope?.Dispose();
                _viewScope = null;
            }
        }

#pragma warning disable CA2000 // The ViewModel takes ownership of the FallbackUnitOfWork and disposes it
        return new UtilityCustomerViewModel(new FallbackUnitOfWork());
#pragma warning restore CA2000
    }
}