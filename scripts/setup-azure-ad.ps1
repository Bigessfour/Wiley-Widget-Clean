#!/usr/bin/env pwsh
# Azure AD Configuration Setup Script
# This script helps configure Azure AD environment variables for WileyWidget

param(
    [Parameter(Mandatory = $false)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$TenantId,

    [switch]$Test,
    [switch]$Validate
)

function Test-GuidFormat {
    param([string]$Value)
    return [Guid]::TryParse($Value, [ref][Guid]::Empty)
}

function Show-Instruction {
    Write-Host "Azure AD Configuration Setup for WileyWidget" -ForegroundColor Cyan
    Write-Host "===============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To get your Azure AD GUIDs:" -ForegroundColor Yellow
    Write-Host "1. Go to Azure Portal: https://portal.azure.com"
    Write-Host "2. Navigate to Microsoft Entra ID > App registrations"
    Write-Host "3. Select your app or create a new registration"
    Write-Host "4. Copy the following from the Overview page:"
    Write-Host "   - Application (client) ID"
    Write-Host "   - Directory (tenant) ID"
    Write-Host ""
    Write-Host "Example GUID format: cb097857-10d5-410b-8e09-6073de3ab035" -ForegroundColor Green
    Write-Host ""
}

function Set-AzureAdEnvironment {
    param([string]$ClientId, [string]$TenantId)

    if (-not (Test-GuidFormat $ClientId)) {
        Write-Error "Invalid Client ID format. Must be a valid GUID."
        return $false
    }

    if (-not (Test-GuidFormat $TenantId)) {
        Write-Error "Invalid Tenant ID format. Must be a valid GUID."
        return $false
    }

    # Set environment variables for current session
    $env:AZURE_AD_CLIENT_ID = $ClientId
    $env:AZURE_AD_TENANT_ID = $TenantId

    Write-Host "✅ Environment variables set for current session:" -ForegroundColor Green
    Write-Host "   AZURE_AD_CLIENT_ID = $ClientId"
    Write-Host "   AZURE_AD_TENANT_ID = $TenantId"
    Write-Host ""

    # Update .env file if it exists
    if (Test-Path ".env") {
        $envContent = Get-Content ".env"
        $updated = $false

        for ($i = 0; $i -lt $envContent.Length; $i++) {
            if ($envContent[$i] -match "^AZURE_AD_CLIENT_ID=") {
                $envContent[$i] = "AZURE_AD_CLIENT_ID=$ClientId"
                $updated = $true
            }
            if ($envContent[$i] -match "^AZURE_AD_TENANT_ID=") {
                $envContent[$i] = "AZURE_AD_TENANT_ID=$TenantId"
                $updated = $true
            }
        }

        if ($updated) {
            $envContent | Set-Content ".env"
            Write-Host "✅ Updated .env file with new values" -ForegroundColor Green
        }
    }

    return $true
}

function Test-AzureAdConfiguration {
    Write-Host "Testing Azure AD Configuration..." -ForegroundColor Cyan

    # Check environment variables
    $clientId = $env:AZURE_AD_CLIENT_ID
    $tenantId = $env:AZURE_AD_TENANT_ID

    if (-not $clientId) {
        Write-Warning "AZURE_AD_CLIENT_ID environment variable not set"
        return $false
    }

    if (-not $tenantId) {
        Write-Warning "AZURE_AD_TENANT_ID environment variable not set"
        return $false
    }

    # Validate GUID format
    if (-not (Test-GuidFormat $clientId)) {
        Write-Error "AZURE_AD_CLIENT_ID is not a valid GUID: $clientId"
        return $false
    }

    if (-not (Test-GuidFormat $tenantId)) {
        Write-Error "AZURE_AD_TENANT_ID is not a valid GUID: $tenantId"
        return $false
    }

    Write-Host "✅ Environment variables are properly set and valid:" -ForegroundColor Green
    Write-Host "   AZURE_AD_CLIENT_ID = $clientId"
    Write-Host "   AZURE_AD_TENANT_ID = $tenantId"

    # Test Azure CLI login status
    try {
        $azAccount = az account show 2>$null | ConvertFrom-Json
        if ($azAccount) {
            Write-Host "✅ Azure CLI is logged in as: $($azAccount.user.name)" -ForegroundColor Green
        }
        else {
            Write-Warning "Azure CLI is not logged in. Run 'az login' to authenticate."
        }
    }
    catch {
        Write-Warning "Azure CLI not available or not logged in. Run 'az login' to authenticate."
    }

    return $true
}

# Main script logic
if ($Test) {
    Test-AzureAdConfiguration
}
elseif ($Validate) {
    Test-AzureAdConfiguration
    Write-Host ""
    Write-Host "To test the application, run:" -ForegroundColor Yellow
    Write-Host "dotnet run --project WileyWidget.csproj" -ForegroundColor White
}
elseif ($ClientId -and $TenantId) {
    if (Set-AzureAdEnvironment -ClientId $ClientId -TenantId $TenantId) {
        Write-Host "Configuration complete! Run with -Test to validate." -ForegroundColor Green
    }
}
else {
    Show-Instructions
    Write-Host "Usage Examples:" -ForegroundColor Yellow
    Write-Host "  .\scripts\setup-azure-ad.ps1 -ClientId 'your-guid' -TenantId 'your-guid'"
    Write-Host "  .\scripts\setup-azure-ad.ps1 -Test"
    Write-Host "  .\scripts\setup-azure-ad.ps1 -Validate"
    Write-Host ""
}
