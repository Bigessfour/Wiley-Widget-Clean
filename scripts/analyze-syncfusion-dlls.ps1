#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Analyzes Syncfusion DLL usage and NuGet package availability
.DESCRIPTION
    Compares DLLs in lib/Syncfusion with project references and checks NuGet availability
#>

param(
    [switch]$CheckNuGet,
    [switch]$ShowUnused
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Get all DLLs in lib folder
$libDlls = Get-ChildItem "lib\Syncfusion\*.dll" | Select-Object -ExpandProperty Name | Sort-Object

# Get referenced DLLs from csproj
$csprojContent = Get-Content "WileyWidget.csproj" -Raw
$referencedDlls = [regex]::Matches($csprojContent, 'lib\\Syncfusion\\(Syncfusion\.[^"]+\.dll)') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

# Get PackageReference entries
$nugetPackages = [regex]::Matches($csprojContent, 'PackageReference Include="(Syncfusion[^"]+)"') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

Write-Output "`n=== SYNCFUSION DLL ANALYSIS ===`n"
Write-Output "Total DLLs in lib/Syncfusion: $($libDlls.Count)"
Write-Output "Referenced in .csproj: $($referencedDlls.Count)"
Write-Output "NuGet packages referenced: $($nugetPackages.Count)"

Write-Output "`n=== REFERENCED DLLs (Used by project) ===`n"
$referencedDlls | ForEach-Object { Write-Output "  ✅ $_" }

Write-Output "`n=== NUGET PACKAGES (Already using NuGet) ===`n"
$nugetPackages | ForEach-Object { Write-Output "  📦 $_" }

if ($ShowUnused) {
    Write-Output "`n=== UNUSED DLLs (Can be removed) ===`n"
    $unusedDlls = $libDlls | Where-Object { $_ -notin $referencedDlls }
    Write-Output "Total unused: $($unusedDlls.Count)"
    $unusedDlls | ForEach-Object { Write-Output "  ❌ $_" }

    # Calculate size savings
    $unusedSize = 0
    foreach ($dll in $unusedDlls) {
        $file = Get-Item "lib\Syncfusion\$dll"
        $unusedSize += $file.Length
    }
    $unusedSizeMB = [math]::Round($unusedSize / 1MB, 2)
    Write-Output "`n💾 Potential space savings: $unusedSizeMB MB"
}

if ($CheckNuGet) {
    Write-Output "`n=== CHECKING NUGET AVAILABILITY ===`n"
    Write-Output "Checking which referenced DLLs have NuGet packages available..."

    $packagesMap = @{
        'Syncfusion.SfBusyIndicator.WPF.dll'       = 'Syncfusion.SfBusyIndicator.WPF'
        'Syncfusion.SfChart.WPF.dll'               = 'Syncfusion.SfChart.WPF'
        'Syncfusion.SfChat.Wpf.dll'                = 'Syncfusion.SfChat.WPF'
        'Syncfusion.SfGrid.WPF.dll'                = 'Syncfusion.SfGrid.WPF'
        'Syncfusion.SfGridCommon.WPF.dll'          = 'Syncfusion.SfGrid.WPF'
        'Syncfusion.SfGridConverter.WPF.dll'       = 'Syncfusion.SfGridConverter.WPF'
        'Syncfusion.SfInput.WPF.dll'               = 'Syncfusion.SfInput.WPF'
        'Syncfusion.SfProgressBar.WPF.dll'         = 'Syncfusion.SfProgressBar.WPF'
        'Syncfusion.SfShared.WPF.dll'              = 'Syncfusion.SfShared.WPF'
        'Syncfusion.SfSkinManager.WPF.dll'         = 'Syncfusion.SfSkinManager.WPF'
        'Syncfusion.SfSpreadsheet.WPF.dll'         = 'Syncfusion.SfSpreadsheet.WPF'
        'Syncfusion.Grid.WPF.dll'                  = 'Syncfusion.Grid.WPF'
        'Syncfusion.GridCommon.WPF.dll'            = 'Syncfusion.Grid.WPF'
        'Syncfusion.Shared.WPF.dll'                = 'Syncfusion.Shared.WPF'
        'Syncfusion.Tools.WPF.dll'                 = 'Syncfusion.Tools.WPF'
        'Syncfusion.Data.WPF.dll'                  = 'Syncfusion.Data.WPF'
        'Syncfusion.Themes.FluentDark.WPF.dll'     = 'Syncfusion.Themes.FluentDark.WPF'
        'Syncfusion.Themes.FluentLight.WPF.dll'    = 'Syncfusion.Themes.FluentLight.WPF'
        'Syncfusion.Themes.Windows11Light.WPF.dll' = 'Syncfusion.Themes.Windows11Light.WPF'
        'Syncfusion.Compression.Base.dll'          = 'Syncfusion.Compression.Base'
        'Syncfusion.Compression.NET.dll'           = 'Syncfusion.Compression.NET'
        'Syncfusion.DocIO.Base.dll'                = 'Syncfusion.DocIO.Base'
        'Syncfusion.DocIO.NET.dll'                 = 'Syncfusion.DocIO.NET'
        'Syncfusion.DocIORenderer.NET.dll'         = 'Syncfusion.DocIORenderer.NET'
        'Syncfusion.Pdf.NET.dll'                   = 'Syncfusion.Pdf.NET'
        'Syncfusion.XlsIO.Base.dll'                = 'Syncfusion.XlsIO.Base'
    }

    foreach ($dll in $referencedDlls) {
        $packageName = $packagesMap[$dll]
        if ($packageName) {
            Write-Output "  ✅ $dll → 📦 $packageName (NuGet available)"
        }
        else {
            Write-Output "  ⚠️ $dll → No known NuGet package"
        }
    }
}

Write-Output "`n=== SUMMARY ===`n"
Write-Output "Referenced DLLs: $($referencedDlls.Count)"
Write-Output "Unused DLLs: $($libDlls.Count - $referencedDlls.Count)"
Write-Output "Can migrate to NuGet: ~$($referencedDlls.Count - 5) DLLs (most have packages)"
Write-Output "`nRecommendation: Remove unused DLLs, migrate common controls to NuGet packages"
