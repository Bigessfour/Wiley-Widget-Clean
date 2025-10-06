# Clean up orphaned .NET processes during development
# Run this before starting new development sessions to prevent conflicts

param(
    [switch]$Force,
    [switch]$DryRun
)

Write-Host "=== .NET Process Cleanup ===" -ForegroundColor Cyan

# Function to get .NET processes
function Get-DotNetProcess {
    try {
        $processes = Get-Process | Where-Object {
            $_.ProcessName -like "*dotnet*" -or
            $_.ProcessName -eq "WileyWidget"
        } | Select-Object Id, ProcessName, StartTime

        return $processes
    }
    catch {
        Write-Host "❌ Error getting processes: $($_.Exception.Message)" -ForegroundColor Red
        return @()
    }
}

# Get current processes
try {
    $dotnetProcesses = Get-DotNetProcesses
    if (-not $dotnetProcesses -or $dotnetProcesses.Count -eq 0) {
        Write-Host "✅ No orphaned .NET processes found" -ForegroundColor Green
        exit 0
    }
}
catch {
    Write-Host "❌ Error getting processes: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Found $($dotnetProcesses.Count) .NET-related process(es):" -ForegroundColor Yellow
foreach ($proc in $dotnetProcesses) {
    Write-Host "  - $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor White
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run mode - no processes killed" -ForegroundColor Cyan
    exit 0
}

if (-not $Force) {
    $confirm = Read-Host "Kill these processes? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Aborted" -ForegroundColor Yellow
        exit 0
    }
}

# Kill processes
$killCount = 0
foreach ($proc in $dotnetProcesses) {
    try {
        Stop-Process -Id $proc.Id -Force
        Write-Host "✅ Killed $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Green
        $killCount++
    }
    catch {
        Write-Host "❌ Failed to kill $($proc.ProcessName) (PID: $($proc.Id)): $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Cleaned up $killCount process(es)" -ForegroundColor Green

# Optional: Clean up any leftover files
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Cyan
try {
    # Clean bin/obj if they exist
    if (Test-Path "bin") { Remove-Item "bin" -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force -ErrorAction SilentlyContinue }
    Write-Host "✅ Temporary build files cleaned" -ForegroundColor Green
}
catch {
    Write-Host "⚠️  Could not clean temporary files: $($_.Exception.Message)" -ForegroundColor Yellow
}
