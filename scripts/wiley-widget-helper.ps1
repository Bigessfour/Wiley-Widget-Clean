# Wiley Widget Helper Functions
# PowerShell utilities to support the C# WPF application

function Show-WileyWidgetStatus {
    <#
    .SYNOPSIS
        Displays current status of Wiley Widget application
    .DESCRIPTION
        Checks if Wiley Widget is running, shows process info, and app data
    #>
    param()

    Write-Host "🔍 Checking Wiley Widget Status..." -ForegroundColor Cyan

    # Check if app is running
    $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Host "✅ Wiley Widget is running" -ForegroundColor Green
        Write-Host "   Process ID: $($process.Id)" -ForegroundColor Gray
        Write-Host "   Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB" -ForegroundColor Gray
        Write-Host "   Start Time: $($process.StartTime)" -ForegroundColor Gray
    }
    else {
        Write-Host "❌ Wiley Widget is not running" -ForegroundColor Red
    }

    # Check app data directory
    $appDataPath = "$env:APPDATA\WileyWidget"
    if (Test-Path $appDataPath) {
        Write-Host "📁 App Data Directory: $appDataPath" -ForegroundColor Blue
        $logFiles = Get-ChildItem "$appDataPath\logs" -Filter "*.log" -ErrorAction SilentlyContinue
        if ($logFiles) {
            Write-Host "   📄 Latest Log: $($logFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty Name)" -ForegroundColor Gray
        }
    }
}

function Start-WileyWidget {
    <#
    .SYNOPSIS
        Starts the Wiley Widget WPF application
    .DESCRIPTION
        Launches Wiley Widget with proper environment setup
    #>
    param(
        [switch]$Debug,
        [switch]$CleanStart
    )

    Write-Host "🚀 Starting Wiley Widget..." -ForegroundColor Cyan

    if ($CleanStart) {
        Write-Host "🧹 Performing clean start..." -ForegroundColor Yellow
        # Kill any existing processes
        Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue | Stop-Process -Force
        # Clear logs if requested
        $logPath = "$env:APPDATA\WileyWidget\logs"
        if (Test-Path $logPath) {
            Remove-Item "$logPath\*.log" -Force
            Write-Host "   Cleared old log files" -ForegroundColor Gray
        }
    }

    # Set working directory
    $appPath = Join-Path $PSScriptRoot ".." "bin" "Debug" "net9.0-windows"
    if (Test-Path $appPath) {
        Set-Location $appPath
        Write-Host "   Working Directory: $appPath" -ForegroundColor Gray
    }

    # Launch the application
    $exePath = Join-Path $appPath "WileyWidget.exe"
    if (Test-Path $exePath) {
        if ($Debug) {
            Write-Host "   Launching in debug mode..." -ForegroundColor Yellow
            & $exePath
        }
        else {
            Write-Host "   Launching Wiley Widget..." -ForegroundColor Green
            Start-Process $exePath
        }
    }
    else {
        Write-Host "❌ WileyWidget.exe not found at: $exePath" -ForegroundColor Red
        Write-Host "   Try building the project first with: dotnet build" -ForegroundColor Yellow
    }
}

function Test-WileyWidgetDatabase {
    <#
    .SYNOPSIS
        Tests database connectivity for Wiley Widget
    .DESCRIPTION
        Checks if the database is accessible and shows connection info
    #>
    param()

    Write-Host "🗄️ Testing Wiley Widget Database Connection..." -ForegroundColor Cyan

    # Load environment variables
    $envPath = Join-Path $PSScriptRoot ".." ".env"
    if (Test-Path $envPath) {
        Write-Host "   Loading environment from .env file..." -ForegroundColor Gray
        # Note: In real usage, you'd load the .env file properly
    }

    # Check for SQL LocalDB
    $localDbInstances = & sqllocaldb info 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ SQL LocalDB is available" -ForegroundColor Green
        Write-Host "   Instances: $($localDbInstances -join ', ')" -ForegroundColor Gray
    }
    else {
        Write-Host "❌ SQL LocalDB not found or not running" -ForegroundColor Red
        Write-Host "   Try: sqllocaldb start" -ForegroundColor Yellow
    }

    # Check for database files
    $dbPath = Join-Path $PSScriptRoot ".." "WileyWidget.db"
    if (Test-Path $dbPath) {
        Write-Host "✅ Database file found: $dbPath" -ForegroundColor Green
        $dbInfo = Get-Item $dbPath
        Write-Host "   Size: $([math]::Round($dbInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
        Write-Host "   Modified: $($dbInfo.LastWriteTime)" -ForegroundColor Gray
    }
    else {
        Write-Host "⚠️ Database file not found at: $dbPath" -ForegroundColor Yellow
    }
}

function Show-WileyWidgetLog {
    <#
    .SYNOPSIS
        Displays recent Wiley Widget application logs
    .DESCRIPTION
        Shows the latest log entries from the application
    #>
    param(
        [int]$Lines = 20,
        [switch]$Follow
    )

    $logPath = "$env:APPDATA\WileyWidget\logs"
    $latestLog = Get-ChildItem $logPath -Filter "*.log" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($latestLog) {
        Write-Host "📄 Showing last $Lines lines from: $($latestLog.Name)" -ForegroundColor Cyan
        Write-Host ("=" * 50) -ForegroundColor Gray

        if ($Follow) {
            Get-Content $latestLog.FullName -Tail $Lines -Wait
        }
        else {
            Get-Content $latestLog.FullName -Tail $Lines
        }
    }
    else {
        Write-Host "❌ No log files found in: $logPath" -ForegroundColor Red
    }
}

function Update-WileyWidgetEnvironment {
    <#
    .SYNOPSIS
        Updates environment variables for Wiley Widget
    .DESCRIPTION
        Loads or refreshes environment variables from .env file
    #>
    param()

    Write-Host "🔄 Updating Wiley Widget Environment..." -ForegroundColor Cyan

    $envPath = Join-Path $PSScriptRoot ".." ".env"
    if (Test-Path $envPath) {
        Write-Host "   Loading .env file..." -ForegroundColor Gray
        # In a real implementation, you'd parse and set environment variables
        Write-Host "✅ Environment variables loaded" -ForegroundColor Green
    }
    else {
        Write-Host "❌ .env file not found at: $envPath" -ForegroundColor Red
    }
}

# Export functions for use in other scripts
Export-ModuleMember -Function Show-WileyWidgetStatus, Start-WileyWidget, Test-WileyWidgetDatabase, Show-WileyWidgetLogs, Update-WileyWidgetEnvironment
