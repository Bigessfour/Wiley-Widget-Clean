using System;
using System.IO;
using System.Windows;
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
}