using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WileyWidget.Configuration;
using WileyWidget.Data;
using Microsoft.EntityFrameworkCore;

#nullable enable

namespace DbConnTest
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            try
            {
                RunAsync(args).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static async Task RunAsync(string[] args)
        {
            Console.WriteLine("🔍 Testing Enterprise Database Connection (tools/DbConnTest)...");

            static string? FindAppSettings()
            {
                var dir = new System.IO.DirectoryInfo(System.IO.Directory.GetCurrentDirectory());
                for (var i = 0; i < 8 && dir != null; i++)
                {
                    var candidate = System.IO.Path.Combine(dir.FullName, "appsettings.json");
                    if (System.IO.File.Exists(candidate))
                    {
                        return candidate;
                    }
                    dir = dir.Parent;
                }
                return null;
            }

            var appSettingsPath = FindAppSettings();
            if (appSettingsPath is null)
            {
                Console.WriteLine("❌ Could not locate appsettings.json in parent directories. Place it near the solution root or run from repository root.");
                return;
            }

            var configuration = new ConfigurationBuilder()
                .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection()
                .AddLogging(config => config.AddConsole())
                .AddSingleton<IConfiguration>(configuration);

            using var serviceProvider = services.BuildServiceProvider();

            try
            {
                var factory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
                await using var ctx = await factory.CreateDbContextAsync();
                Console.WriteLine("✅ DbContext created");

                var connection = ctx.Database.GetDbConnection();
                await connection.OpenAsync();

                try
                {
                    try
                    {
                        await ctx.Database.MigrateAsync();
                        Console.WriteLine("✅ Database migrations applied");
                    }
                    catch (Exception migrateEx)
                    {
                        Console.WriteLine($"⚠️ Database migration failed ({migrateEx.Message}). Falling back to EnsureCreated.");
                        var created = await ctx.Database.EnsureCreatedAsync();
                        if (created)
                        {
                            Console.WriteLine("✅ Database schema created via EnsureCreated fallback");
                        }
                    }

                    var canConnect = await ctx.Database.CanConnectAsync();
                    Console.WriteLine($"✅ Database.CanConnectAsync: {canConnect}");
                    if (canConnect)
                    {
                        var conn = ctx.Database.GetDbConnection();
                        Console.WriteLine($"Connected to: {conn.DataSource} / {conn.Database}");
                        // Temporarily disabled - Enterprises removed
                        // var count = await ctx.Enterprises.CountAsync();
                        // Console.WriteLine($"Enterprise count: {count}");
                        Console.WriteLine("Database connection successful");
                    }
                }
                finally
                {
                    await connection.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine(ex);
            }

            Console.WriteLine("Done.");
        }
    }
}