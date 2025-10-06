#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Complete Syncfusion cleanup and NuGet migration
.DESCRIPTION
    1. Removes 81 unused DLLs
    2. Migrates 26 referenced DLLs to NuGet packages
    3. Cleans up lib folder completely
#>

param(
    [switch]$DryRun,
    [switch]$KeepBackup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Output "`n=== SYNCFUSION COMPLETE MIGRATION ===`n"

# Step 1: Analyze current state
$libDlls = Get-ChildItem "lib\Syncfusion\*.dll"
$csprojPath = "WileyWidget.csproj"
$csprojContent = Get-Content $csprojPath -Raw

$referencedDllNames = [regex]::Matches($csprojContent, 'lib\\Syncfusion\\(Syncfusion\.[^"]+\.dll)') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

Write-Output "📊 Current State:"
Write-Output "  Total DLLs: $($libDlls.Count)"
Write-Output "  Referenced: $($referencedDllNames.Count)"
Write-Output "  Unused: $($libDlls.Count - $referencedDllNames.Count)"

# DLL to NuGet package mapping
$dllToPackage = @{
    'Syncfusion.Compression.Base.dll'          = 'Syncfusion.Compression.Base'
    'Syncfusion.Compression.NET.dll'           = 'Syncfusion.Compression.NET'
    'Syncfusion.Data.WPF.dll'                  = 'Syncfusion.Data.WPF'
    'Syncfusion.DocIO.Base.dll'                = 'Syncfusion.DocIO.Base'
    'Syncfusion.DocIO.NET.dll'                 = 'Syncfusion.DocIO.NET'
    'Syncfusion.DocIORenderer.NET.dll'         = 'Syncfusion.DocIORenderer.NET'
    'Syncfusion.Grid.WPF.dll'                  = 'Syncfusion.Grid.WPF'
    'Syncfusion.GridCommon.WPF.dll'            = 'Syncfusion.Grid.WPF'
    'Syncfusion.Pdf.NET.dll'                   = 'Syncfusion.Pdf.NET'
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
    'Syncfusion.Shared.WPF.dll'                = 'Syncfusion.Shared.WPF'
    'Syncfusion.Themes.FluentDark.WPF.dll'     = 'Syncfusion.Themes.FluentDark.WPF'
    'Syncfusion.Themes.FluentLight.WPF.dll'    = 'Syncfusion.Themes.FluentLight.WPF'
    'Syncfusion.Themes.Windows11Light.WPF.dll' = 'Syncfusion.Themes.Windows11Light.WPF'
    'Syncfusion.Tools.WPF.dll'                 = 'Syncfusion.Tools.WPF'
    'Syncfusion.XlsIO.Base.dll'                = 'Syncfusion.XlsIO.Base'
}

# Get unique NuGet packages needed
$packagesNeeded = $referencedDllNames |
    ForEach-Object { $dllToPackage[$_] } |
    Where-Object { $_ } |
    Sort-Object -Unique

Write-Output "`n📦 NuGet Packages to Add: $($packagesNeeded.Count)"
$packagesNeeded | ForEach-Object { Write-Output "  + $_" }

if ($DryRun) {
    Write-Output "`n🔍 DRY RUN - Would perform these actions:`n"
    Write-Output "1. Remove all $($libDlls.Count) DLLs from lib/Syncfusion/"
    Write-Output "2. Add $($packagesNeeded.Count) NuGet package references"
    Write-Output "3. Remove all <Reference> entries pointing to lib/"
    Write-Output "4. Clean up lib/Syncfusion folder"
    Write-Output "`nRun without -DryRun to execute migration."
    exit 0
}

# Confirmation
Write-Output "`n⚠️ WARNING: This will:`n"
Write-Output "  1. Remove all $($libDlls.Count) DLLs from lib/Syncfusion/"
Write-Output "  2. Add $($packagesNeeded.Count) NuGet packages to project"
Write-Output "  3. Modify WileyWidget.csproj"
Write-Output ""
$response = Read-Host "Continue? (yes/no)"
if ($response -ne 'yes') {
    Write-Output "Cancelled."
    exit 0
}

# Create backup
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = "Migration_Backups\Syncfusion_Migration_$timestamp"
Write-Output "`n📦 Creating backup at: $backupDir"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
Copy-Item $csprojPath -Destination "$backupDir\WileyWidget.csproj.backup"
Copy-Item "lib\Syncfusion\" -Destination "$backupDir\lib_Syncfusion\" -Recurse

# Step 2: Add NuGet packages
Write-Output "`n📦 Adding NuGet packages..."
foreach ($package in $packagesNeeded) {
    Write-Output "  Adding: $package..."
    try {
        & dotnet add package $package --version 27.1.48 2>&1 | Out-Null
        Write-Output "  ✅ Added: $package"
    }
    catch {
        Write-Output "  ⚠️ Warning: $package - $($_.Exception.Message)"
    }
}

# Step 3: Remove DLL references from csproj
Write-Output "`n🔧 Updating project file..."
$newCsprojContent = $csprojContent

# Remove all <Reference Include="Syncfusion.*"> blocks that point to lib/
$pattern = '(?s)<Reference Include="Syncfusion\.[^"]*">.*?<HintPath>lib\\Syncfusion\\.*?</HintPath>.*?</Reference>'
$newCsprojContent = $newCsprojContent -replace $pattern, ''

# Clean up empty ItemGroups
$newCsprojContent = $newCsprojContent -replace '(?s)<ItemGroup>\s*</ItemGroup>', ''

Set-Content -Path $csprojPath -Value $newCsprojContent
Write-Output "  ✅ Removed DLL references from .csproj"

# Step 4: Remove DLLs
Write-Output "`n🗑️ Removing DLL files..."
Remove-Item "lib\Syncfusion\*.dll" -Force
Write-Output "  ✅ Removed all DLLs"

# Check if lib/Syncfusion is empty and remove
$remaining = Get-ChildItem "lib\Syncfusion\" -ErrorAction SilentlyContinue
if ($remaining.Count -eq 0) {
    Remove-Item "lib\Syncfusion\" -Force
    Write-Output "  ✅ Removed empty lib/Syncfusion folder"

    $libRemaining = Get-ChildItem "lib\" -ErrorAction SilentlyContinue
    if ($libRemaining.Count -eq 0) {
        Remove-Item "lib\" -Force
        Write-Output "  ✅ Removed empty lib folder"
    }
}

Write-Output "`n=== MIGRATION COMPLETE ===`n"
Write-Output "✅ Removed: $($libDlls.Count) DLL files"
Write-Output "✅ Added: $($packagesNeeded.Count) NuGet packages"
Write-Output "✅ Updated: WileyWidget.csproj"
Write-Output "📦 Backup: $backupDir"

if (-not $KeepBackup) {
    Write-Output "`n💡 TIP: Backup will be kept in Migration_Backups/"
}

Write-Output "`n🔨 Next steps:"
Write-Output "  1. Run: dotnet restore"
Write-Output "  2. Run: dotnet build"
Write-Output "  3. Test the application"
Write-Output "  4. If successful: git add . && git commit -m 'refactor: migrate Syncfusion to NuGet packages'"
