#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Locks in Azure performance enhancements for Wiley Widget development
.DESCRIPTION
    This script configures Azure CLI and PowerShell for optimal performance
    and sets up automatic caching and authentication.
#>

param(
    [switch]$Force,
    [switch]$SkipAuth
)

Write-Host "🔒 Locking Azure Performance Enhancements..." -ForegroundColor Green

# Ensure Azure CLI is optimized
Write-Host "⚙️  Configuring Azure CLI optimizations..." -ForegroundColor Yellow
try {
    az config set core.collect_telemetry=false
    az account set --subscription 89c2076a-8c6f-41fe-b03c-850d46a57abf
    az config set defaults.location=eastus2
    Write-Host "✅ Azure CLI optimized" -ForegroundColor Green
}
catch {
    Write-Warning "Azure CLI optimization failed: $_"
}

# Authenticate Azure PowerShell (skip if requested)
if (-not $SkipAuth) {
    Write-Host "🔐 Authenticating Azure PowerShell..." -ForegroundColor Yellow
    try {
        $tenantId = "cb097857-10d5-410b-8e09-6073de3ab035"
        Connect-AzAccount -TenantId $tenantId -ErrorAction Stop
        Write-Host "✅ Azure PowerShell authenticated" -ForegroundColor Green
    }
    catch {
        Write-Warning "Azure PowerShell authentication failed: $_"
        Write-Host "Manual authentication required. Run: Connect-AzAccount -TenantId $tenantId" -ForegroundColor Yellow
    }
}

# Set up caching
Write-Host "💾 Setting up performance caching..." -ForegroundColor Yellow
try {
    # Cache resource groups
    $script:rgCache = Get-AzResourceGroup -ErrorAction SilentlyContinue
    if ($rgCache) {
        Write-Host "✅ Resource groups cached ($($rgCache.Count) groups)" -ForegroundColor Green
    }

    # Cache subscriptions
    $script:subCache = Get-AzSubscription -ErrorAction SilentlyContinue
    if ($subCache) {
        Write-Host "✅ Subscriptions cached ($($subCache.Count) subscriptions)" -ForegroundColor Green
    }

    # Export cache for use in other sessions
    $cacheFile = "$PSScriptRoot\azure-cache.json"
    @{
        ResourceGroups = $rgCache
        Subscriptions  = $subCache
        Timestamp      = Get-Date
    } | ConvertTo-Json | Out-File $cacheFile -Force
    Write-Host "💾 Cache saved to $cacheFile" -ForegroundColor Green

}
catch {
    Write-Warning "Caching setup failed: $_"
}

# Create performance monitoring function
Write-Host "📊 Setting up performance monitoring..." -ForegroundColor Yellow
function Measure-AzureCommand {
    param([scriptblock]$Command)
    $result = Measure-Command $Command
    Write-Host "⏱️  Command completed in $($result.TotalSeconds) seconds" -ForegroundColor Cyan
    return $result
}

# Export functions to global scope
New-Item -Path function: -Name global:Measure-AzureCommand -Value ${function:Measure-AzureCommand} -Force | Out-Null

Write-Host "🎯 Performance enhancements locked in!" -ForegroundColor Green
Write-Host "💡 Use Measure-AzureCommand { Get-AzResourceGroup } to monitor performance" -ForegroundColor Cyan
Write-Host "🔄 Run this script on new sessions or add to PowerShell profile for auto-execution" -ForegroundColor Yellow
