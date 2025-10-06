using Microsoft.Data.SqlClient;

Console.WriteLine("Granting database permissions...");

string connectionString = "Server=tcp:wileywidget-sql.database.windows.net,1433;Database=WileyWidgetDB;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
string query = @"
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'bigessfour@gmail.com')
BEGIN
    CREATE USER [bigessfour@gmail.com] FROM EXTERNAL PROVIDER;
END

ALTER ROLE db_datareader ADD MEMBER [bigessfour@gmail.com];
ALTER ROLE db_datawriter ADD MEMBER [bigessfour@gmail.com];
ALTER ROLE db_ddladmin ADD MEMBER [bigessfour@gmail.com];
";

try
{
    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();
        Console.WriteLine("Connected to database successfully");

        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.ExecuteNonQuery();
            Console.WriteLine("Database permissions granted successfully");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
