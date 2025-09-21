using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using System;
using System.Threading.Tasks;

namespace DatabaseConnectionTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔍 Testing Enterprise Database Connection...");

            try
            {
                // Build configuration
                var configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddEnvironmentVariables()
                    .Build();

                // Setup dependency injection
                var services = new ServiceCollection()
                    .AddLogging()
                    .AddEnterpriseDatabaseServices(configuration);

                var serviceProvider = services.BuildServiceProvider();

                // Test database connection
                var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

                using var context = await dbContextFactory.CreateDbContextAsync();

                Console.WriteLine("✅ Database context created successfully");

                // Test a simple query
                var canConnect = await context.Database.CanConnectAsync();
                Console.WriteLine($"✅ Database connectivity: {canConnect}");

                if (canConnect)
                {
                    // Try to get database info
                    var connection = context.Database.GetDbConnection();
                    Console.WriteLine($"✅ Connected to: {connection.Database} on {connection.DataSource}");

                    // Test a simple query
                    var enterpriseCount = await context.Enterprises.CountAsync();
                    Console.WriteLine($"✅ Found {enterpriseCount} enterprises in database");
                }

                Console.WriteLine("🎉 Enterprise database connection test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Database connection test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
    }
}