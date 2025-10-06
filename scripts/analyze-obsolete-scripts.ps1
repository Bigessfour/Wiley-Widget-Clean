#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Identifies obsolete, duplicate, and unused scripts for cleanup
.DESCRIPTION
    Analyzes scripts folder for:
    - Temporary/test scripts (temp_*, test_*, quick_*)
    - Duplicates (multiple versions of same script)
    - Obsolete scripts (superseded by newer implementations)
#>

param(
    [switch]$Interactive
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Output "`n=== SCRIPT CLEANUP ANALYSIS ===`n"

# Scripts identified as OBSOLETE or TEMPORARY
$obsoleteScripts = @{
    # Temporary extraction scripts
    'temp_extract.py'                     = 'Temporary - extraction test script'
    'extract_chapter.py'                  = 'Temporary - PDF extraction utility'

    # Duplicate/superseded scripts
    'dev-start-debug.py'                  = 'Superseded by dev-start-debugpy.py'
    'test_configuration.py'               = 'One-time test script'
    'quick_ui_inspect.py'                 = 'Development/debug utility'
    'ui_debug.py'                         = 'Development/debug utility'
    'verify_ui_setup.py'                  = 'One-time setup verification'

    # Legacy Azure scripts
    'setup-liberal-firewall.ps1'          = 'Dangerous - replaced by azure-safe-operations.ps1'

    # CI/CD legacy
    'ci-generate-manifest.ps1'            = 'CI-specific, not needed locally'
    'Generate-FetchabilityManifest.ps1'   = 'One-time generation script'
    'Index-RemoteRepository-Enhanced.ps1' = 'One-time indexing script'

    # Temporary Syncfusion scripts
    'check_syncfusion_nuget.py'           = 'One-time check, now have .ps1 version'

    # Obsolete profile scripts
    'cleanup-profiles.ps1'                = 'Superseded by fast-profile.ps1'

    # Legacy test runners
    'test-stafact-phase2.ps1'             = 'Phase 2 specific, tests complete'
    'ui_test_runner.py'                   = 'Python test runner, prefer .NET test tools'
}

# Scripts to KEEP (active, useful)
$keepScripts = @(
    # Core development
    'dev-start.py',
    'dev-start-debugpy.py',
    'cleanup-dotnet.py',
    'watch-py.py',

    # Azure operations
    'azure-setup.py',
    'azure-setup.ps1',
    'azure-safe-operations.ps1',
    'azure-sql-monitoring.ps1',
    'azure-sql-scheduler.ps1',
    'setup-azure-ad.ps1',
    'setup-azure-db.ps1',
    'update-firewall-ip.ps1',
    'test-azure-keyvault-integration.py',
    'test-enterprise-connections.ps1',

    # Build & testing
    'build.ps1',
    'incremental-build.ps1',
    'manage-build-cache.ps1',
    'test-with-kill.ps1',
    'test-stafact-with-cleanup.ps1',
    'run_database_tests.py',

    # Configuration & setup
    'setup-environment-variables.ps1',
    'setup-database.ps1',
    'apply-migrations.ps1',
    'load-env.py',
    'manage-secrets.ps1',
    'set-machine-env-from-dotenv.ps1',

    # Profiling & optimization
    'profile-startup.ps1',
    'fast-profile.ps1',
    'optimized-profile.ps1',
    'analyze-startup-timing.py',

    # Tools & utilities
    'wiley-widget-helper.ps1',
    'check-setup-status.ps1',
    'check-mcp-status.ps1',
    'show-syncfusion-license.ps1',
    'run-xaml-sleuth.ps1',

    # CI/CD
    'verify-cicd-tools.ps1',
    'verify-cicd-tools.py',
    'trunk-environment-setup.ps1',
    'quick-cicd-check.ps1',
    'install-azure-cli-extensions.ps1',
    'install-extensions.ps1',
    'setup-mcp-complete.ps1',
    'test-serilog-config.ps1',
    'test-debugpy.py',
    'Configure-XAI.ps1',

    # Standards & best practices
    'PowerShell-Standards.ps1',

    # Syncfusion management (NEW)
    'analyze-syncfusion-dlls.ps1',
    'cleanup-unused-syncfusion-dlls.ps1',
    'check-nuget-availability.ps1',
    'migrate-syncfusion-to-nuget.ps1',
    'add-remaining-syncfusion-packages.ps1',
    'add-missing-syncfusion-packages.ps1',

    # Cleanup utilities (NEW)
    'cleanup-root-directory.ps1'
)

# Analyze
$allScripts = Get-ChildItem scripts\*.ps1, scripts\*.py | Select-Object -ExpandProperty Name
$obsoleteFound = $allScripts | Where-Object { $obsoleteScripts.Keys -contains $_ }
$keepFound = $allScripts | Where-Object { $keepScripts -contains $_ }
$unclassified = $allScripts | Where-Object { $_ -notin $obsoleteScripts.Keys -and $_ -notin $keepScripts }

Write-Output "📊 Script Inventory:`n"
Write-Output "  Total scripts: $($allScripts.Count)"
Write-Output "  To keep: $($keepFound.Count)"
Write-Output "  Obsolete/temp: $($obsoleteFound.Count)"
Write-Output "  Unclassified: $($unclassified.Count)"

Write-Output "`n🗑️ SCRIPTS RECOMMENDED FOR REMOVAL ($($obsoleteFound.Count)):`n"
foreach ($script in $obsoleteFound | Sort-Object) {
    $reason = $obsoleteScripts[$script]
    Write-Output "  ❌ $script"
    Write-Output "     └─ Reason: $reason"
}

if ($unclassified.Count -gt 0) {
    Write-Output "`n⚠️ UNCLASSIFIED SCRIPTS (review needed):`n"
    foreach ($script in $unclassified | Sort-Object) {
        Write-Output "  ❓ $script"
    }
}

Write-Output "`n✅ KEEPING $($keepFound.Count) ACTIVE SCRIPTS"

if ($Interactive) {
    Write-Output "`n❓ Would you like to:"
    Write-Output "  1. Move obsolete scripts to scripts/archive/"
    Write-Output "  2. Delete obsolete scripts (with backup)"
    Write-Output "  3. Cancel"

    $choice = Read-Host "`nEnter choice (1-3)"

    switch ($choice) {
        "1" {
            Write-Output "`n📦 Moving to archive..."
            $archiveDir = "scripts/archive"
            if (-not (Test-Path $archiveDir)) {
                New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
            }
            foreach ($script in $obsoleteFound) {
                if (Test-Path "scripts\$script") {
                    Move-Item "scripts\$script" -Destination $archiveDir -Force
                    Write-Output "  ✅ Archived: $script"
                }
            }
            Write-Output "`n✅ Scripts archived to: $archiveDir"
        }
        "2" {
            $backupDir = "scripts_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

            foreach ($script in $obsoleteFound) {
                if (Test-Path "scripts\$script") {
                    Copy-Item "scripts\$script" -Destination $backupDir
                    Remove-Item "scripts\$script" -Force
                    Write-Output "  ✅ Deleted: $script"
                }
            }
            Write-Output "`n✅ Deleted $($obsoleteFound.Count) scripts (backup: $backupDir)"
        }
        "3" {
            Write-Output "Cancelled."
        }
    }
}
else {
    Write-Output "`nRun with -Interactive to archive or delete obsolete scripts."
}
