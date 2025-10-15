using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prism.Navigation.Regions;
using Prism.Events;
using Serilog;
using WileyWidget.Configuration;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Views;
using WileyWidget.Data;
using WileyWidget.Business.Interfaces;

namespace WileyWidget.UiTests;

/// <summary>
/// Test setup class that initializes the DI container for UI tests.
/// This ensures that views can resolve their dependencies during testing.
/// </summary>
public static class TestDiSetup
{
    private static readonly object _lock = new object();
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Gets the service provider for tests with thread-safe initialization.
    /// </summary>
    public static IServiceProvider GetServiceProvider()
    {
        lock (_lock)
        {
            if (_serviceProvider != null)
                return _serviceProvider;

            try
            {
                var services = new ServiceCollection();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Integrated Security=True")
                           .UseSeeding(async (context) =>
                           {
                               await context.MunicipalAccounts.AddAsync(new MunicipalAccount { Id = 1, Name = "Test Account" });
                               await context.SaveChangesAsync();
                           }));
                services.AddSingleton<ISettingsService, MockSettingsService>();
                services.AddScoped<MainViewModel>();
                services.AddScoped<MainWindow>();
                services.AddScoped<AnalyticsView>();
                // Mock Syncfusion dependencies
                services.AddSingleton(provider => Mock.Of<Syncfusion.Windows.Tools.Controls.DockingManager>());
                _serviceProvider = services.BuildServiceProvider();
                Log.Information("DI container initialized for UI tests");
                return _serviceProvider;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize DI container: {Message}", ex.Message);
                throw;
            }
    }
}
    /// This should be called once before running UI tests.
    /// </summary>
    public static void Initialize()
    {
        if (_isInitialized)
            return;

        try
        {
            // Create host builder similar to the main app
            var hostBuilder = Host.CreateApplicationBuilder();

            // Set the base path to the UI test directory so it uses the test appsettings.json
            hostBuilder.Configuration.Sources.Clear();
            var assemblyLocation = typeof(TestDiSetup).Assembly.Location;
            var basePath = Path.GetDirectoryName(assemblyLocation) ?? AppDomain.CurrentDomain.BaseDirectory;
            hostBuilder.Configuration
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            // Configure the application (this sets up all services)
            hostBuilder.ConfigureWpfApplication();

            // Override database connection for tests to use SQL Server LocalDB
            hostBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetTestDb;Integrated Security=True"
            });

            // Mock IUnitOfWork for testing
            var mockUnitOfWork = new Mock<IUnitOfWork>();
            
            // Setup SaveChangesAsync to return success
            mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(1);
            
            // Setup transaction methods
            mockUnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);
            
            mockUnitOfWork.Setup(u => u.CommitTransactionAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            mockUnitOfWork.Setup(u => u.RollbackTransactionAsync(It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);
            
            // Setup ExecuteInTransactionAsync to execute the operation
            mockUnitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns<Func<Task>, System.Threading.CancellationToken>((operation, ct) => operation());
            
            mockUnitOfWork.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<object>>>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns<Func<Task<object>>, System.Threading.CancellationToken>((operation, ct) => operation());
            
            // Setup mock to return empty collections or test data as needed
            hostBuilder.Services.AddScoped<IUnitOfWork>(sp => mockUnitOfWork.Object);

            // Mock IEnterpriseRepository for Dashboard testing with sample data
            var mockEnterpriseRepo = new Mock<IEnterpriseRepository>();
            var sampleEnterprises = new List<WileyWidget.Models.Enterprise>
            {
                new WileyWidget.Models.Enterprise
                {
                    Id = 1,
                    Name = "Water Department",
                    Type = "Water",
                    Description = "Municipal water services",
                    TotalBudget = 500000m,
                    CurrentRate = 25.00m,
                    MonthlyExpenses = 35000m,
                    CitizenCount = 1500,
                    Status = WileyWidget.Models.EnterpriseStatus.Active,
                    LastModified = DateTime.Now.AddDays(-5)
                },
                new WileyWidget.Models.Enterprise
                {
                    Id = 2,
                    Name = "Sewer Department",
                    Type = "Sewer",
                    Description = "Wastewater management",
                    TotalBudget = 350000m,
                    CurrentRate = 20.00m,
                    MonthlyExpenses = 25000m,
                    CitizenCount = 1400,
                    Status = WileyWidget.Models.EnterpriseStatus.Active,
                    LastModified = DateTime.Now.AddDays(-10)
                },
                new WileyWidget.Models.Enterprise
                {
                    Id = 3,
                    Name = "Trash Collection",
                    Type = "Sanitation",
                    Description = "Solid waste disposal",
                    TotalBudget = 200000m,
                    CurrentRate = 15.00m,
                    MonthlyExpenses = 18000m,
                    CitizenCount = 1600,
                    Status = WileyWidget.Models.EnterpriseStatus.Active,
                    LastModified = DateTime.Now.AddDays(-2)
                }
            };
            mockEnterpriseRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(sampleEnterprises);
            mockEnterpriseRepo.Setup(x => x.GetCountAsync()).ReturnsAsync(sampleEnterprises.Count);
            mockEnterpriseRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => sampleEnterprises.FirstOrDefault(e => e.Id == id));
            mockEnterpriseRepo.Setup(x => x.AddAsync(It.IsAny<WileyWidget.Models.Enterprise>()))
                .ReturnsAsync((WileyWidget.Models.Enterprise e) => 
                {
                    e.Id = sampleEnterprises.Max(x => x.Id) + 1;
                    sampleEnterprises.Add(e);
                    return e;
                });
            mockEnterpriseRepo.Setup(x => x.UpdateAsync(It.IsAny<WileyWidget.Models.Enterprise>()))
                .ReturnsAsync((WileyWidget.Models.Enterprise e) => e);
            mockEnterpriseRepo.Setup(x => x.DeleteAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => 
                {
                    var enterprise = sampleEnterprises.FirstOrDefault(e => e.Id == id);
                    if (enterprise != null)
                    {
                        sampleEnterprises.Remove(enterprise);
                        return true;
                    }
                    return false;
                });
            hostBuilder.Services.AddScoped<IEnterpriseRepository>(sp => mockEnterpriseRepo.Object);

            // Mock IChargeCalculatorService for AI Assist testing with static responses
            var mockChargeCalculator = new Mock<IChargeCalculatorService>();
            mockChargeCalculator.Setup(x => x.CalculateRecommendedChargeAsync(It.IsAny<int>()))
                .ReturnsAsync((int enterpriseId) => new ServiceChargeRecommendation
                {
                    EnterpriseId = enterpriseId,
                    EnterpriseName = "Test Enterprise",
                    CurrentRate = 25.00m,
                    RecommendedRate = 30.00m,
                    TotalMonthlyExpenses = 420000m,
                    MonthlyRevenueAtRecommended = 42000m,
                    MonthlySurplus = 0m,
                    ReserveAllocation = 42000m,
                    CalculationDate = DateTime.Now,
                    Assumptions = new List<string> { "Test assumption" }
                });
            mockChargeCalculator.Setup(x => x.GenerateChargeScenarioAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
                .ReturnsAsync((int enterpriseId, decimal rateIncrease, decimal expenseChange) => new WhatIfScenario
                {
                    ScenarioName = $"Rate Increase {rateIncrease}%",
                    CurrentRate = 25.00m,
                    ProposedRate = 25.00m * (1 + rateIncrease / 100),
                    CurrentMonthlyExpenses = 42000m,
                    ProposedMonthlyExpenses = 42000m + expenseChange,
                    CurrentMonthlyRevenue = 35000m,
                    ProposedMonthlyRevenue = 35000m * (1 + rateIncrease / 100),
                    CurrentMonthlyBalance = 35000m - 42000m,
                    ProposedMonthlyBalance = (35000m * (1 + rateIncrease / 100)) - (42000m + expenseChange),
                    ImpactAnalysis = rateIncrease > 0 ? "Positive impact" : "Negative impact",
                    Recommendations = new List<string> { rateIncrease > 0 ? "Rate increase will improve financial position." : "Consider alternative cost reduction strategies." }
                });
            hostBuilder.Services.AddSingleton<IChargeCalculatorService>(sp => mockChargeCalculator.Object);

            // Mock Prism services that views expect
            var mockRegionManager = new Mock<IRegionManager>();
            mockRegionManager.Setup(rm => rm.Regions).Returns((IRegionCollection)null!);
            hostBuilder.Services.AddSingleton<IRegionManager>(sp => mockRegionManager.Object);

            var mockEventAggregator = new Mock<IEventAggregator>();
            var mockEventBase = new Mock<EventBase>();
            mockEventAggregator.Setup(ea => ea.GetEvent<PubSubEvent<string>>())
                .Returns(new PubSubEvent<string>());
            hostBuilder.Services.AddSingleton<IEventAggregator>(sp => mockEventAggregator.Object);

            // Mock IMunicipalAccountRepository for MunicipalAccountView testing with sample data
            var mockMunicipalAccountRepo = new Mock<IMunicipalAccountRepository>();
            var sampleAccounts = new List<WileyWidget.Models.MunicipalAccount>
            {
                new WileyWidget.Models.MunicipalAccount
                {
                    Id = 1,
                    AccountNumber = new WileyWidget.Models.AccountNumber("101-1000"),
                    Name = "General Fund - Cash",
                    Type = WileyWidget.Models.AccountType.Asset,
                    Fund = WileyWidget.Models.MunicipalFundType.General,
                    Balance = 125000.00m,
                    IsActive = true,
                    Notes = "Primary operating cash account"
                },
                new WileyWidget.Models.MunicipalAccount
                {
                    Id = 2,
                    AccountNumber = new WileyWidget.Models.AccountNumber("201-2000"),
                    Name = "Water Fund - Revenue",
                    Type = WileyWidget.Models.AccountType.Revenue,
                    Fund = WileyWidget.Models.MunicipalFundType.Water,
                    Balance = 45000.00m,
                    IsActive = true,
                    Notes = "Water service charges"
                },
                new WileyWidget.Models.MunicipalAccount
                {
                    Id = 3,
                    AccountNumber = new WileyWidget.Models.AccountNumber("301-3000"),
                    Name = "Sewer Fund - Expenses",
                    Type = WileyWidget.Models.AccountType.Expense,
                    Fund = WileyWidget.Models.MunicipalFundType.Sewer,
                    Balance = -22500.00m,
                    IsActive = true,
                    Notes = "Sewer maintenance costs"
                }
            };
            mockMunicipalAccountRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(sampleAccounts);
            mockMunicipalAccountRepo.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => sampleAccounts.FirstOrDefault(a => a.Id == id));
            mockMunicipalAccountRepo.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>()))
                .ReturnsAsync((string accountNumber) => sampleAccounts.FirstOrDefault(a => a.AccountNumber.Value == accountNumber));
            mockMunicipalAccountRepo.Setup(x => x.GetByFundAsync(It.IsAny<WileyWidget.Models.MunicipalFundType>()))
                .ReturnsAsync((WileyWidget.Models.MunicipalFundType fund) => sampleAccounts.Where(a => a.Fund == fund));
            mockMunicipalAccountRepo.Setup(x => x.GetByTypeAsync(It.IsAny<WileyWidget.Models.AccountType>()))
                .ReturnsAsync((WileyWidget.Models.AccountType type) => sampleAccounts.Where(a => a.Type == type));
            mockMunicipalAccountRepo.Setup(x => x.AddAsync(It.IsAny<WileyWidget.Models.MunicipalAccount>()))
                .ReturnsAsync((WileyWidget.Models.MunicipalAccount a) => 
                {
                    a.Id = sampleAccounts.Max(x => x.Id) + 1;
                    sampleAccounts.Add(a);
                    return a;
                });
            mockMunicipalAccountRepo.Setup(x => x.UpdateAsync(It.IsAny<WileyWidget.Models.MunicipalAccount>()))
                .ReturnsAsync((WileyWidget.Models.MunicipalAccount a) => a);
            mockMunicipalAccountRepo.Setup(x => x.DeleteAsync(It.IsAny<int>()))
                .ReturnsAsync((int id) => 
                {
                    var account = sampleAccounts.FirstOrDefault(a => a.Id == id);
                    if (account != null)
                    {
                        sampleAccounts.Remove(account);
                        return true;
                    }
                    return false;
                });
            hostBuilder.Services.AddScoped<IMunicipalAccountRepository>(sp => mockMunicipalAccountRepo.Object);

            // Build the host
            var host = hostBuilder.Build();

            // Initialize the database before setting the service provider
            EnsureTestDatabaseCreatedAsync(host.Services).GetAwaiter().GetResult();

            // Set the static service provider
            _serviceProvider = host.Services;

            // Set up Application.Current if not already set
            if (Application.Current == null)
            {
                // For StaFact tests, Application.Current should be set by the test runner
                // We don't create it ourselves as it may interfere with the test framework
            }

            // Store the service provider in Application properties (like the main app does)
            if (Application.Current != null)
            {
                Application.Current.Properties["ServiceProvider"] = _serviceProvider;
            }

            // Set the static App.ServiceProvider (this is a bit of a hack but necessary for views)
            typeof(WileyWidget.App).GetProperty("ServiceProvider")?.SetValue(null, _serviceProvider);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize DI container for UI tests", ex);
        }
    }

    /// <summary>
    /// Gets the service provider for tests.
    /// </summary>
    public static IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("DI container not initialized. Call Initialize() first.");

    /// <summary>
    /// Gets the service provider for tests (alias for compatibility).
    /// </summary>
    public static IServiceProvider GetServiceProvider() => ServiceProvider;

    /// <summary>
    /// Creates a view with proper scoped services and simulates the full WPF lifecycle.
    /// This addresses the critical DataContext initialization gap in UI tests.
    /// </summary>
    public static async Task<T> CreateViewWithFullLifecycleAsync<T>() where T : Window, new()
    {
        if (!typeof(T).IsAssignableFrom(typeof(MainWindow)) &&
            !typeof(T).IsAssignableFrom(typeof(BudgetView)) &&
            !typeof(T).IsAssignableFrom(typeof(EnterpriseView)) &&
            !typeof(T).IsAssignableFrom(typeof(UtilityCustomerView)))
        {
            throw new ArgumentException($"Type {typeof(T).Name} is not supported. Only main application views are supported.");
        }

        var serviceProvider = ServiceProvider ?? throw new InvalidOperationException("DI container not initialized. Call Initialize() first.");

        // Create a scoped service provider (like production OnWindowLoaded)
        using var scope = serviceProvider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        // Create the view using scoped services
        var view = scopedProvider.GetRequiredService<T>();

        // Simulate the WPF lifecycle that sets DataContext
        await SimulateViewLifecycleAsync(view, scopedProvider);

        return view;
    }

    /// <summary>
    /// Simulates the critical WPF view lifecycle events that initialize DataContext and ViewModel.
    /// This replicates what happens in production OnWindowLoaded.
    /// </summary>
    private static async Task SimulateViewLifecycleAsync<T>(T view, IServiceProvider scopedProvider) where T : Window
    {
        // Set DataContext using scoped services (like production)
        if (view is MainWindow mainWindow)
        {
            var mainViewModel = scopedProvider.GetRequiredService<ViewModels.MainViewModel>();
            view.DataContext = mainViewModel;

            // Subscribe to property changes (like production)
            mainViewModel.PropertyChanged += (s, e) => { /* Handle property changes */ };

            // Load persisted settings (like production)
            // mainViewModel.UseDynamicColumns = SettingsService.Instance.Current.UseDynamicColumns;

            // Initialize grid columns (like production)
            await Task.Delay(50); // Allow initial render
            view.UpdateLayout();

            var grid = mainWindow.FindName("Grid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (grid != null)
            {
                // Configure grid based on settings
                grid.AutoGenerateColumns = false;
                // Would call AddStaticColumns() here in production
            }
        }
        else if (view is BudgetView budgetView)
        {
            // Set appropriate ViewModel for BudgetView
            var budgetViewModel = scopedProvider.GetService<BudgetViewModel>();
            if (budgetViewModel != null)
            {
                view.DataContext = budgetViewModel;
            }
        }
        else if (view is EnterpriseView enterpriseView)
        {
            // Set appropriate ViewModel for EnterpriseView
            var enterpriseViewModel = scopedProvider.GetService<EnterpriseViewModel>();
            if (enterpriseViewModel != null)
            {
                view.DataContext = enterpriseViewModel;
            }
        }
        else if (view is UtilityCustomerView utilityView)
        {
            // Set appropriate ViewModel for UtilityCustomerView
            var utilityViewModel = scopedProvider.GetService<UtilityCustomerViewModel>();
            if (utilityViewModel != null)
            {
                view.DataContext = utilityViewModel;
            }
        }

        // Force layout and rendering (critical for Syncfusion controls)
        view.UpdateLayout();
        await Task.Delay(100); // Allow async rendering to complete

        // Pump messages to ensure all rendering is complete
        UiTestHelpers.DoEvents();
    }

    /// <summary>
    /// Creates a view using the legacy pattern for backward compatibility.
    /// WARNING: This does NOT initialize DataContext properly - use CreateViewWithFullLifecycleAsync instead.
    /// </summary>
    [Obsolete("Use CreateViewWithFullLifecycleAsync for proper DataContext initialization")]
    public static T CreateView<T>() where T : Window, new()
    {
        return new T();
    }
}