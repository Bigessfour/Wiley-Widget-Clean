// See https://aka.ms/new-console-template for more information
using Microsoft.Data.SqlClient;
using Azure.Identity;
using System;

Console.WriteLine("Testing Azure SQL Database Connection...");

try
{
    // Build connection string
    var server = Environment.GetEnvironmentVariable("AZURE_SQL_SERVER");
    var database = Environment.GetEnvironmentVariable("AZURE_SQL_DATABASE");

    if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database))
    {
        Console.WriteLine("ERROR: AZURE_SQL_SERVER or AZURE_SQL_DATABASE environment variables not set");
        return;
    }

    var connectionString = $"Server=tcp:{server}.database.windows.net,1433;Initial Catalog={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;";

    Console.WriteLine($"Connecting to: {server}/{database}");

    using var connection = new SqlConnection(connectionString);

    // Note: When using Authentication=Active Directory Default, SqlClient handles authentication automatically
    // We don't need to manually set AccessToken

    Console.WriteLine("Opening connection...");
    await connection.OpenAsync();

    Console.WriteLine("✅ SUCCESS: Database connection established!");

    // Test a simple query
    using var command = new SqlCommand("SELECT @@VERSION", connection);
    var result = await command.ExecuteScalarAsync();
    Console.WriteLine($"SQL Server Version: {result}");

    // Test if our database has tables
    using var tableCommand = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", connection);
    var tableCount = await tableCommand.ExecuteScalarAsync();
    Console.WriteLine($"Tables in database: {tableCount}");

    Console.WriteLine("✅ Database test completed successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERROR: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
    }
}
