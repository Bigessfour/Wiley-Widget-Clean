using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WileyWidget.Data;
using System;

namespace DatabaseSeeder
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Wiley Widget Database Seeder");
            Console.WriteLine("============================");

            try
            {
                // Create host with configuration
                var host = Host.CreateDefaultBuilder(args)
                    .ConfigureAppConfiguration((context, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: false);
                    })
                    .ConfigureServices((context, services) =>
                    {
                        // Add DbContext
                        services.AddDbContext<AppDbContext>(options =>
                            options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));

                        // Add seeder
                        services.AddScoped<WileyWidget.Data.DatabaseSeeder>();
                    })
                    .Build();

                // Run seeder
                using var scope = host.Services.CreateScope();
                var seeder = scope.ServiceProvider.GetRequiredService<WileyWidget.Data.DatabaseSeeder>();
                await seeder.SeedAsync();

                Console.WriteLine("\n✅ Database seeding completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
            }
        }
    }
}
