<#
.SYNOPSIS
    Sets Windows machine-level environment variables from a .env file.

.DESCRIPTION
    Reads KEY=VALUE pairs from the provided .env file and writes them to the
    Machine environment scope using [Environment]::SetEnvironmentVariable.
    Optionally refreshes the environment for the current process and prints a summary.

.PARAMETER EnvFile
    Path to the .env file. Defaults to workspace-root/.env.production

.PARAMETER WhatIf
    Show what would change without making changes.

.EXAMPLE
    .\scripts\set-machine-env-from-dotenv.ps1 -EnvFile .env.production

.NOTES
    Sets environment variables at User scope. No administrator privileges required.
#>
param(
    [string]$EnvFile = (Join-Path $PSScriptRoot '..' '.env.production'),
    [switch]$WhatIf
)

Write-Host "🛠️ Applying machine-level environment variables from: $EnvFile" -ForegroundColor Cyan

if (-not (Test-Path -LiteralPath $EnvFile)) {
    Write-Host "❌ Env file not found: $EnvFile" -ForegroundColor Red
    exit 1
}

$setCount = 0
$skipped = 0

Get-Content -LiteralPath $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { return }
    $idx = $line.IndexOf('=')
    if ($idx -lt 1) { return }
    $key = $line.Substring(0, $idx).Trim()
    $val = $line.Substring($idx + 1).Trim()

    # Strip surrounding quotes
    if (($val.StartsWith('"') -and $val.EndsWith('"')) -or ($val.StartsWith("'") -and $val.EndsWith("'"))) {
        $val = $val.Substring(1, $val.Length - 2)
    }

    # Skip AzureKeyVault references for now
    if ($val -like '@AzureKeyVault(*)') {
        Write-Host "  ⚠️ Skipping Key Vault reference for $key" -ForegroundColor Yellow
        $skipped++
        return
    }

    if ($WhatIf) {
        Write-Host "  ▶️ Would set [User] $key" -ForegroundColor DarkGray
    }
    else {
        [Environment]::SetEnvironmentVariable($key, $val, 'User')
        Write-Host "  ✅ Set [User] $key" -ForegroundColor Green
        $setCount++
    }
}

if (-not $WhatIf) {
    Write-Host "\nℹ️ Restart terminals or sign out/in to propagate User variables to new processes." -ForegroundColor Yellow
}

Write-Host "\nSummary: Set=$setCount, Skipped=$skipped" -ForegroundColor Cyan
