using System;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace DatabaseSetup
{
    class Program
    {
        private const string ServerName = "wileywidget-sql.database.windows.net";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("🔧 Setting up Azure AD user access to WileyWidgetDB");
            Console.WriteLine("==================================================");
            Console.WriteLine();

            try
            {
                // Step 1: Connect to master database as Azure AD admin
                Console.WriteLine("📡 Connecting to master database as Azure AD admin...");
                
                var masterConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = ServerName,
                    InitialCatalog = "master",
                    Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive,
                    Encrypt = true,
                    TrustServerCertificate = false,
                    ConnectTimeout = 30,
                    ApplicationName = "WileyWidget-Setup"
                };

                using var masterConnection = new SqlConnection(masterConnectionString.ConnectionString);
                await masterConnection.OpenAsync();
                Console.WriteLine("✅ Connected to master database successfully");

                // Step 2: Connect to target database and create user
                Console.WriteLine("📊 Connecting to WileyWidgetDB...");
                
                var dbConnectionString = new SqlConnectionStringBuilder
                {
                    DataSource = ServerName,
                    InitialCatalog = "WileyWidgetDB",
                    Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive,
                    Encrypt = true,
                    TrustServerCertificate = false,
                    ConnectTimeout = 30,
                    ApplicationName = "WileyWidget-Setup"
                };

                using var dbConnection = new SqlConnection(dbConnectionString.ConnectionString);
                await dbConnection.OpenAsync();
                Console.WriteLine("✅ Connected to WileyWidgetDB successfully");

                // Step 3: Create user and grant permissions
                Console.WriteLine("👤 Creating Azure AD user in database...");
                
                var createUserSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'bigessfour@gmail.com')
                    BEGIN
                        CREATE USER [bigessfour@gmail.com] FROM EXTERNAL PROVIDER;
                        ALTER ROLE db_owner ADD MEMBER [bigessfour@gmail.com];
                        PRINT 'User created and granted db_owner role';
                    END
                    ELSE
                    BEGIN
                        PRINT 'User already exists';
                    END";

                using var command = new SqlCommand(createUserSql, dbConnection);
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("✅ User setup completed");

                // Step 4: Test permissions
                Console.WriteLine("🧪 Testing user permissions...");
                using var testCommand = new SqlCommand("SELECT USER_NAME() as CurrentUser, IS_ROLEMEMBER('db_owner') as IsOwner", dbConnection);
                using var reader = await testCommand.ExecuteReaderAsync();
                
                if (await reader.ReadAsync())
                {
                    var currentUser = reader["CurrentUser"];
                    var isOwner = reader["IsOwner"];
                    Console.WriteLine($"✅ Current user: {currentUser}");
                    Console.WriteLine($"✅ Is db_owner: {isOwner}");
                }

                Console.WriteLine();
                Console.WriteLine("🎉 Azure AD authentication setup completed successfully!");
                Console.WriteLine("✅ You now have passwordless access to WileyWidgetDB");

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