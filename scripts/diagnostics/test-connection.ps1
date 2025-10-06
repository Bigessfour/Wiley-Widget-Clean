#!/usr/bin/env pwsh

# Test Azure SQL Database connection
param(
    [string]$Server = "wileywidget-sql.database.windows.net",
    [string]$Database = "WileyWidgetDB"
)

Write-Output "Testing Azure SQL Database connection..."
Write-Output "Server: $Server"
Write-Output "Database: $Database"

# Test with sqlcmd using different authentication methods
$testMethods = @(
    "ActiveDirectoryDefault",
    "ActiveDirectoryAzCli",
    "ActiveDirectoryInteractive"
)

foreach ($method in $testMethods) {
    Write-Output "`n--- Testing $method ---"
    try {
        $result = sqlcmd -S "tcp:$Server,1433" -d $Database --authentication-method $method -Q "SELECT 'SUCCESS' as Result, DB_NAME() as DatabaseName" -t 10 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Output "SUCCESS: $method"
            Write-Output $result
        }
        else {
            Write-Output "FAILED: $method"
            Write-Output $result
        }
    }
    catch {
        Write-Output "ERROR: $method - $($_.Exception.Message)"
    }
}

Write-Output "`n--- Connection String Test ---"
# Test the connection string format
$connectionStrings = @(
    "Server=tcp:$Server,1433;Database=$Database;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
    "Server=tcp:$Server,1433;Database=$Database;Authentication=Active Directory Integrated;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
)

foreach ($connStr in $connectionStrings) {
    Write-Output "`nTesting: $($connStr.Substring(0, [Math]::Min(60, $connStr.Length)))..."
    # This would require a .NET test app to properly test Entity Framework connections
    Write-Output "Connection string format: $(if ($connStr -match 'Active Directory Default') {'Default'} else {'Integrated'})"
}
