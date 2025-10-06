#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Removes unused Syncfusion DLLs from lib folder
.DESCRIPTION
    Safely removes DLLs not referenced in WileyWidget.csproj, with backup
#>

param(
    [switch]$DryRun,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Get all DLLs in lib folder
$libDlls = Get-ChildItem "lib\Syncfusion\*.dll"

# Get referenced DLLs from csproj
$csprojContent = Get-Content "WileyWidget.csproj" -Raw
$referencedDllNames = [regex]::Matches($csprojContent, 'lib\\Syncfusion\\(Syncfusion\.[^"]+\.dll)') |
    ForEach-Object { $_.Groups[1].Value } |
    Sort-Object -Unique

Write-Output "`n=== SYNCFUSION DLL CLEANUP ===`n"

# Find unused DLLs
$unusedDlls = $libDlls | Where-Object { $_.Name -notin $referencedDllNames }

Write-Output "Total DLLs: $($libDlls.Count)"
Write-Output "Referenced: $($referencedDllNames.Count)"
Write-Output "Unused: $($unusedDlls.Count)"

# Calculate size
$unusedSize = ($unusedDlls | Measure-Object -Property Length -Sum).Sum
$unusedSizeMB = [math]::Round($unusedSize / 1MB, 2)

Write-Output "`n💾 Space to be freed: $unusedSizeMB MB"

if ($unusedDlls.Count -eq 0) {
    Write-Output "`n✅ No unused DLLs found. Nothing to clean up."
    exit 0
}

if ($DryRun) {
    Write-Output "`n🔍 DRY RUN - Would remove these files:`n"
    $unusedDlls | ForEach-Object { Write-Output "  ❌ $($_.Name)" }
    Write-Output "`nRun without -DryRun to actually remove files."
    exit 0
}

if (-not $Force) {
    Write-Output "`n⚠️ WARNING: About to delete $($unusedDlls.Count) DLL files!`n"
    $response = Read-Host "Continue? (yes/no)"
    if ($response -ne 'yes') {
        Write-Output "Cancelled."
        exit 0
    }
}

# Create backup
$backupDir = "lib\Syncfusion_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
Write-Output "`n📦 Creating backup at: $backupDir"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

# Remove unused DLLs
$removed = 0
foreach ($dll in $unusedDlls) {
    try {
        Copy-Item $dll.FullName -Destination $backupDir
        Remove-Item $dll.FullName -Force
        Write-Output "  ✅ Removed: $($dll.Name)"
        $removed++
    }
    catch {
        Write-Output "  ❌ Failed to remove: $($dll.Name) - $($_.Exception.Message)"
    }
}

Write-Output "`n=== CLEANUP COMPLETE ===`n"
Write-Output "Removed: $removed files"
Write-Output "Freed: $unusedSizeMB MB"
Write-Output "Backup: $backupDir"
Write-Output "`n✅ Run 'git status' to see changes"
