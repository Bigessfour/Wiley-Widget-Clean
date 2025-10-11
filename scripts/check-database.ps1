# Simple database check script
param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "WileyWidgetDev"
)

$ErrorActionPreference = "Stop"

try {
    $connectionString = "Server=$Server;Database=$Database;Trusted_Connection=True;Connection Timeout=30;"
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    $connection.Open()

    # Check database info
    $command = $connection.CreateCommand()
    $command.CommandText = "SELECT name, state_desc, recovery_model_desc FROM sys.databases WHERE name = '$Database'"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Database: $($reader['name']) - $($reader['state_desc']) - $($reader['recovery_model_desc'])"
    }
    $reader.Close()

    # Check tables
    $command.CommandText = "SELECT COUNT(*) as TableCount FROM sys.tables"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Tables: $($reader['TableCount'])"
    }
    $reader.Close()

    # Check if stats exist
    $command.CommandText = "SELECT COUNT(*) as StatsCount FROM sys.stats WHERE auto_created = 0"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Manual Statistics: $($reader['StatsCount'])"
    }
    $reader.Close()

    # Check indexes
    $command.CommandText = "SELECT COUNT(*) as IndexCount FROM sys.indexes WHERE type > 0"
    $reader = $command.ExecuteReader()
    if ($reader.Read()) {
        Write-Host "Indexes: $($reader['IndexCount'])"
    }
    $reader.Close()

    $connection.Close()
    Write-Host "Database check completed successfully"
}
catch {
    Write-Host "Error: $($_.Exception.Message)"
}
