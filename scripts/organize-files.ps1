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
function Write-Success { param($Message) Write-Output "✅ $Message" }
function Write-Info { param($Message) Write-Output "ℹ️  $Message" }
function WriteColoredWarning { param($Message) Write-Output "⚠️  $Message" }
function WriteColoredError { param($Message) Write-Output "❌ $Message" }

function Move-FileOrDir {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Description
    )

    if (-not (Test-Path $Source)) {
        WriteColoredWarning "$Description - Source not found: $Source"
        return
    }

    $destDir = Split-Path $Destination -Parent
    if ($destDir -and -not (Test-Path $destDir)) {
        if ($DryRun) {
            Write-Info "[DRY RUN] Would create directory: $destDir"
        }
        else {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            Write-Info "Created directory: $destDir"
        }
    }

    if ($DryRun) {
        Write-Info "[DRY RUN] Would move: $Source → $Destination"
    }
    else {
        try {
            if ($UseGit -and (Test-Path ".git")) {
                git mv "$Source" "$Destination" 2>&1 | Out-Null
                Write-Success "Moved (git): $Description"
            }
            else {
                Move-Item -Path $Source -Destination $Destination -Force
                Write-Success "Moved: $Description"
            }
        }
        catch {
            WriteColoredWarning "Failed to move $Description : $($_.Exception.Message)"
        }
    }
}

function Remove-FileOrDir {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        return
    }

    if ($DryRun) {
        Write-Info "[DRY RUN] Would delete: $Path"
    }
    else {
        if ($PSCmdlet.ShouldProcess($Path, "Remove $Description")) {
            try {
                Remove-Item -Path $Path -Recurse -Force
                Write-Success "Deleted: $Description"
            }
            catch {
                WriteColoredWarning "Failed to delete $Description : $($_.Exception.Message)"
            }
        }
    }
}

Write-Output ""
Write-Output "═══════════════════════════════════════════════════════"
Write-Output "  WileyWidget File Organization Script"
Write-Output "═══════════════════════════════════════════════════════"
Write-Output ""

if ($DryRun) {
    WriteColoredWarning "DRY RUN MODE - No changes will be made"
    Write-Output ""
}

# Check if we're in the right directory
if (-not (Test-Path "WileyWidget.sln")) {
    WriteColoredError "This script must be run from the WileyWidget project root directory"
    exit 1
}

Write-Info "Creating new directory structure..."
Write-Output ""

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
        }
        else {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-Success "Created: $dir"
        }
    }
}

Write-Output ""
Write-Info "Moving documentation files..."
Write-Output ""

# Move documentation files
Move-FileOrDir -Source "AI_Integration_Plan.md" -Destination "docs/architecture/AI_Integration_Plan.md" -Description "AI Integration Plan"
Move-FileOrDir -Source "AI_INTEGRATION_DI_STATUS.md" -Destination "docs/architecture/AI_INTEGRATION_DI_STATUS.md" -Description "AI Integration DI Status"
Move-FileOrDir -Source "LOGGING_ENHANCEMENTS.md" -Destination "docs/architecture/LOGGING_ENHANCEMENTS.md" -Description "Logging Enhancements"
Move-FileOrDir -Source "quickbooks-registration-guide.md" -Destination "docs/guides/quickbooks-registration-guide.md" -Description "QuickBooks Registration Guide"
Move-FileOrDir -Source "QUICKBOOKS-SETUP.md" -Destination "docs/guides/QUICKBOOKS-SETUP.md" -Description "QuickBooks Setup Guide"
Move-FileOrDir -Source "COMMAND_REVIEW_REPORT.md" -Destination "docs/reports/COMMAND_REVIEW_REPORT.md" -Description "Command Review Report"
Move-FileOrDir -Source "fetchability-resources.json" -Destination "docs/analysis/fetchability-resources.json" -Description "Fetchability Resources"
# Note: repomix-output.md and repomix-output.xml remain in root per user preference
Move-FileOrDir -Source "wiley-widget-llm.txt" -Destination "docs/analysis/wiley-widget-llm.txt" -Description "LLM Context File"

Write-Output ""
Write-Info "Moving script files..."
Write-Output ""

# Move scripts
Move-FileOrDir -Source "setup-quickbooks-sandbox.ps1" -Destination "scripts/quickbooks/setup-quickbooks-sandbox.ps1" -Description "QuickBooks Sandbox Setup"
Move-FileOrDir -Source "setup-town-of-wiley.ps1" -Destination "scripts/quickbooks/setup-town-of-wiley.ps1" -Description "Town of Wiley Setup"
Move-FileOrDir -Source "test-qbo-keyvault-integration.ps1" -Destination "scripts/quickbooks/test-qbo-keyvault-integration.ps1" -Description "QBO KeyVault Test"
Move-FileOrDir -Source "test-quickbooks-connection.ps1" -Destination "scripts/quickbooks/test-quickbooks-connection.ps1" -Description "QuickBooks Connection Test"
Move-FileOrDir -Source "run-dashboard-tests.ps1" -Destination "scripts/testing/run-dashboard-tests.ps1" -Description "Dashboard Tests"

Write-Output ""
Write-Info "Moving Docker files..."
Write-Output ""

# Move Docker files
Move-FileOrDir -Source "docker-compose.regionviewregistry-tests.yml" -Destination "docker/docker-compose.regionviewregistry-tests.yml" -Description "Docker Compose (RegionView)"
Move-FileOrDir -Source "docker-compose.test.yml" -Destination "docker/docker-compose.test.yml" -Description "Docker Compose (Tests)"
Move-FileOrDir -Source "Dockerfile.regionviewregistry-tests" -Destination "docker/Dockerfile.regionviewregistry-tests" -Description "Dockerfile (RegionView Tests)"
Move-FileOrDir -Source "Dockerfile.test" -Destination "docker/Dockerfile.test" -Description "Dockerfile (Tests)"
Move-FileOrDir -Source "Dockerfile.test-regionviewregistry" -Destination "docker/Dockerfile.test-regionviewregistry" -Description "Dockerfile (Test RegionView)"

Write-Output ""
Write-Info "Moving test files..."
Write-Output ""

# Move test files
Move-FileOrDir -Source "QuickBooksStructureTest.cs" -Destination "tests/integration/QuickBooksStructureTest.cs" -Description "QuickBooks Structure Test"

Write-Output ""
Write-Info "Moving configuration files..."
Write-Output ""

# Move configuration
Move-FileOrDir -Source ".env.production.sample" -Destination "config/.env.production.sample" -Description "Production Environment Sample"

Write-Output ""
Write-Info "Moving database files..."
Write-Output ""

# Move database files
Move-FileOrDir -Source "WileyWidgetDev.db" -Destination "data/WileyWidgetDev.db" -Description "Development Database"
Move-FileOrDir -Source "DatabaseSetup" -Destination "data/DatabaseSetup" -Description "Database Setup Scripts"
Move-FileOrDir -Source "DatabaseTest" -Destination "data/tests/DatabaseTest" -Description "Database Tests"

if (-not $SkipCleanup) {
    Write-Output ""
    Write-Info "Cleaning up temporary files..."
    Write-Output ""

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
}
else {
    Write-Info "Skipping cleanup (--SkipCleanup specified)"
}

Write-Output ""
Write-Output "═══════════════════════════════════════════════════════"
Write-Success "File organization complete!"
Write-Output "═══════════════════════════════════════════════════════"
Write-Output ""

if ($DryRun) {
    WriteColoredWarning "This was a DRY RUN - no changes were made"
    Write-Info "Run without -DryRun to execute the changes"
}
else {
    Write-Info "Verify changes with: git status"

    if ($UseGit) {
        Write-Info "Files moved using git mv - commit the changes"
        Write-Output ""
        Write-Output "Next steps:"
        Write-Output "  1. Review changes: git status"
        Write-Output "  2. Commit changes: git commit -m 'refactor: organize project files into proper structure'"
    }
}

Write-Output ""
