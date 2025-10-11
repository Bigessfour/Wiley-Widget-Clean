# Production Database Monitoring Script for Wiley Widget
# Monitors database health, performance, and security metrics

param(
    [string]$Server = ".\SQLEXPRESS",
    [string]$Database = "WileyWidgetDev",
    [string]$OutputPath = "$PSScriptRoot\..\logs",
    [int]$AlertThresholdPercent = 80,
    [switch]$Detailed,
    [switch]$JsonOutput
)

$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Set variables from parameters
$script:Server = $Server
$script:Database = $Database
$script:OutputPath = $OutputPath
$script:AlertThresholdPercent = $AlertThresholdPercent
$script:Detailed = $Detailed
$script:JsonOutput = $JsonOutput

# Ensure output directory exists
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# Output file
$outputFile = Join-Path $OutputPath "db_monitor_$timestamp.json"
$logFile = Join-Path $OutputPath "db_monitor_$timestamp.log"

# Monitoring data structure
$monitorData = @{
    Timestamp   = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    Server      = $Server
    Database    = $Database
    Health      = @{}
    Performance = @{}
    Security    = @{}
    Alerts      = @()
}

# Logging function
function Write-MonitorLog {
    param([string]$Message, [string]$Level = "INFO")
    $logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Information $logEntry
    Add-Content -Path $logFile -Value $logEntry
}

# Database query function
function Invoke-MonitorQuery {
    param([string]$Query, [string]$DatabaseName = "master")
    try {
        $connectionString = "Server=$script:Server;Database=$DatabaseName;Trusted_Connection=True;Connection Timeout=30;"
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString

        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = 60

        $reader = $command.ExecuteReader()
        $results = @()

        while ($reader.Read()) {
            $row = @{}
            for ($i = 0; $i -lt $reader.FieldCount; $i++) {
                $row[$reader.GetName($i)] = $reader.GetValue($i)
            }
            $results += $row
        }

        $reader.Close()
        return , $results
    }
    catch {
        Write-MonitorLog "Query failed: $($_.Exception.Message)" -Level "ERROR"
        throw
    }
    finally {
        if ($connection -and $connection.State -eq 'Open') {
            $connection.Close()
        }
    }
}

# Alert function
function Add-MonitorAlert {
    param([string]$Type, [string]$Message, [string]$Severity = "Warning")
    $alert = @{
        Type      = $Type
        Message   = $Message
        Severity  = $Severity
        Timestamp = Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ"
    }
    $monitorData.Alerts += $alert
    Write-MonitorLog "$Type Alert ($Severity): $Message" -Level $Severity
}

# Main monitoring function
function Start-DatabaseMonitoring {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()
    Write-MonitorLog "Starting database monitoring for $Database on $Server"

    try {
        # 1. Database Health Checks
        Write-MonitorLog "Checking database health..."
        $healthQuery = @"
SELECT
    name,
    state_desc as Status,
    recovery_model_desc as RecoveryModel,
    log_reuse_wait_desc as LogReuseWait,
    is_auto_create_stats_on,
    is_auto_update_stats_on,
    is_auto_create_stats_incremental_on
FROM sys.databases
WHERE name = '$Database'
"@
        $healthData = Invoke-MonitorQuery $healthQuery
        Write-MonitorLog "Health data type: $($healthData.GetType().FullName), count: $($healthData.Count)" -Level "DEBUG"
        if ($healthData.Count -gt 0) {
            Write-MonitorLog "First item type: $($healthData[0].GetType().FullName)" -Level "DEBUG"
            Write-MonitorLog "First item: $($healthData[0] | ConvertTo-Json)" -Level "DEBUG"
        }
        if ($null -eq $healthData -or $healthData.Count -eq 0) {
            throw "Health query returned no data"
        }

        $db = $healthData[0]
        $monitorData.Health = @{
            Name             = $db.name
            Status           = $db.Status
            RecoveryModel    = $db.RecoveryModel
            LogReuseWait     = $db.LogReuseWait
            AutoStats        = $db.is_auto_create_stats_on
            AutoStatsUpdate  = $db.is_auto_update_stats_on
            IncrementalStats = $db.is_auto_create_stats_incremental_on
        }

        # Check for issues
        if ($db.Status -ne "ONLINE") {
            Add-MonitorAlert "Health" "Database is not online (Status: $($db.Status))" "Critical"
        }
        if ($db.LogReuseWait -ne "NOTHING") {
            Add-MonitorAlert "Health" "Log reuse wait detected: $($db.LogReuseWait)" "Warning"
        }

        # 2. Performance Metrics
        Write-MonitorLog "Collecting performance metrics..."
        $perfQuery = @"
SELECT
    (SELECT COUNT(*) FROM sys.dm_exec_connections WHERE session_id > 50) as ActiveConnections,
    (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50) as ActiveRequests,
    (SELECT AVG(wait_time_ms) FROM sys.dm_os_wait_stats WHERE wait_type NOT LIKE '%SLEEP%') as AvgWaitTime,
    (SELECT physical_memory_in_use_kb / 1024 FROM sys.dm_os_process_memory) as MemoryUsageMB,
    (SELECT cpu_count FROM sys.dm_os_sys_info) as CpuCount,
    (SELECT sqlserver_start_time FROM sys.dm_os_sys_info) as SqlStartTime
"@
        $perfData = Invoke-MonitorQuery $perfQuery
        if ($perfData.Count -gt 0) {
            $perf = $perfData[0]
            $monitorData.Performance = @{
                ActiveConnections = [int]$perf.ActiveConnections
                ActiveRequests    = [int]$perf.ActiveRequests
                AvgWaitTime       = [int]$perf.AvgWaitTime
                MemoryUsageMB     = [int]$perf.MemoryUsageMB
                CpuCount          = [int]$perf.CpuCount
                SqlStartTime      = $perf.SqlStartTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                Uptime            = [math]::Round((New-TimeSpan -Start $perf.SqlStartTime -End (Get-Date)).TotalHours, 1)
            }

            # Performance alerts
            if ($perf.ActiveConnections -gt ($AlertThresholdPercent / 10)) {
                Add-MonitorAlert "Performance" "High connection count: $($perf.ActiveConnections) (threshold: $([math]::Round($AlertThresholdPercent / 10)))" "Warning"
            }
            if ($perf.AvgWaitTime -gt ($AlertThresholdPercent * 10)) {
                Add-MonitorAlert "Performance" "High average wait time: $($perf.AvgWaitTime)ms (threshold: $($AlertThresholdPercent * 10)ms)" "Warning"
            }
        }

        # 3. Security Checks
        Write-MonitorLog "Performing security checks..."
        $securityQuery = @"
SELECT
    (SELECT COUNT(*) FROM sys.sql_logins WHERE is_disabled = 0) as EnabledSqlLogins,
    (SELECT COUNT(*) FROM sys.server_principals WHERE type = 'U' AND is_disabled = 0) as EnabledWindowsLogins,
    (SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.configurations WHERE name = 'common criteria compliance enabled' AND value_in_use = 1) THEN 1 ELSE 0 END) as CommonCriteriaEnabled,
    (SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.configurations WHERE name = 'c2 audit mode' AND value_in_use = 1) THEN 1 ELSE 0 END) as C2AuditEnabled
"@
        $securityData = Invoke-MonitorQuery $securityQuery
        if ($securityData.Count -gt 0) {
            $sec = $securityData[0]
            $monitorData.Security = @{
                EnabledSqlLogins      = [int]$sec.EnabledSqlLogins
                EnabledWindowsLogins  = [int]$sec.EnabledWindowsLogins
                CommonCriteriaEnabled = [bool]$sec.CommonCriteriaEnabled
                C2AuditEnabled        = [bool]$sec.C2AuditEnabled
            }

            # Security recommendations
            if (!$sec.CommonCriteriaEnabled) {
                Add-MonitorAlert "Security" "Common Criteria compliance is not enabled" "Info"
            }
        }

        # 4. Detailed metrics (if requested)
        if ($Detailed) {
            Write-MonitorLog "Collecting detailed metrics..."

            # Top queries by CPU
            $topQueriesQuery = @"
SELECT TOP 10
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset
            WHEN -1 THEN DATALENGTH(st.text)
            ELSE qs.statement_end_offset
        END - qs.statement_start_offset)/2)+1) as QueryText,
    qs.execution_count,
    qs.total_worker_time/1000 as TotalCpuTimeMs,
    qs.total_elapsed_time/1000 as TotalElapsedTimeMs,
    qs.total_logical_reads,
    qs.total_logical_writes
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY qs.total_worker_time DESC
"@
            $topQueries = Invoke-MonitorQuery $topQueriesQuery -DatabaseName $Database
            $monitorData.Performance.TopQueries = @()
            foreach ($row in $topQueries) {
                $monitorData.Performance.TopQueries += @{
                    QueryText          = $row.QueryText.ToString().Trim()
                    ExecutionCount     = [int]$row.execution_count
                    TotalCpuTimeMs     = [int]$row.TotalCpuTimeMs
                    TotalElapsedTimeMs = [int]$row.TotalElapsedTimeMs
                    TotalLogicalReads  = [int]$row.total_logical_reads
                    TotalLogicalWrites = [int]$row.total_logical_writes
                }
            }

            # Database file sizes
            $fileSizeQuery = @"
SELECT
    name,
    type_desc as Type,
    size * 8 / 1024 as SizeMB,
    max_size * 8 / 1024 as MaxSizeMB,
    growth * 8 / 1024 as GrowthMB
FROM sys.database_files
"@
            $fileSizes = Invoke-MonitorQuery $fileSizeQuery -DatabaseName $Database
            $monitorData.Health.FileSizes = @()
            foreach ($row in $fileSizes) {
                $monitorData.Health.FileSizes += @{
                    Name      = $row.name
                    Type      = $row.Type
                    SizeMB    = [int]$row.SizeMB
                    MaxSizeMB = [int]$row.MaxSizeMB
                    GrowthMB  = [int]$row.GrowthMB
                }
            }
        }

        # 5. Generate summary
        $alertCount = $monitorData.Alerts.Count
        $criticalAlerts = @($monitorData.Alerts | Where-Object { $_.Severity -eq "Critical" }).Count
        $warningAlerts = @($monitorData.Alerts | Where-Object { $_.Severity -eq "Warning" }).Count

        Write-MonitorLog "Monitoring completed. Alerts: $alertCount (Critical: $criticalAlerts, Warning: $warningAlerts)"

        # Output results
        if ($JsonOutput) {
            $monitorData | ConvertTo-Json -Depth 10 | Set-Content -Path $outputFile -Encoding UTF8
            Write-MonitorLog "Results saved to: $outputFile"
        }

        return $monitorData

    }
    catch {
        Write-MonitorLog "Monitoring failed: $($_.Exception.Message)" -Level "ERROR"
        throw
    }
}

# Execute monitoring
try {
    $results = Start-DatabaseMonitoring

    # Display summary
    Write-Output "`nDatabase Monitoring Summary:"
    Write-Output "=========================="
    Write-Output "Server: $($results.Server)"
    Write-Output "Database: $($results.Database)"
    Write-Output "Status: $($results.Health.Status)"
    Write-Output "Active Connections: $($results.Performance.ActiveConnections)"
    Write-Output "Memory Usage: $($results.Performance.MemoryUsageMB) MB"
    Write-Output "Alerts: $($results.Alerts.Count)"
    Write-Output "Log file: $logFile"

    if ($results.Alerts.Count -gt 0) {
        Write-Output "`nAlerts:"
        foreach ($alert in $results.Alerts) {
            Write-Output "- [$($alert.Severity)] $($alert.Type): $($alert.Message)"
        }
    }

    Write-Output "`nMonitoring completed successfully"

}
catch {
    Write-MonitorLog "Script execution failed: $($_.Exception.Message)" -Level "ERROR"
    Write-Output "Monitoring failed. Check log file: $logFile"
    exit 1
}
