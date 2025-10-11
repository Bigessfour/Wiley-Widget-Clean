# Trunk Maintenance Script for Wiley Widget Project
# This script provides utilities for Trunk CLI maintenance and monitoring

param(
    [switch]$Diagnose,
    [switch]$Fix,
    [switch]$Clean,
    [switch]$Monitor,
    [switch]$Help
)

function Write-Header {
    param([string]$Message)
    Write-Information "🔧 $Message" -InformationAction Continue
    Write-Information ("-" * 50) -InformationAction Continue
}

function Write-Success {
    param([string]$Message)
    Write-Information "✅ $Message" -InformationAction Continue
}

function Write-ScriptWarning {
    param([string]$Message)
    Write-Information "⚠️  $Message" -InformationAction Continue
}

function Write-ScriptError {
    param([string]$Message)
    Write-Information "❌ $Message" -InformationAction Continue
}

if ($Help) {
    Write-Header "Trunk Maintenance Script Help"
    Write-Information "Usage: .\trunk-maintenance.ps1 [options]" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Options:" -InformationAction Continue
    Write-Information "  -Diagnose    Run diagnostic checks on Trunk configuration" -InformationAction Continue
    Write-Information "  -Fix         Apply automatic fixes for common issues" -InformationAction Continue
    Write-Information "  -Clean       Clean Trunk cache and temporary files" -InformationAction Continue
    Write-Information "  -Monitor     Monitor Trunk performance and resource usage" -InformationAction Continue
    Write-Information "  -Help        Show this help message" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Examples:" -InformationAction Continue
    Write-Information "  .\trunk-maintenance.ps1 -Diagnose" -InformationAction Continue
    Write-Information "  .\trunk-maintenance.ps1 -Fix -Clean" -InformationAction Continue
    exit 0
}

if ($Diagnose) {
    Write-Header "Trunk Configuration Diagnostics"

    # Check Trunk version
    Write-Information "Checking Trunk version..." -InformationAction Continue
    try {
        $version = & trunk --version 2>$null
        Write-Success "Trunk version: $version"
    }
    catch {
        Write-ScriptError "Trunk CLI not found. Please install Trunk CLI."
        exit 1
    }

    # Check configuration validity
    Write-Information "Validating Trunk configuration..." -InformationAction Continue
    try {
        $configCheck = & trunk print-config 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Trunk configuration is valid"
        }
        else {
            Write-ScriptWarning "Configuration issues detected:"
            Write-Information $configCheck -InformationAction Continue
        }
    }
    catch {
        Write-ScriptError "Failed to validate configuration"
    }

    # Check tool installations
    Write-Information "Checking tool installations..." -InformationAction Continue
    $toolsPath = "$env:LOCALAPPDATA\trunk\tools"
    if (Test-Path $toolsPath) {
        $toolCount = (Get-ChildItem $toolsPath -Directory).Count
        Write-Success "Found $toolCount installed tools"
    }
    else {
        Write-ScriptWarning "No tools directory found"
    }

    # Check cache status
    Write-Information "Checking cache status..." -InformationAction Continue
    $cachePath = ".trunk\cache"
    if (Test-Path $cachePath) {
        $cacheSize = (Get-ChildItem $cachePath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Information "Cache size: $([math]::Round($cacheSize, 2)) MB" -InformationAction Continue
    }
    else {
        Write-Information "No cache found" -InformationAction Continue
    }
}

if ($Clean) {
    Write-Header "Cleaning Trunk Cache and Temporary Files"

    # Clean Trunk cache
    Write-Information "Cleaning Trunk cache..." -InformationAction Continue
    try {
        & trunk cache clean 2>$null
        Write-Success "Cache cleaned"
    }
    catch {
        Write-ScriptWarning "Failed to clean cache"
    }

    # Remove temporary files
    Write-Information "Removing temporary Trunk files..." -InformationAction Continue
    $tempPaths = @(
        ".trunk\out",
        ".trunk\logs",
        ".trunk\actions",
        ".trunk\notifications"
    )

    foreach ($path in $tempPaths) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force
            Write-Success "Removed $path"
        }
    }
}

if ($Fix) {
    Write-Header "Applying Automatic Fixes"

    # Upgrade tools
    Write-Information "Upgrading Trunk tools..." -InformationAction Continue
    try {
        & trunk upgrade 2>$null
        Write-Success "Tools upgraded"
    }
    catch {
        Write-ScriptWarning "Failed to upgrade tools"
    }

    # Fix auto-fixable issues
    Write-Information "Applying auto-fixes..." -InformationAction Continue
    try {
        $fixResult = & trunk check --fix --no-progress 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Auto-fixes applied"
        }
        else {
            Write-Information "Fix results:" -InformationAction Continue
            Write-Information $fixResult -InformationAction Continue
        }
    }
    catch {
        Write-ScriptWarning "Failed to apply fixes"
    }
}

if ($Monitor) {
    Write-Header "Trunk Performance Monitoring"

    # Monitor memory usage
    Write-Information "Monitoring Trunk processes..." -InformationAction Continue
    $trunkProcesses = Get-Process | Where-Object { $_.ProcessName -like "*trunk*" }
    if ($trunkProcesses) {
        foreach ($process in $trunkProcesses) {
            $memoryMB = [math]::Round($process.WorkingSet64 / 1MB, 2)
            Write-Information "Process $($process.ProcessName) (PID: $($process.Id)): $memoryMB MB" -InformationAction Continue
        }
    }
    else {
        Write-Information "No active Trunk processes found" -InformationAction Continue
    }

    # Check recent activity
    Write-Information "Checking recent Trunk activity..." -InformationAction Continue
    $logPath = ".trunk\logs"
    if (Test-Path $logPath) {
        $recentLogs = Get-ChildItem $logPath -File | Sort-Object LastWriteTime -Descending | Select-Object -First 3
        foreach ($log in $recentLogs) {
            Write-Information "Log: $($log.Name) ($($log.LastWriteTime))" -InformationAction Continue
        }
    }
}

if (-not ($Diagnose -or $Fix -or $Clean -or $Monitor -or $Help)) {
    Write-ScriptWarning "No action specified. Use -Help for usage information."
    exit 1
}

Write-Success "Trunk maintenance completed"
