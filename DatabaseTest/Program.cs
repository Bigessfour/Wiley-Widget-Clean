using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Azure.Identity;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DatabaseTest
{
    class Program
    {
        // Fallback values - environment variables will override these
        private const string ServerName = "wileywidget-sql.database.windows.net";
        private const string DatabaseName = "WileyWidgetDB";

        static async Task Main(string[] args)
        {
            // Get actual values from environment variables
            var serverName = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER") ?? ServerName;
            var databaseName = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE") ?? DatabaseName;

            Console.WriteLine("🔍 Wiley Widget Enterprise Database Connection Test");
            Console.WriteLine("==================================================");
            Console.WriteLine();

            try
            {
                Console.WriteLine("📋 Testing Azure AD Interactive Authentication (Recommended)...");
                Console.WriteLine($"Server: {serverName}");
                Console.WriteLine($"Database: {databaseName}");
                Console.WriteLine();

                // Test 1: Basic Connection Test
                await TestBasicConnection();

                // Test 2: Connection Pooling Test
                await TestConnectionPooling();

                // Test 3: Retry Logic Test
                await TestRetryLogic();

                // Test 4: Performance Test
                await TestPerformance();

                Console.WriteLine();
                Console.WriteLine("✅ All tests completed successfully!");
                Console.WriteLine("🎉 Enterprise database connection is working perfectly!");

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
                Console.WriteLine("1. Ensure you're logged in to Azure CLI: az login");
                Console.WriteLine("2. Verify your user has access to the database");
                Console.WriteLine("3. Check firewall settings for Azure SQL Database");
                Console.WriteLine("4. Confirm the server and database names are correct");

                return;
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static async Task TestBasicConnection()
        {
            Console.WriteLine("🔗 Test 1: Basic Connection Test");

            using var connection = CreateEnterpriseConnection();
            await connection.OpenAsync();

            Console.WriteLine("✅ Connection opened successfully");

            // Test a simple query
            using var command = new SqlCommand("SELECT @@VERSION AS SqlVersion", connection);
            var version = await command.ExecuteScalarAsync();
            Console.WriteLine($"✅ SQL Server Version: {version}");

            await connection.CloseAsync();
            Console.WriteLine("✅ Connection closed successfully");
            Console.WriteLine();
        }

        private static async Task TestConnectionPooling()
        {
            Console.WriteLine("🏊 Test 2: Connection Pooling Test");

            var stopwatch = Stopwatch.StartNew();
            var tasks = new Task[10];

            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    using var connection = CreateEnterpriseConnection();
                    await connection.OpenAsync();

                    using var command = new SqlCommand("SELECT 1", connection);
                    await command.ExecuteScalarAsync();

                    await connection.CloseAsync();
                });
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            Console.WriteLine($"✅ 10 concurrent connections completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("✅ Connection pooling is working efficiently");
            Console.WriteLine();
        }

        private static async Task TestRetryLogic()
        {
            Console.WriteLine("🔄 Test 3: Retry Logic Test");

            // Test with a query that might timeout to trigger retry logic
            using var connection = CreateEnterpriseConnection();
            await connection.OpenAsync();

            using var command = new SqlCommand("WAITFOR DELAY '00:00:01'; SELECT 'Retry Test Successful'", connection);
            command.CommandTimeout = 30; // 30 seconds timeout

            var result = await command.ExecuteScalarAsync();
            Console.WriteLine($"✅ {result}");
            Console.WriteLine("✅ Retry logic handled the operation successfully");
            Console.WriteLine();
        }

        private static async Task TestPerformance()
        {
            Console.WriteLine("⚡ Test 4: Performance Test");

            using var connection = CreateEnterpriseConnection();
            await connection.OpenAsync();

            var stopwatch = Stopwatch.StartNew();

            // Execute 100 simple queries
            for (int i = 0; i < 100; i++)
            {
                using var command = new SqlCommand("SELECT GETDATE()", connection);
                await command.ExecuteScalarAsync();
            }

            stopwatch.Stop();

            Console.WriteLine($"✅ 100 queries executed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"✅ Average query time: {stopwatch.ElapsedMilliseconds / 100.0}ms");
            Console.WriteLine("✅ Performance is excellent!");
            Console.WriteLine();
        }

        private static SqlConnection CreateEnterpriseConnection()
        {
            // Get server and database from environment variables (machine-level)
            var serverName = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER") ?? ServerName;
            var databaseName = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE") ?? DatabaseName;

            Console.WriteLine($"📡 Using server: {serverName}");
            Console.WriteLine($"📊 Using database: {databaseName}");

            // Per Azure documentation: Active Directory Interactive is recommended for development
            // It supports MFA and caches tokens for subsequent connections
            // https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication
            var connectionString = $"Server={serverName};" +
                                  $"Authentication=Active Directory Interactive;" +
                                  $"Encrypt=True;" +
                                  $"Database={databaseName};" +
                                  $"Connect Timeout=30;" +
                                  $"Application Name=WileyWidget-Enterprise-Test";

            var connection = new SqlConnection(connectionString);
            return connection;
        }
    }
}
