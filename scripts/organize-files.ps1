#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Organizes WileyWidget project files into proper directory structure.

.DESCRIPTION
    This script moves files from the root directory into organized subdirectories,
    creates new directory structure, and cleans up temporary files.
    
    Uses git mv when possible to preserve file history.

.PARAMETER DryRun
    Show what would be done without actually moving files.

.PARAMETER UseGit
    Use 'git mv' instead of 'Move-Item' to preserve git history.

.PARAMETER SkipCleanup
    Skip deletion of temporary/build files.

.EXAMPLE
    .\organize-files.ps1 -DryRun
    Shows what would be done without making changes.

.EXAMPLE
    .\organize-files.ps1 -UseGit
    Reorganizes files using git mv to preserve history.
#>

param(
    [switch]$DryRun,
    [switch]$UseGit,
    [switch]$SkipCleanup
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

function Move-FileOrDir {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Description
    )
    
    if (-not (Test-Path $Source)) {
        Write-Warning "$Description - Source not found: $Source"
        return
    }
    
    $destDir = Split-Path $Destination -Parent
    if ($destDir -and -not (Test-Path $destDir)) {
        if ($DryRun) {
            Write-Info "[DRY RUN] Would create directory: $destDir"
        } else {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            Write-Info "Created directory: $destDir"
        }
    }
    
    if ($DryRun) {
        Write-Info "[DRY RUN] Would move: $Source → $Destination"
    } else {
        try {
            if ($UseGit -and (Test-Path ".git")) {
                git mv "$Source" "$Destination" 2>&1 | Out-Null
                Write-Success "Moved (git): $Description"
            } else {
                Move-Item -Path $Source -Destination $Destination -Force
                Write-Success "Moved: $Description"
            }
        } catch {
            Write-Warning "Failed to move $Description : $($_.Exception.Message)"
        }
    }
}

function Remove-FileOrDir {
    param(
        [string]$Path,
        [string]$Description
    )
    
    if (-not (Test-Path $Path)) {
        return
    }
    
    if ($DryRun) {
        Write-Info "[DRY RUN] Would delete: $Path"
    } else {
        try {
            Remove-Item -Path $Path -Recurse -Force
            Write-Success "Deleted: $Description"
        } catch {
            Write-Warning "Failed to delete $Description : $($_.Exception.Message)"
        }
    }
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  WileyWidget File Organization Script" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - No changes will be made"
    Write-Host ""
}

# Check if we're in the right directory
if (-not (Test-Path "WileyWidget.sln")) {
    Write-Error "This script must be run from the WileyWidget project root directory"
    exit 1
}

Write-Info "Creating new directory structure..."
Write-Host ""

# Create directory structure
$directories = @(
    "docs/architecture",
    "docs/guides",
    "docs/reports",
    "docs/analysis",
    "data",
    "data/tests",
    "docker",
    "scripts/quickbooks",
    "scripts/testing",
    "tests/integration"
)

foreach ($dir in $directories) {
    if (-not (Test-Path $dir)) {
        if ($DryRun) {
            Write-Info "[DRY RUN] Would create: $dir"
        } else {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Success "Created: $dir"
        }
    }
}

Write-Host ""
Write-Info "Moving documentation files..."
Write-Host ""

# Move documentation files
Move-FileOrDir "AI_Integration_Plan.md" "docs/architecture/AI_Integration_Plan.md" "AI Integration Plan"
Move-FileOrDir "AI_INTEGRATION_DI_STATUS.md" "docs/architecture/AI_INTEGRATION_DI_STATUS.md" "AI Integration DI Status"
Move-FileOrDir "LOGGING_ENHANCEMENTS.md" "docs/architecture/LOGGING_ENHANCEMENTS.md" "Logging Enhancements"
Move-FileOrDir "quickbooks-registration-guide.md" "docs/guides/quickbooks-registration-guide.md" "QuickBooks Registration Guide"
Move-FileOrDir "QUICKBOOKS-SETUP.md" "docs/guides/QUICKBOOKS-SETUP.md" "QuickBooks Setup Guide"
Move-FileOrDir "COMMAND_REVIEW_REPORT.md" "docs/reports/COMMAND_REVIEW_REPORT.md" "Command Review Report"
Move-FileOrDir "fetchability-resources.json" "docs/analysis/fetchability-resources.json" "Fetchability Resources"
# Note: repomix-output.md and repomix-output.xml remain in root per user preference
Move-FileOrDir "wiley-widget-llm.txt" "docs/analysis/wiley-widget-llm.txt" "LLM Context File"

Write-Host ""
Write-Info "Moving script files..."
Write-Host ""

# Move scripts
Move-FileOrDir "setup-quickbooks-sandbox.ps1" "scripts/quickbooks/setup-quickbooks-sandbox.ps1" "QuickBooks Sandbox Setup"
Move-FileOrDir "setup-town-of-wiley.ps1" "scripts/quickbooks/setup-town-of-wiley.ps1" "Town of Wiley Setup"
Move-FileOrDir "test-qbo-keyvault-integration.ps1" "scripts/quickbooks/test-qbo-keyvault-integration.ps1" "QBO KeyVault Test"
Move-FileOrDir "test-quickbooks-connection.ps1" "scripts/quickbooks/test-quickbooks-connection.ps1" "QuickBooks Connection Test"
Move-FileOrDir "run-dashboard-tests.ps1" "scripts/testing/run-dashboard-tests.ps1" "Dashboard Tests"

Write-Host ""
Write-Info "Moving Docker files..."
Write-Host ""

# Move Docker files
Move-FileOrDir "docker-compose.regionviewregistry-tests.yml" "docker/docker-compose.regionviewregistry-tests.yml" "Docker Compose (RegionView)"
Move-FileOrDir "docker-compose.test.yml" "docker/docker-compose.test.yml" "Docker Compose (Tests)"
Move-FileOrDir "Dockerfile.regionviewregistry-tests" "docker/Dockerfile.regionviewregistry-tests" "Dockerfile (RegionView Tests)"
Move-FileOrDir "Dockerfile.test" "docker/Dockerfile.test" "Dockerfile (Tests)"
Move-FileOrDir "Dockerfile.test-regionviewregistry" "docker/Dockerfile.test-regionviewregistry" "Dockerfile (Test RegionView)"

Write-Host ""
Write-Info "Moving test files..."
Write-Host ""

# Move test files
Move-FileOrDir "QuickBooksStructureTest.cs" "tests/integration/QuickBooksStructureTest.cs" "QuickBooks Structure Test"

Write-Host ""
Write-Info "Moving configuration files..."
Write-Host ""

# Move configuration
Move-FileOrDir ".env.production.sample" "config/.env.production.sample" "Production Environment Sample"

Write-Host ""
Write-Info "Moving database files..."
Write-Host ""

# Move database files
Move-FileOrDir "WileyWidgetDev.db" "data/WileyWidgetDev.db" "Development Database"
Move-FileOrDir "DatabaseSetup" "data/DatabaseSetup" "Database Setup Scripts"
Move-FileOrDir "DatabaseTest" "data/tests/DatabaseTest" "Database Tests"

if (-not $SkipCleanup) {
    Write-Host ""
    Write-Info "Cleaning up temporary files..."
    Write-Host ""
    
    # Delete temporary/build files
    Remove-FileOrDir "build-detailed.log" "Build Detailed Log"
    Remove-FileOrDir "build-diag.txt" "Build Diagnostics"
    Remove-FileOrDir "build-errors.log" "Build Errors Log"
    Remove-FileOrDir "debug-hosted.log" "Debug Hosted Log"
    Remove-FileOrDir "xaml-trace.log" "XAML Trace Log"
    Remove-FileOrDir "psscriptanalyzer-results.txt" "PSScriptAnalyzer Results"
    Remove-FileOrDir "startup-performance-results.json" "Startup Performance Results"
    Remove-FileOrDir ".packages.lastmodified" "Packages Last Modified"
    Remove-FileOrDir ".coverage" "Coverage File"
    
    # Delete build artifact directories
    Remove-FileOrDir ".buildcache" "Build Cache Directory"
    Remove-FileOrDir ".tmp.drivedownload" "Temp Drive Download"
    Remove-FileOrDir ".tmp.driveupload" "Temp Drive Upload"
} else {
    Write-Info "Skipping cleanup (--SkipCleanup specified)"
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Success "File organization complete!"
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Warning "This was a DRY RUN - no changes were made"
    Write-Info "Run without -DryRun to execute the changes"
} else {
    Write-Info "Verify changes with: git status"
    
    if ($UseGit) {
        Write-Info "Files moved using git mv - commit the changes"
        Write-Host ""
        Write-Host "Next steps:" -ForegroundColor Yellow
        Write-Host "  1. Review changes: git status" -ForegroundColor White
        Write-Host "  2. Commit changes: git commit -m 'refactor: organize project files into proper structure'" -ForegroundColor White
    }
}

Write-Host ""
