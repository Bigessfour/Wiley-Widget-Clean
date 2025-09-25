#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated Azure SQL Database Monitoring Scheduler

.DESCRIPTION
    Runs periodic checks on Azure SQL Database Basic tier usage and sends alerts when upgrade needed.
    Can be run manually or scheduled with Windows Task Scheduler.

.PARAMETER RunOnce
    Run monitoring check once and exit

.PARAMETER SetupSchedule
    Configure Windows Task Scheduler to run monitoring daily

.PARAMETER EmailAlert
    Email address to send alerts to (optional)
#>

param(
    [switch]$RunOnce,
    [switch]$SetupSchedule,
    [string]$EmailAlert = ""
)

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$MONITORING_SCRIPT = Join-Path $SCRIPT_DIR "azure-sql-monitoring.ps1"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"

    # Write to console
    switch ($Level) {
        "ERROR" { Write-Warning $logEntry }
        "WARN" { Write-Warning $logEntry }
        default { Write-Information $logEntry -InformationAction Continue }
    }

    # Write to log file
    $logFile = Join-Path $SCRIPT_DIR "sql-monitoring.log"
    $logEntry | Out-File -FilePath $logFile -Append -Encoding UTF8
}

function Test-MonitoringPrerequisite {
    # Check if main monitoring script exists
    if (-not (Test-Path $MONITORING_SCRIPT)) {
        Write-Log "Monitoring script not found: $MONITORING_SCRIPT" "ERROR"
        return $false
    }

    # Check Azure CLI
    try {
        $null = az account show 2>$null
        Write-Log "Azure CLI authentication verified" "INFO"
        return $true
    }
    catch {
        Write-Log "Azure CLI not authenticated. Run 'az login'" "ERROR"
        return $false
    }
}

function Invoke-MonitoringCheck {
    Write-Log "Starting Azure SQL Database monitoring check" "INFO"

    try {
        # Run the monitoring script
        $results = & $MONITORING_SCRIPT -CheckLimits

        # Check if results file was created
        $resultsFile = Join-Path $SCRIPT_DIR "sql-monitoring-results.json"
        if (Test-Path $resultsFile) {
            $data = Get-Content $resultsFile | ConvertFrom-Json

            # Check if upgrade is needed
            if ($data.Analysis.NeedsUpgrade -or $data.Analysis.Warnings.Count -gt 0) {
                $alertMessage = "⚠️  Azure SQL Database approaching Basic tier limits!"
                Write-Log $alertMessage "WARN"

                if ($data.Analysis.Critical.Count -gt 0) {
                    Write-Log "CRITICAL: $($data.Analysis.Critical -join ', ')" "ERROR"
                    Send-UpgradeAlert -Level "CRITICAL" -Issues $data.Analysis.Critical
                }

                if ($data.Analysis.Warnings.Count -gt 0) {
                    Write-Log "WARNING: $($data.Analysis.Warnings -join ', ')" "WARN"
                    Send-UpgradeAlert -Level "WARNING" -Issues $data.Analysis.Warnings
                }
            }
            else {
                Write-Log "✅ Basic tier is operating within normal limits" "INFO"
            }
        }

        Write-Log "Monitoring check completed successfully" "INFO"
        return $true
    }
    catch {
        Write-Log "Error during monitoring check: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

function Send-UpgradeAlert {
    param(
        [string]$Level,
        [array]$Issues
    )

    $alertBody = @"
Azure SQL Database Alert - Upgrade Needed

Database: WileyWidgetDB
Current Tier: Basic (5 DTU, 2GB)
Alert Level: $Level

Issues Detected:
$($Issues | ForEach-Object { "• $_" } | Out-String)

Recommended Actions:
1. Review current usage with: .\scripts\azure-sql-monitoring.ps1 -CheckLimits
2. See upgrade options with: .\scripts\azure-sql-monitoring.ps1 -RecommendUpgrade
3. Consider upgrading to Standard S0 (10 DTU, 250GB) for +$10/month

Current costs: ~$5/month
Next tier costs: ~$15/month (Standard S0)

Generated: $(Get-Date)
"@

    Write-Log "ALERT: $alertBody" "WARN"

    # Save alert to file for manual review
    $alertFile = Join-Path $SCRIPT_DIR "sql-upgrade-alert.txt"
    $alertBody | Out-File -FilePath $alertFile -Encoding UTF8
    Write-Log "Alert details saved to: $alertFile" "INFO"

    # If email is configured, you could send email here
    if ($EmailAlert) {
        Write-Log "Email alerts not yet implemented. Alert saved to file." "INFO"
    }
}

function Set-MonitoringSchedule {
    Write-Log "Setting up Windows Task Scheduler for daily monitoring" "INFO"

    $taskName = "AzureSQLMonitoring"
    $scriptPath = $MyInvocation.MyCommand.Path

    try {
        # Create scheduled task to run daily at 9 AM
        $action = New-ScheduledTaskAction -Execute "pwsh.exe" -Argument "-File `"$scriptPath`" -RunOnce"
        $trigger = New-ScheduledTaskTrigger -Daily -At "9:00AM"
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

        Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description "Monitor Azure SQL Database usage and alert when upgrade needed"

        Write-Log "✅ Scheduled task '$taskName' created successfully" "INFO"
        Write-Log "Task will run daily at 9:00 AM" "INFO"

        return $true
    }
    catch {
        Write-Log "Failed to create scheduled task: $($_.Exception.Message)" "ERROR"
        return $false
    }
}

# Main execution
function Main {
    Write-Log "Azure SQL Database Monitor Scheduler Starting" "INFO"

    if (-not (Test-MonitoringPrerequisites)) {
        Write-Log "Prerequisites not met. Exiting." "ERROR"
        exit 1
    }

    if ($SetupSchedule) {
        if (Set-MonitoringSchedule) {
            Write-Log "Monitoring schedule configured successfully" "INFO"
        }
        else {
            Write-Log "Failed to configure monitoring schedule" "ERROR"
            exit 1
        }
        return
    }

    if ($RunOnce) {
        $success = Invoke-MonitoringCheck
        if (-not $success) {
            exit 1
        }
        return
    }

    # Default: Show usage
    Write-Information "Azure SQL Database Monitoring Scheduler" -InformationAction Continue
    Write-Information "Usage:" -InformationAction Continue
    Write-Information "  -RunOnce       : Run monitoring check once" -InformationAction Continue
    Write-Information "  -SetupSchedule : Configure daily scheduled monitoring" -InformationAction Continue
    Write-Information "  -EmailAlert    : Email address for alerts (future feature)" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "Examples:" -InformationAction Continue
    Write-Information "  ./azure-sql-scheduler.ps1 -RunOnce" -InformationAction Continue
    Write-Information "  ./azure-sql-scheduler.ps1 -SetupSchedule" -InformationAction Continue
}

# Run main function
Main
