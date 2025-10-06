#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Checks NuGet availability for Syncfusion packages
.DESCRIPTION
    Queries nuget.org to verify which Syncfusion packages are actually available
#>

param(
    [string[]]$PackageNames
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

if (-not $PackageNames) {
    # Check all referenced packages
    $PackageNames = @(
        'Syncfusion.Compression.Base',
        'Syncfusion.Compression.NET',
        'Syncfusion.Data.WPF',
        'Syncfusion.DocIO.Base',
        'Syncfusion.DocIO.NET',
        'Syncfusion.DocIORenderer.NET',
        'Syncfusion.Grid.WPF',
        'Syncfusion.Pdf.NET',
        'Syncfusion.SfBusyIndicator.WPF',
        'Syncfusion.SfChart.WPF',
        'Syncfusion.SfChat.WPF',
        'Syncfusion.SfGrid.WPF',
        'Syncfusion.SfGridConverter.WPF',
        'Syncfusion.SfInput.WPF',
        'Syncfusion.SfProgressBar.WPF',
        'Syncfusion.SfShared.WPF',
        'Syncfusion.SfSkinManager.WPF',
        'Syncfusion.SfSpreadsheet.WPF',
        'Syncfusion.Shared.WPF',
        'Syncfusion.Themes.FluentDark.WPF',
        'Syncfusion.Themes.FluentLight.WPF',
        'Syncfusion.Themes.Windows11Light.WPF',
        'Syncfusion.Tools.WPF',
        'Syncfusion.XlsIO.Base'
    )
}

Write-Output "`n=== NUGET PACKAGE AVAILABILITY CHECK ===`n"
Write-Output "Checking $($PackageNames.Count) packages on nuget.org...`n"

$available = @()
$unavailable = @()

foreach ($package in $PackageNames) {
    Write-Output "Checking: $package..." -NoNewline

    try {
        $response = Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/$($package.ToLower())/index.json" -TimeoutSec 5
        if ($response.versions -and $response.versions.Count -gt 0) {
            $latestVersion = $response.versions[-1]
            Write-Output " ✅ Available (latest: $latestVersion)"
            $available += [PSCustomObject]@{
                Package       = $package
                LatestVersion = $latestVersion
                Available     = $true
            }
        }
    }
    catch {
        Write-Output " ❌ Not found on nuget.org"
        $unavailable += $package
    }
}

Write-Output "`n=== SUMMARY ===`n"
Write-Output "✅ Available on NuGet: $($available.Count)"
Write-Output "❌ Not available: $($unavailable.Count)"

if ($unavailable.Count -gt 0) {
    Write-Output "`n❌ These packages are NOT available on nuget.org:"
    $unavailable | ForEach-Object { Write-Output "  - $_" }
    Write-Output "`n⚠️ These MUST stay in lib/ folder"
}

if ($available.Count -gt 0) {
    Write-Output "`n✅ These packages CAN be migrated to NuGet:"
    $available | ForEach-Object {
        Write-Output "  - $($_.Package) @ $($_.LatestVersion)"
    }
}

# Export results
$available | Export-Csv -Path "nuget-available-packages.csv" -NoTypeInformation
Write-Output "`n📄 Results saved to: nuget-available-packages.csv"
