using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace DatabaseSetup
{
    class Program
    {
        private const string ServerName = @".\SQLEXPRESS";
        private const string DatabaseName = "WileyWidgetDev";

        static async Task Main(string[] args)
        {
            Console.WriteLine("🔧 Setting up local SQL Server Express database for Wiley Widget");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            try
            {
                var masterConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = ServerName,
                    InitialCatalog = "master",
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    ConnectTimeout = 15,
                    ApplicationName = "WileyWidget-Setup"
                };

                using var masterConnection = new SqlConnection(masterConnectionString.ConnectionString);
                Console.WriteLine("📡 Connecting to local SQL Server Express instance...");
                await masterConnection.OpenAsync();
                Console.WriteLine("✅ Connected to master database successfully");

                // Create database if it does not exist
                var createDatabaseSql = $@"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{DatabaseName}')
BEGIN
    PRINT 'Creating database {DatabaseName}...';
    CREATE DATABASE [{DatabaseName}];
END
ELSE
BEGIN
    PRINT 'Database {DatabaseName} already exists.';
END";

                using (var createDbCommand = new SqlCommand(createDatabaseSql, masterConnection))
                {
                    await createDbCommand.ExecuteNonQueryAsync();
                }

                Console.WriteLine($"✅ Database {DatabaseName} is ready");

                // Validate connectivity to the target database
                var targetConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = ServerName,
                    InitialCatalog = DatabaseName,
                    IntegratedSecurity = true,
                    TrustServerCertificate = true,
                    ConnectTimeout = 15,
                    ApplicationName = "WileyWidget-Setup"
                };

                using var targetConnection = new SqlConnection(targetConnectionString.ConnectionString);
                Console.WriteLine($"📊 Connecting to {DatabaseName}...");
                await targetConnection.OpenAsync();
                Console.WriteLine("✅ Verified connectivity to local database");

                // Ensure the default schema exists and basic health checks pass
                const string smokeTestSql = "SELECT DB_NAME() AS DatabaseName, SUSER_SNAME() AS CurrentUser";
                using var smokeTestCommand = new SqlCommand(smokeTestSql, targetConnection);
                using var reader = await smokeTestCommand.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Console.WriteLine($"🧪 Connection summary: Database={reader["DatabaseName"]}, User={reader["CurrentUser"]}");
                }

                Console.WriteLine();
                Console.WriteLine("🎉 Local SQL Server Express setup completed successfully!");
                Console.WriteLine($"✅ Update your connection string to: {targetConnectionString.ConnectionString}");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("❌ Setup failed:");
                Console.WriteLine($"Error: {ex.Message}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}