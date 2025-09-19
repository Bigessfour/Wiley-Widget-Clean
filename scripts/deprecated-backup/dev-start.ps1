# Development startup script for Wiley Widget
# Handles cleanup and proper process management

param(
    [switch]$CleanOnly,
    [switch]$NoWatch,
    [switch]$Verbose
)

Write-Host "=== Wiley Widget Development Startup ===" -ForegroundColor Cyan

# Step 1: Clean up orphaned processes
Write-Host ""
Write-Host "Step 1: Cleaning up orphaned processes..." -ForegroundColor Yellow
try {
    & "$PSScriptRoot\cleanup-dotnet.ps1" -Force
    Write-Host "✅ Process cleanup completed" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Process cleanup failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

if ($CleanOnly) {
    Write-Host ""
    Write-Host "Clean-only mode - exiting" -ForegroundColor Cyan
    exit 0
}

# Step 2: Ensure no conflicting processes
Write-Host ""
Write-Host "Step 2: Checking for conflicts..." -ForegroundColor Yellow
try {
    $conflicts = Get-Process | Where-Object { $_.ProcessName -eq "WileyWidget" }
    if ($conflicts -and $conflicts.Count -gt 0) {
        Write-Host "⚠️  Found $($conflicts.Count) WileyWidget process(es) still running" -ForegroundColor Yellow
        foreach ($proc in $conflicts) {
            Write-Host "  - PID: $($proc.Id), Started: $($proc.StartTime)" -ForegroundColor White
        }

        $kill = Read-Host "Kill these processes? (y/N)"
        if ($kill -eq 'y' -or $kill -eq 'Y') {
            $conflicts | Stop-Process -Force
            Write-Host "✅ Conflicting processes killed" -ForegroundColor Green
        }
    }
    else {
        Write-Host "✅ No conflicting processes found" -ForegroundColor Green
    }
}
catch {
    Write-Host "⚠️  Could not check for conflicts: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 3: Clean build artifacts
Write-Host ""
Write-Host "Step 3: Cleaning build artifacts..." -ForegroundColor Yellow
try {
    dotnet clean WileyWidget.csproj
    Write-Host "✅ Build artifacts cleaned" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Clean failed: $($_.Exception.Message)" -ForegroundColor Yellow
}

# Step 4: Start development
Write-Host ""
Write-Host "Step 4: Starting development..." -ForegroundColor Yellow

if ($NoWatch) {
    Write-Host "Running without watch mode..." -ForegroundColor Cyan
    dotnet run --project WileyWidget.csproj
}
else {
    Write-Host "Starting dotnet watch..." -ForegroundColor Cyan
    Write-Host "Press Ctrl+C to stop, then run cleanup script if needed" -ForegroundColor White
    Write-Host ""

    # Start watch in foreground
    dotnet watch run --project WileyWidget.csproj
}

Write-Host ""
Write-Host "Development session ended" -ForegroundColor Cyan