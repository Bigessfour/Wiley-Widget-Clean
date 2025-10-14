#Requires -Version 7.0

<#
.SYNOPSIS
    PowerShell script to build and run xUnit tests for DashboardModule in Docker container

.DESCRIPTION
    This script provides commands to build the Docker image and run xUnit tests
    for the DashboardModule in a Windows container environment.

.PARAMETER Action
    The action to perform: Build, Run, or Test

.EXAMPLE
    .\run-dashboard-tests.ps1 -Action Build
    .\run-dashboard-tests.ps1 -Action Run
    .\run-dashboard-tests.ps1 -Action Test
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Build", "Run", "Test")]
    [string]$Action = "Test",

    [Parameter(Mandatory = $false)]
    [switch]$Clean
)

# Script configuration
$dockerfile = "Dockerfile.test"
$imageName = "wiley-widget-tests"
$containerName = "wiley-widget-test-runner"

# Ensure TestResults directory exists
if (-not (Test-Path "TestResults")) {
    New-Item -ItemType Directory -Path "TestResults" | Out-Null
}

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Green
}

function Write-Command {
    param([string]$Command)
    Write-Host "    $Command" -ForegroundColor Yellow
}

function Invoke-Build {
    Write-Step "Building Docker image for xUnit tests"

    $buildCommand = "docker build -f $dockerfile -t $imageName ."
    Write-Command $buildCommand

    try {
        & docker build -f $dockerfile -t $imageName .
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Docker image built successfully!" -ForegroundColor Green
        } else {
            Write-Error "Failed to build Docker image"
            exit $LASTEXITCODE
        }
    }
    catch {
        Write-Error "Error building Docker image: $_"
        exit 1
    }
}

function Invoke-Run {
    Write-Step "Running xUnit tests in Docker container"

    # Clean up any existing containers
    docker rm -f $containerName 2>$null | Out-Null

    $runCommand = "docker run --name $containerName -v ${PWD}/TestResults:/test-results $imageName"
    Write-Command $runCommand

    try {
        & docker run --name $containerName -v ${PWD}/TestResults:/test-results $imageName
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            Write-Host "All tests passed!" -ForegroundColor Green
        } else {
            Write-Warning "Some tests failed (exit code: $exitCode)"
        }

        # Copy test results
        Write-Step "Copying test results"
        if (Test-Path "TestResults") {
            Get-ChildItem "TestResults" | Format-Table Name, LastWriteTime
        }

        return $exitCode
    }
    catch {
        Write-Error "Error running tests in Docker: $_"
        exit 1
    }
}

function Invoke-Test {
    Write-Step "Running complete test cycle (Build + Run)"

    # Build the image
    Invoke-Build

    # Run the tests
    $exitCode = Invoke-Run

    # Summary
    Write-Step "Test execution summary"
    Write-Host "Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { "Green" } else { "Red" })

    if ($exitCode -eq 0) {
        Write-Host "✅ All DashboardModule tests passed successfully!" -ForegroundColor Green
    } else {
        Write-Host "❌ Some DashboardModule tests failed. Check TestResults directory for details." -ForegroundColor Red
    }

    exit $exitCode
}

function Invoke-Clean {
    Write-Step "Cleaning up Docker resources"

    # Remove containers
    docker rm -f $containerName 2>$null | Out-Null

    # Remove images
    docker rmi -f $imageName 2>$null | Out-Null

    # Clean up test results
    if (Test-Path "TestResults") {
        Remove-Item "TestResults" -Recurse -Force
    }

    Write-Host "Cleanup completed!" -ForegroundColor Green
}

# Main execution logic
switch ($Action) {
    "Build" {
        Invoke-Build
    }
    "Run" {
        Invoke-Run
    }
    "Test" {
        Invoke-Test
    }
}

if ($Clean) {
    Invoke-Clean
}

# Display usage information
Write-Host ""
Write-Host "Available commands:" -ForegroundColor Cyan
Write-Host "  .\run-dashboard-tests.ps1 -Action Build    # Build Docker image"
Write-Host "  .\run-dashboard-tests.ps1 -Action Run      # Run tests in container"
Write-Host "  .\run-dashboard-tests.ps1 -Action Test     # Build and run tests"
Write-Host "  .\run-dashboard-tests.ps1 -Clean           # Clean up Docker resources"
Write-Host ""
Write-Host "Direct Docker commands:" -ForegroundColor Cyan
Write-Host "  docker build -f Dockerfile.test -t wiley-widget-tests ."
Write-Host "  docker run --rm -v ${PWD}/TestResults:/test-results wiley-widget-tests"
Write-Host "  docker-compose -f docker-compose.test.yml up --abort-on-container-exit"