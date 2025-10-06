# Configure XAI API Key for Wiley Widget
# This script sets the XAI API key in both environment variables and appsettings.json

param(
    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [Parameter(Mandatory = $false)]
    [switch]$PersistEnvironmentVariable,

    [Parameter(Mandatory = $false)]
    [string]$AppSettingsPath = "appsettings.json"
)

Write-Host "🤖 Configuring XAI API Key for Wiley Widget..." -ForegroundColor Cyan

# Validate API key format (basic check)
if ($ApiKey.Length -lt 10) {
    Write-Error "API key appears to be too short. Please provide a valid xAI API key."
    exit 1
}

# Update appsettings.json
if (Test-Path $AppSettingsPath) {
    try {
        $config = Get-Content $AppSettingsPath -Raw | ConvertFrom-Json

        if (-not $config.XAI) {
            $config | Add-Member -Type NoteProperty -Name "XAI" -Value @{
                "ApiKey"         = ""
                "BaseUrl"        = "https://api.x.ai/v1/"
                "Model"          = "grok-4-0709"
                "RequireService" = $false
            }
        }

        $config.XAI.ApiKey = $ApiKey

        $config | ConvertTo-Json -Depth 10 | Set-Content $AppSettingsPath
        Write-Host "✅ Updated $AppSettingsPath with XAI API key" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to update appsettings.json: $_"
        exit 1
    }
}
else {
    Write-Warning "appsettings.json not found at $AppSettingsPath"
}

# Set environment variable if requested
if ($PersistEnvironmentVariable) {
    try {
        [Environment]::SetEnvironmentVariable("XAI_API_KEY", $ApiKey, "User")
        Write-Host "✅ Set XAI_API_KEY environment variable for current user" -ForegroundColor Green

        # Also set for current process
        $env:XAI_API_KEY = $ApiKey
        Write-Host "✅ Set XAI_API_KEY for current PowerShell session" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to set environment variable: $_"
        exit 1
    }
}
else {
    # Set for current session only
    $env:XAI_API_KEY = $ApiKey
    Write-Host "✅ Set XAI_API_KEY for current PowerShell session only" -ForegroundColor Green
    Write-Host "💡 Use -PersistEnvironmentVariable to make it permanent" -ForegroundColor Yellow
}

# Test the configuration
Write-Host "`n🧪 Testing XAI configuration..." -ForegroundColor Cyan
try {
    $testKey = $env:XAI_API_KEY
    if ($testKey -and $testKey.Length -gt 10) {
        Write-Host "✅ XAI API key is configured (length: $($testKey.Length))" -ForegroundColor Green
    }
    else {
        Write-Warning "XAI API key validation failed"
    }
}
catch {
    Write-Warning "Could not validate XAI API key: $_"
}

Write-Host "`n🎯 XAI API key configuration complete!" -ForegroundColor Green
Write-Host "📝 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Restart the Wiley Widget application" -ForegroundColor White
Write-Host "  2. Check the application logs for XAI configuration status" -ForegroundColor White
Write-Host "  3. Test the AI Assistant feature" -ForegroundColor White
