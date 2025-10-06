# Apply Database Migrations Script
# This script applies database migration scripts generated during CI/CD
# Usage: .\apply-migrations.ps1 -ConnectionString "your-connection-string" -ScriptPath "migration-script.sql"

param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString,

    [Parameter(Mandatory = $true)]
    [string]$ScriptPath,

    [switch]$WhatIf
)

Write-Information "=== Database Migration Application Script ===" -InformationAction Continue
Write-Information "Connection String: $($ConnectionString.Substring(0, [Math]::Min(50, $ConnectionString.Length)))..." -InformationAction Continue
Write-Information "Script Path: $ScriptPath" -InformationAction Continue
Write-Information "WhatIf Mode: $WhatIf" -InformationAction Continue
Write-Information "" -InformationAction Continue

try {
    # Check if script file exists
    if (!(Test-Path $ScriptPath)) {
        throw "Migration script not found at: $ScriptPath"
    }

    # Read the migration script
    $scriptContent = Get-Content $ScriptPath -Raw
    Write-Information "Migration script loaded successfully ($($scriptContent.Length) characters)" -InformationAction Continue

    if ($WhatIf) {
        Write-Information "WhatIf mode - would execute migration script" -InformationAction Continue
        Write-Information "Script preview (first 500 chars):" -InformationAction Continue
        Write-Output $scriptContent.Substring(0, [Math]::Min(500, $scriptContent.Length))
        return
    }

    # Apply the migration using dotnet ef database update
    # This is safer than executing raw SQL in production
    Write-Information "Applying migrations using Entity Framework..." -InformationAction Continue

    $env:ConnectionStrings__DefaultConnection = $ConnectionString
    dotnet ef database update --project ..\WileyWidget.csproj

    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ Database migrations applied successfully!" -InformationAction Continue
    }
    else {
        throw "Migration application failed with exit code: $LASTEXITCODE"
    }

}
catch {
    Write-Information "❌ Migration application failed: $($_.Exception.Message)" -InformationAction Continue
    throw
}

Write-Information "Migration process completed." -InformationAction Continue
