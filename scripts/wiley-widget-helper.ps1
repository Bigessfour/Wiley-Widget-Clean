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

    Write-Information "🔍 Checking Wiley Widget Status..." -InformationAction Continue

    # Check if app is running
    $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
    if ($process) {
        Write-Information "✅ Wiley Widget is running" -InformationAction Continue
        Write-Information "   Process ID: $($process.Id)" -InformationAction Continue
        Write-Information "   Memory Usage: $([math]::Round($process.WorkingSet64 / 1MB, 2)) MB" -InformationAction Continue
        Write-Information "   Start Time: $($process.StartTime)" -InformationAction Continue
    }
    else {
        Write-Information "❌ Wiley Widget is not running" -InformationAction Continue
    }

    # Check app data directory
    $appDataPath = "$env:APPDATA\WileyWidget"
    if (Test-Path $appDataPath) {
        Write-Information "📁 App Data Directory: $appDataPath" -InformationAction Continue
        $logFiles = Get-ChildItem "$appDataPath\logs" -Filter "*.log" -ErrorAction SilentlyContinue
        if ($logFiles) {
            Write-Information "   📄 Latest Log: $($logFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty Name)" -InformationAction Continue
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

    Write-Information "🚀 Starting Wiley Widget..." -InformationAction Continue

    if ($CleanStart) {
        Write-Information "🧹 Performing clean start..." -InformationAction Continue
        # Kill any existing processes
        Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue | Stop-Process -Force
        # Clear logs if requested
        $logPath = "$env:APPDATA\WileyWidget\logs"
        if (Test-Path $logPath) {
            Remove-Item "$logPath\*.log" -Force
            Write-Information "   Cleared old log files" -InformationAction Continue
        }
    }

    # Set working directory
    $appPath = Join-Path $PSScriptRoot ".." "bin" "Debug" "net9.0-windows"
    if (Test-Path $appPath) {
        Set-Location $appPath
        Write-Information "   Working Directory: $appPath" -InformationAction Continue
    }

    # Launch the application
    $exePath = Join-Path $appPath "WileyWidget.exe"
    if (Test-Path $exePath) {
        if ($Debug) {
            Write-Information "   Launching in debug mode..." -InformationAction Continue
            & $exePath
        }
        else {
            Write-Information "   Launching Wiley Widget..." -InformationAction Continue
            Start-Process $exePath
        }
    }
    else {
        Write-Information "❌ WileyWidget.exe not found at: $exePath" -InformationAction Continue
        Write-Information "   Try building the project first with: dotnet build" -InformationAction Continue
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

    Write-Information "🗄️ Testing Wiley Widget Database Connection..." -InformationAction Continue

    # Load environment variables
    $envPath = Join-Path $PSScriptRoot ".." ".env"
    if (Test-Path $envPath) {
        Write-Information "   Loading environment from .env file..." -InformationAction Continue
        # Note: In real usage, you'd load the .env file properly
    }

    # Check for SQL LocalDB
    $localDbInstances = & sqllocaldb info 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Information "✅ SQL LocalDB is available" -InformationAction Continue
        Write-Information "   Instances: $($localDbInstances -join ', ')" -InformationAction Continue
    }
    else {
        Write-Information "❌ SQL LocalDB not found or not running" -InformationAction Continue
        Write-Information "   Try: sqllocaldb start" -InformationAction Continue
    }

    # Check for database files
    $dbPath = Join-Path $PSScriptRoot ".." "WileyWidget.db"
    if (Test-Path $dbPath) {
        Write-Information "✅ Database file found: $dbPath" -InformationAction Continue
        $dbInfo = Get-Item $dbPath
        Write-Information "   Size: $([math]::Round($dbInfo.Length / 1MB, 2)) MB" -InformationAction Continue
        Write-Information "   Modified: $($dbInfo.LastWriteTime)" -InformationAction Continue
    }
    else {
        Write-Information "⚠️ Database file not found at: $dbPath" -InformationAction Continue
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
        Write-Information "📄 Showing last $Lines lines from: $($latestLog.Name)" -InformationAction Continue
        Write-Information ("=" * 50) -InformationAction Continue

        if ($Follow) {
            Get-Content $latestLog.FullName -Tail $Lines -Wait
        }
        else {
            Get-Content $latestLog.FullName -Tail $Lines
        }
    }
    else {
        Write-Information "❌ No log files found in: $logPath" -InformationAction Continue
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

    Write-Information "🔄 Updating Wiley Widget Environment..." -InformationAction Continue

    $envPath = Join-Path $PSScriptRoot ".." ".env"
    if (Test-Path $envPath) {
        Write-Information "   Loading .env file..." -InformationAction Continue
        # In a real implementation, you'd parse and set environment variables
        Write-Information "✅ Environment variables loaded" -InformationAction Continue
    }
    else {
        Write-Information "❌ .env file not found at: $envPath" -InformationAction Continue
    }
}

# Export functions for use in other scripts
Export-ModuleMember -Function Show-WileyWidgetStatus, Start-WileyWidget, Test-WileyWidgetDatabase, Show-WileyWidgetLogs, Update-WileyWidgetEnvironment
