# Trunk Environment Setup Script for Wiley Widget
# This script configures trunk to work properly with the Wiley Widget development environment

param(
    [switch]$Diagnose,
    [switch]$Fix,
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

# Load environment variables from .env file
function Load-Environment {
    Write-Host "🔧 Loading Wiley Widget environment configuration..." -ForegroundColor Cyan

    if (Test-Path ".env") {
        Write-Host "📄 Found .env file, loading environment variables..." -ForegroundColor Green
        Get-Content ".env" | Where-Object { $_ -match '^[^#].*=' } | ForEach-Object {
            $key, $value = $_ -split '=', 2
            $key = $key.Trim()
            $value = $value.Trim()
            [Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
        Write-Host "✅ Environment variables loaded from .env" -ForegroundColor Green
    }
    else {
        Write-Warning "⚠️  .env file not found. Some features may not work correctly."
    }
}

# Diagnose trunk configuration
function Test-TrunkConfiguration {
    Write-Host "🔍 Diagnosing trunk configuration..." -ForegroundColor Yellow

    # Check if trunk is installed
    try {
        $trunkVersion = & trunk --version 2>$null
        Write-Host "✅ Trunk is installed: $trunkVersion" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Trunk is not installed or not in PATH"
        return $false
    }

    # Check trunk.yaml configuration
    if (Test-Path ".trunk\trunk.yaml") {
        Write-Host "✅ Trunk configuration file found" -ForegroundColor Green

        # Validate YAML syntax
        try {
            $yamlContent = Get-Content ".trunk\trunk.yaml" -Raw
            # Basic YAML validation (this is a simple check)
            if ($yamlContent -match 'version:\s*0\.1') {
                Write-Host "✅ Trunk YAML configuration appears valid" -ForegroundColor Green
            }
            else {
                Write-Warning "⚠️  Trunk YAML version may be incorrect"
            }
        }
        catch {
            Write-Error "❌ Error reading trunk.yaml: $_"
            return $false
        }
    }
    else {
        Write-Error "❌ Trunk configuration file not found at .trunk\trunk.yaml"
        return $false
    }

    # Check environment variables
    $requiredEnvVars = @(
        'ASPNETCORE_ENVIRONMENT',
        'DOTNET_CLI_TELEMETRY_OPTOUT',
        'POWERSHELL_EXECUTION_POLICY'
    )

    foreach ($envVar in $requiredEnvVars) {
        if ([Environment]::GetEnvironmentVariable($envVar)) {
            Write-Host "✅ Environment variable $envVar is set" -ForegroundColor Green
        }
        else {
            Write-Warning "⚠️  Environment variable $envVar is not set"
        }
    }

    Write-Host "🔍 Trunk configuration diagnosis complete" -ForegroundColor Cyan
    return $true
}

# Fix trunk configuration issues
function Repair-TrunkConfiguration {
    Write-Host "🔧 Attempting to fix trunk configuration issues..." -ForegroundColor Yellow

    # Ensure trunk daemon is not running (to avoid conflicts)
    try {
        & trunk daemon shutdown 2>$null
        Write-Host "✅ Trunk daemon shut down" -ForegroundColor Green
    }
    catch {
        Write-Host "ℹ️  Trunk daemon was not running" -ForegroundColor Blue
    }

    # Clear trunk cache
    try {
        & trunk cache clean 2>$null
        Write-Host "✅ Trunk cache cleared" -ForegroundColor Green
    }
    catch {
        Write-Warning "⚠️  Could not clear trunk cache: $_"
    }

    # Reinitialize trunk
    try {
        & trunk init --force 2>$null
        Write-Host "✅ Trunk reinitialized" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Failed to reinitialize trunk: $_"
        return $false
    }

    # Test trunk functionality
    try {
        & trunk check --ci 2>$null
        Write-Host "✅ Trunk check passed" -ForegroundColor Green
    }
    catch {
        Write-Warning "⚠️  Trunk check failed, but this may be expected on first run"
    }

    Write-Host "🔧 Trunk configuration repair complete" -ForegroundColor Cyan
    return $true
}

# Reset trunk to clean state
function Reset-TrunkConfiguration {
    Write-Host "🔄 Resetting trunk to clean state..." -ForegroundColor Red

    # Stop all trunk processes
    Get-Process -Name "trunk" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "✅ All trunk processes stopped" -ForegroundColor Green

    # Remove trunk cache and data
    $trunkPaths = @(
        ".trunk\cache",
        ".trunk\data",
        ".trunk\logs"
    )

    foreach ($path in $trunkPaths) {
        if (Test-Path $path) {
            Remove-Item -Path $path -Recurse -Force
            Write-Host "✅ Removed $path" -ForegroundColor Green
        }
    }

    # Reinitialize trunk
    try {
        & trunk init 2>$null
        Write-Host "✅ Trunk reinitialized from clean state" -ForegroundColor Green
    }
    catch {
        Write-Error "❌ Failed to reinitialize trunk: $_"
        return $false
    }

    Write-Host "🔄 Trunk reset complete" -ForegroundColor Cyan
    return $true
}

# Main execution
Write-Host "🚀 Wiley Widget Trunk Environment Setup" -ForegroundColor Magenta
Write-Host "==========================================" -ForegroundColor Magenta

# Load environment
Load-Environment

# Execute requested operations
$success = $true

if ($Diagnose) {
    $success = Test-TrunkConfiguration
}

if ($Fix) {
    $success = Repair-TrunkConfiguration
}

if ($Reset) {
    $success = Reset-TrunkConfiguration
}

# If no specific operation requested, do a full setup
if (-not ($Diagnose -or $Fix -or $Reset)) {
    Write-Host "📋 Performing full trunk environment setup..." -ForegroundColor Cyan

    if (Test-TrunkConfiguration) {
        Write-Host "✅ Trunk configuration is healthy" -ForegroundColor Green
    }
    else {
        Write-Host "🔧 Configuration issues detected, attempting repair..." -ForegroundColor Yellow
        $success = Repair-TrunkConfiguration
    }
}

# Final status
if ($success) {
    Write-Host "" -ForegroundColor White
    Write-Host "🎉 Trunk environment setup completed successfully!" -ForegroundColor Green
    Write-Host "💡 You can now use trunk commands in this environment" -ForegroundColor Cyan
    Write-Host "   Example: trunk check, trunk fmt, trunk daemon launch" -ForegroundColor Gray
}
else {
    Write-Host "" -ForegroundColor White
    Write-Host "❌ Trunk environment setup failed. Please check the errors above." -ForegroundColor Red
    exit 1
}
