#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Comprehensive root directory cleanup and reorganization
.DESCRIPTION
    Identifies and moves/removes temporary, diagnostic, and misplaced files from root
    Preserves important files like budget data (.xls), configs, and documentation
#>

param(
    [switch]$DryRun,
    [switch]$Verbose
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Output "`n=== ROOT DIRECTORY CLEANUP ===`n"

# Files to KEEP in root (critical for project)
$keepInRoot = @(
    'WileyWidget.csproj',
    'WileyWidget.sln',
    'README.md',
    '.gitignore',
    '.editorconfig'
)

# Files to MOVE to appropriate folders
$fileMoves = @{
    # Diagnostic scripts → scripts/diagnostics/
    'powershell_diag.ps1'                                 = 'scripts/diagnostics/'
    'python_diag.ps1'                                     = 'scripts/diagnostics/'
    'python_diag.py'                                      = 'scripts/diagnostics/'
    'test-connection.ps1'                                 = 'scripts/diagnostics/'
    'test_ef_connection.py'                               = 'scripts/diagnostics/'

    # Test outputs → tests/output/
    'test-output.txt'                                     = 'tests/output/'
    'syncfusion-nuget-output.txt'                         = 'tests/output/'

    # Config files → config/
    'appsettings.Development.json'                        = 'config/'
    'appsettings.json'                                    = 'config/'
    'appsettings.Production.json'                         = 'config/'
    'appsettings.Test.json'                               = 'config/'

    # Documentation → docs/
    'MUNICIPAL_BUDGET_INTEGRATION_IMPLEMENTATION_PLAN.md' = 'docs/'

    # Data/reference files → data/reference/
    'syncfusion-nuget-check.json'                         = 'data/reference/'
}

# Files to DELETE (temporary, obsolete)
$filesToDelete = @(
    'temp_app_update.json',
    'ExcelAnalyzer.cs'  # Likely temp file
)

# Analyze current state
Write-Output "📊 Current State Analysis:`n"

$allFiles = Get-ChildItem -File | Where-Object { $_.Name -notin $keepInRoot }
Write-Output "  Total files in root: $($allFiles.Count)"
Write-Output "  Files to keep in root: $($keepInRoot.Count)"
Write-Output "  Files to relocate: $($fileMoves.Count)"
Write-Output "  Files to delete: $($filesToDelete.Count)"

if ($DryRun) {
    Write-Output "`n🔍 DRY RUN - Would perform these actions:`n"

    Write-Output "📁 FILES TO MOVE:"
    foreach ($file in $fileMoves.Keys | Sort-Object) {
        $dest = $fileMoves[$file]
        if (Test-Path $file) {
            Write-Output "  $file → $dest"
        }
    }

    Write-Output "`n🗑️ FILES TO DELETE:"
    foreach ($file in $filesToDelete) {
        if (Test-Path $file) {
            Write-Output "  ❌ $file"
        }
    }

    Write-Output "`n✅ FILES TO KEEP IN ROOT:"
    foreach ($file in $keepInRoot) {
        if (Test-Path $file) {
            Write-Output "  ✓ $file"
        }
    }

    Write-Output "`nRun without -DryRun to execute cleanup."
    exit 0
}

# Confirm action
Write-Output "`n⚠️ WARNING: About to reorganize $($allFiles.Count) files!`n"
$response = Read-Host "Continue? (yes/no)"
if ($response -ne 'yes') {
    Write-Output "Cancelled."
    exit 0
}

# Create backup
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = "cleanup_backup_$timestamp"
Write-Output "`n📦 Creating backup at: $backupDir"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

# Execute moves
Write-Output "`n📁 Moving files to proper locations..."
$moveCount = 0
foreach ($file in $fileMoves.Keys) {
    if (Test-Path $file) {
        $dest = $fileMoves[$file]

        # Create destination directory if needed
        if (-not (Test-Path $dest)) {
            New-Item -ItemType Directory -Path $dest -Force | Out-Null
        }

        # Backup original
        Copy-Item $file -Destination $backupDir

        # Move file
        try {
            Move-Item $file -Destination $dest -Force
            Write-Output "  ✅ Moved: $file → $dest"
            $moveCount++
        }
        catch {
            Write-Output "  ❌ Failed: $file - $($_.Exception.Message)"
        }
    }
}

# Execute deletions
Write-Output "`n🗑️ Deleting temporary files..."
$deleteCount = 0
foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        # Backup before deleting
        Copy-Item $file -Destination $backupDir

        try {
            Remove-Item $file -Force
            Write-Output "  ✅ Deleted: $file"
            $deleteCount++
        }
        catch {
            Write-Output "  ❌ Failed to delete: $file - $($_.Exception.Message)"
        }
    }
}

Write-Output "`n=== CLEANUP COMPLETE ===`n"
Write-Output "✅ Files moved: $moveCount"
Write-Output "✅ Files deleted: $deleteCount"
Write-Output "📦 Backup: $backupDir"
Write-Output "`n📋 Root directory now contains only:"
Get-ChildItem -File | ForEach-Object { Write-Output "  • $($_.Name)" }

Write-Output "`n💡 Next steps:"
Write-Output "  1. Review changes with: git status"
Write-Output "  2. Update imports/paths in code if needed"
Write-Output "  3. Test application builds and runs"
Write-Output "  4. Commit: git add . && git commit -m 'chore: reorganize root directory'"
