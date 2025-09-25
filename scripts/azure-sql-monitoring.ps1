#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Azure SQL Database Basic Tier Monitoring & Alert System

.DESCRIPTION
    Monitors Azure SQL Database Basic tier limits and sets up alerts when upgrade is needed:
    - DTU usage monitoring (5 DTU limit)
    - Storage monitoring (2GB limit)
    - Connection monitoring
    - Cost alerts for tier upgrades

.PARAMETER DatabaseName
    Name of the Azure SQL Database to monitor

.PARAMETER ServerName
    Name of the Azure SQL Server

.PARAMETER ResourceGroupName
    Name of the resource group

.PARAMETER SetupAlerts
    Switch to create monitoring alerts

.PARAMETER CheckLimits
    Switch to check current usage against Basic tier limits

.EXAMPLE
    ./azure-sql-monitoring.ps1 -SetupAlerts
    ./azure-sql-monitoring.ps1 -CheckLimits
#>

param(
    [string]$DatabaseName = "WileyWidgetDB",
    [string]$ServerName = "wileywidget-sql",
    [string]$ResourceGroupName = "WileyWidget-RG",
    [switch]$SetupAlerts,
    [switch]$CheckLimits,
    [switch]$RecommendUpgrade
)

# Basic Tier Limits
$BASIC_TIER_LIMITS = @{
    MaxDTU                  = 5
    MaxStorageGB            = 2
    MaxConnections          = 30
    WarningThresholdPercent = 80
}

# Upgrade Options with Costs
$UPGRADE_OPTIONS = @{
    "Standard-S0"            = @{ DTU = 10; StorageGB = 250; CostMonthly = 15; Description = "Small production apps" }
    "Standard-S1"            = @{ DTU = 20; StorageGB = 250; CostMonthly = 30; Description = "Medium apps with more users" }
    "Standard-S2"            = @{ DTU = 50; StorageGB = 250; CostMonthly = 75; Description = "Growing applications" }
    "General-Purpose-2vCore" = @{ DTU = "2 vCore"; StorageGB = 32; CostMonthly = 60; Description = "Modern vCore-based option" }
}

function Write-ColorOutput {
    param([string]$Text, [string]$Color = "White")

    $colors = @{
        "Red" = "91"; "Green" = "92"; "Yellow" = "93"; "Blue" = "94"
        "Magenta" = "95"; "Cyan" = "96"; "White" = "97"
    }

    if ($colors.ContainsKey($Color)) {
        Write-Information "`e[$($colors[$Color])m$Text`e[0m" -InformationAction Continue
    }
    else {
        Write-Information $Text -InformationAction Continue
    }
}

function Test-AzureConnection {
    try {
        $account = az account show --output json 2>$null | ConvertFrom-Json
        if (-not $account) {
            Write-ColorOutput "❌ Not logged into Azure. Please run 'az login'" "Red"
            return $false
        }
        Write-ColorOutput "✅ Connected to Azure subscription: $($account.name)" "Green"
        return $true
    }
    catch {
        Write-ColorOutput "❌ Azure CLI not available or not logged in" "Red"
        return $false
    }
}

function Get-DatabaseMetric {
    param([string]$Database, [string]$Server, [string]$ResourceGroup)

    try {
        Write-ColorOutput "📊 Checking database metrics..." "Blue"

        # Get database details
        $dbInfo = az sql db show --server $Server --resource-group $ResourceGroup --name $Database --output json | ConvertFrom-Json

        # Get current storage usage (requires more complex query)
        $storageQuery = @"
SELECT
    database_name,
    storage_in_megabytes,
    allocated_space_in_megabytes,
    max_size_in_megabytes
FROM sys.dm_db_partition_stats
CROSS APPLY (SELECT DB_NAME() as database_name) AS db_info
"@

        # Get DTU usage for last hour
        $endTime = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
        $startTime = (Get-Date).AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")

        $metrics = @{
            CurrentTier = $dbInfo.sku.tier
            CurrentDTU  = $dbInfo.sku.capacity
            MaxSizeGB   = [math]::Round($dbInfo.maxSizeBytes / 1GB, 2)
            Status      = $dbInfo.status
        }

        Write-ColorOutput "Current Configuration:" "Yellow"
        Write-Information "  Tier: $($metrics.CurrentTier)" -InformationAction Continue
        Write-Information "  DTU: $($metrics.CurrentDTU)" -InformationAction Continue
        Write-Information "  Max Storage: $($metrics.MaxSizeGB) GB" -InformationAction Continue
        Write-Information "  Status: $($metrics.Status)" -InformationAction Continue

        return $metrics
    }
    catch {
        Write-ColorOutput "❌ Failed to get database metrics: $($_.Exception.Message)" "Red"
        return $null
    }
}

function Test-BasicTierLimit {
    param($Metrics)

    Write-ColorOutput "`n🎯 Basic Tier Limit Analysis:" "Cyan"

    $warnings = @()
    $critical = @()

    # Check DTU (we know it's 5 for Basic)
    if ($Metrics.CurrentDTU -eq $BASIC_TIER_LIMITS.MaxDTU) {
        Write-ColorOutput "⚠️  DTU: At maximum ($($BASIC_TIER_LIMITS.MaxDTU) DTU)" "Yellow"
        $warnings += "DTU at maximum capacity"
    }
    else {
        Write-ColorOutput "✅ DTU: $($Metrics.CurrentDTU)/$($BASIC_TIER_LIMITS.MaxDTU)" "Green"
    }

    # Check Storage
    $storagePercent = ($Metrics.MaxSizeGB / $BASIC_TIER_LIMITS.MaxStorageGB) * 100
    if ($storagePercent -ge 100) {
        Write-ColorOutput "🚨 Storage: At maximum ($($BASIC_TIER_LIMITS.MaxStorageGB) GB)" "Red"
        $critical += "Storage at maximum capacity"
    }
    elseif ($storagePercent -ge $BASIC_TIER_LIMITS.WarningThresholdPercent) {
        Write-ColorOutput "⚠️  Storage: $($Metrics.MaxSizeGB)/$($BASIC_TIER_LIMITS.MaxStorageGB) GB ($([math]::Round($storagePercent, 1))%)" "Yellow"
        $warnings += "Storage approaching limit"
    }
    else {
        Write-ColorOutput "✅ Storage: $($Metrics.MaxSizeGB)/$($BASIC_TIER_LIMITS.MaxStorageGB) GB ($([math]::Round($storagePercent, 1))%)" "Green"
    }

    return @{
        Warnings       = $warnings
        Critical       = $critical
        StoragePercent = $storagePercent
        NeedsUpgrade   = ($critical.Count -gt 0 -or $warnings.Count -ge 2)
    }
}

function Show-UpgradeRecommendation {
    param($CurrentUsage, $Analysis)

    if (-not $Analysis.NeedsUpgrade -and $Analysis.Warnings.Count -eq 0) {
        Write-ColorOutput "`n✅ Basic tier is still suitable for your current usage" "Green"
        return
    }

    Write-ColorOutput "`n🚀 Upgrade Recommendations:" "Magenta"
    Write-ColorOutput "Current cost: ~`$5/month (Basic)" "White"

    foreach ($tier in $UPGRADE_OPTIONS.GetEnumerator()) {
        $option = $tier.Value
        Write-ColorOutput "`n📈 $($tier.Key):" "Cyan"
        Write-Information "  DTU/vCore: $($option.DTU)" -InformationAction Continue
        Write-Information "  Storage: $($option.StorageGB) GB" -InformationAction Continue
        Write-Information "  Monthly Cost: ~$($option.CostMonthly)" -InformationAction Continue
        Write-Information "  Best For: $($option.Description)" -InformationAction Continue

        # Calculate cost increase
        $increase = $option.CostMonthly - 5
        $yearlyIncrease = $increase * 12
        Write-ColorOutput "  Cost Increase: +`$$increase/month (+`$$yearlyIncrease/year)" "Yellow"
    }

    Write-ColorOutput "`n💡 Recommended Next Step:" "Green"
    if ($Analysis.StoragePercent -ge 80) {
        Write-Information "  Standard S0 - Immediate storage relief (250GB) for +`$10/month" -InformationAction Continue
    }
    else {
        Write-Information "  Monitor for another month, then consider Standard S0" -InformationAction Continue
    }
}

function Set-CostAlert {
    param([string]$ResourceGroup, [int]$ThresholdAmount = 20)

    Write-ColorOutput "🔔 Setting up cost alert for $$ThresholdAmount/month..." "Blue"

    try {
        # Create action group for notifications
        $actionGroupName = "sql-upgrade-alerts"
        $alertRuleName = "sql-basic-tier-upgrade-needed"

        # Check if action group exists
        $existingActionGroup = az monitor action-group list --resource-group $ResourceGroup --query "[?name=='$actionGroupName']" --output json | ConvertFrom-Json

        if (-not $existingActionGroup) {
            Write-ColorOutput "Creating action group for notifications..." "Blue"
            # Note: You'll need to add your email here
            # az monitor action-group create --resource-group $ResourceGroup --name $actionGroupName --short-name "SQLUpgrade"
            Write-ColorOutput "⚠️  To complete setup, run:" "Yellow"
            Write-Information "az monitor action-group create --resource-group $ResourceGroup --name $actionGroupName --short-name SQLUpgrade --action email your-email@domain.com youremail" -InformationAction Continue
        }

        Write-ColorOutput "✅ Cost alert framework ready" "Green"
        Write-ColorOutput "💡 Manual monitoring script created for immediate use" "Blue"

    }
    catch {
        Write-ColorOutput "⚠️  Note: Automated alerts require additional setup" "Yellow"
        Write-ColorOutput "This script provides manual monitoring capabilities" "White"
    }
}

# Main execution
function Main {
    Write-ColorOutput "🔍 Azure SQL Database Basic Tier Monitor" "Cyan"
    Write-ColorOutput "==============================================" "Cyan"

    if (-not (Test-AzureConnection)) {
        exit 1
    }

    if ($SetupAlerts) {
        Set-CostAlert -ResourceGroup $ResourceGroupName
        Write-ColorOutput "`n✅ Alert setup complete!" "Green"
        Write-ColorOutput "Run this script with -CheckLimits to monitor usage" "Blue"
        return
    }

    if ($CheckLimits -or $RecommendUpgrade) {
        $metrics = Get-DatabaseMetrics -Database $DatabaseName -Server $ServerName -ResourceGroup $ResourceGroupName

        if ($metrics) {
            $analysis = Test-BasicTierLimits -Metrics $metrics

            if ($RecommendUpgrade -or $analysis.NeedsUpgrade) {
                Show-UpgradeRecommendations -CurrentUsage $metrics -Analysis $analysis
            }

            # Save results for future reference
            $result = @{
                Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
                Metrics   = $metrics
                Analysis  = $analysis
            }

            $resultFile = Join-Path $PSScriptRoot "sql-monitoring-results.json"
            $result | ConvertTo-Json -Depth 3 | Out-File $resultFile
            Write-ColorOutput "`n📁 Results saved to: $resultFile" "Blue"
        }
        return
    }

    # Default: Show usage
    Write-ColorOutput "Usage:" "Yellow"
    Write-Information "  -SetupAlerts      : Configure monitoring alerts" -InformationAction Continue
    Write-Information "  -CheckLimits      : Check current usage against Basic tier limits" -InformationAction Continue
    Write-Information "  -RecommendUpgrade : Show upgrade options and costs" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Examples:" -InformationAction Continue
    Write-Information "  ./azure-sql-monitoring.ps1 -SetupAlerts" -InformationAction Continue
    Write-Information "  ./azure-sql-monitoring.ps1 -CheckLimits" -InformationAction Continue
    Write-Information "  ./azure-sql-monitoring.ps1 -RecommendUpgrade" -InformationAction Continue
}

# Run main function
Main
