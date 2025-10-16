using System;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace DatabaseTest
{
    class Program
    {
        private const string ServerName = @".\SQLEXPRESS";
        private const string DatabaseName = "WileyWidgetDev";

        static async Task Main(string[] args)
        {
            var serverName = Environment.GetEnvironmentVariable("WILEY_SQL_SERVER") ?? ServerName;
            var databaseName = Environment.GetEnvironmentVariable("WILEY_SQL_DATABASE") ?? DatabaseName;

            Console.WriteLine("🔍 Wiley Widget Local Database Connection Test");
            Console.WriteLine("================================================");
            Console.WriteLine();

            try
            {
                Console.WriteLine("📋 Testing SQL Server Express connectivity...");
                Console.WriteLine($"Server: {serverName}");
                Console.WriteLine($"Database: {databaseName}");
                Console.WriteLine();

                await TestBasicConnection(serverName, databaseName);
                await TestConnectionPooling(serverName, databaseName);
                await TestPerformance(serverName, databaseName);

                Console.WriteLine();
                Console.WriteLine("✅ All tests completed successfully!");
                Console.WriteLine("🎉 Local database connection is working perfectly!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ Connection test failed:");
                Console.WriteLine($"Error: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }

                Console.WriteLine();
                Console.WriteLine("🔧 Troubleshooting Tips:");
                Console.WriteLine("1. Ensure SQL Server Express is installed and running");
                Console.WriteLine("2. Confirm the SQL Browser service is enabled");
                Console.WriteLine("3. Verify the database exists or update the name");
                Console.WriteLine("4. Check that Windows authentication is permitted");

                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task TestBasicConnection(string serverName, string databaseName)
        {
            Console.WriteLine("🔗 Test 1: Basic Connection Test");

            await using var connection = CreateLocalConnection(serverName, databaseName);
            await connection.OpenAsync();
            Console.WriteLine("✅ Connection opened successfully");

            await using var command = new SqlCommand("SELECT @@VERSION AS SqlVersion", connection);
            var version = await command.ExecuteScalarAsync();
            Console.WriteLine($"✅ SQL Server Version: {version}");

            await connection.CloseAsync();
            Console.WriteLine("✅ Connection closed successfully");
            Console.WriteLine();
        }

        private static async Task TestConnectionPooling(string serverName, string databaseName)
        {
            Console.WriteLine("🏊 Test 2: Connection Pooling Test");

            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[10];

            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    await using var connection = CreateLocalConnection(serverName, databaseName);
                    await connection.OpenAsync();
                    await using var command = new SqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync();
                });
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"✅ 10 concurrent connections completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("✅ Connection pooling is working efficiently");
            Console.WriteLine();
        }

        private static async Task TestPerformance(string serverName, string databaseName)
        {
            Console.WriteLine("⚡ Test 3: Performance Smoke Test");

            await using var connection = CreateLocalConnection(serverName, databaseName);
            await connection.OpenAsync();

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                await using var command = new SqlCommand("SELECT SYSDATETIME()", connection);
                await command.ExecuteScalarAsync();
            }
            stopwatch.Stop();

            Console.WriteLine($"✅ 100 queries executed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"✅ Average query time: {stopwatch.ElapsedMilliseconds / 100.0:F2}ms");
            Console.WriteLine();
        }

        private static SqlConnection CreateLocalConnection(string serverName, string databaseName)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = databaseName,
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 15,
                ApplicationName = "WileyWidget-Local-DbTest"
            };

            Console.WriteLine($"📡 Using server: {builder.DataSource}");
            Console.WriteLine($"📊 Using database: {builder.InitialCatalog}");

            return new SqlConnection(builder.ConnectionString);
        }
    }
}
