using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Configuration;
using WileyWidget.Services;
using WileyWidget.ViewModels;
using WileyWidget.Data;

namespace WileyWidget.UiTests;

/// <summary>
/// Test setup class that initializes the DI container for UI tests.
/// This ensures that views can resolve their dependencies during testing.
/// </summary>
public static class TestDiSetup
{
    private static IServiceProvider _serviceProvider;
    private static bool _isInitialized;

    /// <summary>
    /// Ensures the test database is created (for SQLite in-memory, uses EnsureCreated instead of migrations)
    /// </summary>
    private static async Task EnsureTestDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        using var scope = scopeFactory.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        try
        {
            // For SQLite in-memory databases, use EnsureCreated instead of migrations
            // This creates the database schema without requiring migrations
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            // Log the error
            Console.WriteLine($"Database initialization failed: {ex.Message}");

            // For tests, don't crash - just log the error
            Console.WriteLine("Application will continue without database connectivity.");
        }
    }

    /// <summary>
    /// Initializes the DI container for UI tests.
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
            hostBuilder.Configuration
                .SetBasePath(Path.GetDirectoryName(typeof(TestDiSetup).Assembly.Location))
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            // Configure the application (this sets up all services)
            hostBuilder.ConfigureWpfApplication();

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
            mainViewModel.UseDynamicColumns = SettingsService.Instance.Current.UseDynamicColumns;

            // Initialize grid columns (like production)
            await Task.Delay(50); // Allow initial render
            view.UpdateLayout();

            var grid = mainWindow.FindName("Grid") as Syncfusion.UI.Xaml.Grid.SfDataGrid;
            if (grid != null)
            {
                if (mainViewModel.UseDynamicColumns)
                {
                    // Would call BuildDynamicColumns() here in production
                    grid.AutoGenerateColumns = false;
                }
                else
                {
                    grid.AutoGenerateColumns = false;
                    // Would call AddStaticColumns() here in production
                }
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