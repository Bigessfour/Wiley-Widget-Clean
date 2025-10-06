# Setup Environment Variables for Wiley Widget
# Fixes JSON Gremlins by ensuring all required variables are set

param(
    [switch]$SetDefaults,
    [switch]$ShowCurrent,
    [switch]$ValidateRequired
)

$RequiredVars = @{
    'AZURE_SQL_CONNECTION_STRING' = 'Server=tcp://{server}.database.windows.net,1433;Initial Catalog={database};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;'
    'AZURE_SQL_SERVER'            = 'your-azure-sql-server'
    'AZURE_SQL_DATABASE'          = 'WileyWidgetDb'
    'EMAIL_FROM_ADDRESS'          = 'errors@wileywidget.local'
    'EMAIL_TO_ADDRESS'            = 'admin@wileywidget.local'
    'EMAIL_SMTP_SERVER'           = 'smtp.gmail.com'
    'EMAIL_SMTP_PORT'             = '587'
    'EMAIL_USERNAME'              = 'your-email@domain.com'
    'EMAIL_PASSWORD'              = 'your-app-password'
    'QBO_CLIENT_ID'               = 'your-quickbooks-client-id'
    'QBO_CLIENT_SECRET'           = 'your-quickbooks-client-secret'
    'QBO_REDIRECT_URI'            = 'https://localhost:5001/callback'
    'QBO_ENVIRONMENT'             = 'sandbox'
    'AZURE_KEY_VAULT_URL'         = 'https://wiley-widget-secrets.vault.azure.net/'
}

if ($ShowCurrent) {
    Write-Information "Current Environment Variables:" -InformationAction Continue
    foreach ($var in $RequiredVars.Keys) {
        $value = [Environment]::GetEnvironmentVariable($var)
        if ($value) {
            Write-Information "✅ $var = $([string]::new('*', [Math]::Min($value.Length, 20)))" -InformationAction Continue
        }
        else {
            Write-Information "❌ $var = [NOT SET]" -InformationAction Continue
        }
    }
    return
}

if ($ValidateRequired) {
    Write-Information "Validating Required Environment Variables:" -InformationAction Continue
    $missing = @()
    foreach ($var in $RequiredVars.Keys) {
        $value = [Environment]::GetEnvironmentVariable($var)
        if (-not $value -or $value.Contains('your-') -or $value.Contains('{')) {
            $missing += $var
            Write-Warning "$var is missing or contains placeholder values"
        }
        else {
            Write-Information "✅ $var is properly set" -InformationAction Continue
        }
    }

    if ($missing.Count -gt 0) {
        Write-Error "Missing or invalid environment variables: $($missing -join ', ')"
        Write-Information "Run with -SetDefaults to create template values" -InformationAction Continue
        return $false
    }
    else {
        Write-Information "✅ All required environment variables are set!" -InformationAction Continue
        return $true
    }
}

if ($SetDefaults) {
    Write-Information "Setting default/template environment variables..." -InformationAction Continue
    foreach ($var in $RequiredVars.Keys) {
        $current = [Environment]::GetEnvironmentVariable($var)
        if (-not $current) {
            [Environment]::SetEnvironmentVariable($var, $RequiredVars[$var], 'User')
            Write-Information "Set $var to template value" -InformationAction Continue
        }
        else {
            Write-Information "Skipped $var (already set)" -InformationAction Continue
        }
    }
    Write-Warning "Template values set! Please update them with your actual values before running the application."
    return
}

# Default behavior - show usage
Write-Information @"
🔧 Environment Variable Setup Script

Usage:
  -ShowCurrent     : Display current environment variable status
  -ValidateRequired: Check if all required variables are properly set
  -SetDefaults     : Set template values for missing variables

Examples:
  .\setup-environment-variables.ps1 -ShowCurrent
  .\setup-environment-variables.ps1 -ValidateRequired
  .\setup-environment-variables.ps1 -SetDefaults

After setting defaults, edit the variables with your actual values:
  `$env:AZURE_SQL_CONNECTION_STRING = "Server=tcp://your-server.database.windows.net,1433;..."
  `$env:AZURE_SQL_SERVER = "your-actual-server-name"
"@ -InformationAction Continue
