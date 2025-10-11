# Production SQL Server Configuration Script
# Applies Microsoft-recommended best practices for SQL Server 2019 production tuning

param(
    [string]$Server = ".\SQLEXPRESS",
    [switch]$ApplyAll,
    [switch]$MemoryOnly,
    [switch]$PerformanceOnly,
    [switch]$SecurityOnly,
    [switch]$BackupOnly
)

$ErrorActionPreference = "Stop"

# Get system information
$totalMemoryGB = (Get-CimInstance -ClassName Win32_ComputerSystem).TotalPhysicalMemory / 1GB
$cpuCores = (Get-CimInstance -ClassName Win32_Processor).NumberOfCores | Measure-Object -Sum | Select-Object -ExpandProperty Sum
$logicalProcessors = (Get-CimInstance -ClassName Win32_Processor).NumberOfLogicalProcessors | Measure-Object -Sum | Select-Object -ExpandProperty Sum

Write-Information "System Information:" -InformationAction Continue
Write-Information "Total Memory: $([math]::Round($totalMemoryGB, 1)) GB" -InformationAction Continue
Write-Information "CPU Cores: $cpuCores" -InformationAction Continue
Write-Information "Logical Processors: $logicalProcessors" -InformationAction Continue
Write-Information "" -InformationAction Continue

function Invoke-SqlConfig {
    param([string]$Query, [string]$Description)
    try {
        Write-Information "Applying: $Description" -InformationAction Continue
        $connectionString = "Server=$Server;Database=master;Trusted_Connection=True;Connection Timeout=30;"
        $connection = New-Object System.Data.SqlClient.SqlConnection
        $connection.ConnectionString = $connectionString
        $connection.Open()

        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = 300

        if ($Query -match "^SELECT") {
            $reader = $command.ExecuteReader()
            while ($reader.Read()) {
                Write-Information "  Result: $($reader[0])" -InformationAction Continue
            }
            $reader.Close()
        }
        else {
            $rowsAffected = $command.ExecuteNonQuery()
            Write-Information "  Success: $rowsAffected rows affected" -InformationAction Continue
        }

        $connection.Close()
        return $true
    }
    catch {
        Write-Information "  Error: $($_.Exception.Message)" -InformationAction Continue
        return $false
    }
}

# Calculate optimal memory settings (75% of total memory, leave room for OS)
$maxServerMemoryMB = [math]::Round($totalMemoryGB * 1024 * 0.75)
$minServerMemoryMB = [math]::Round($maxServerMemoryMB * 0.1)  # 10% of max as minimum

# Calculate optimal MaxDOP (cores per NUMA node, max 8)
$maxDOP = [math]::Min($cpuCores, 8)

Write-Information "Calculated Settings:" -InformationAction Continue
Write-Information "Max Server Memory: $maxServerMemoryMB MB (75% of total)" -InformationAction Continue
Write-Information "Min Server Memory: $minServerMemoryMB MB (10% of max)" -InformationAction Continue
Write-Information "Max Degree of Parallelism: $maxDOP" -InformationAction Continue
Write-Information "" -InformationAction Continue

if ($ApplyAll -or $MemoryOnly) {
    Write-Information "=== MEMORY CONFIGURATION ===" -InformationAction Continue

    # Configure memory settings
    Invoke-SqlConfig "EXEC sp_configure 'show advanced options', 1; RECONFIGURE;" "Enable advanced options"
    Invoke-SqlConfig "EXEC sp_configure 'min server memory (MB)', $minServerMemoryMB; RECONFIGURE;" "Set minimum server memory"
    Invoke-SqlConfig "EXEC sp_configure 'max server memory (MB)', $maxServerMemoryMB; RECONFIGURE;" "Set maximum server memory"

    # Enable Lock Pages in Memory (requires Windows privilege)
    Invoke-SqlConfig "EXEC sp_configure 'locks', 0; RECONFIGURE;" "Configure lock pages in memory"

    Write-Information "" -InformationAction Continue
}

if ($ApplyAll -or $PerformanceOnly) {
    Write-Information "=== PERFORMANCE CONFIGURATION ===" -InformationAction Continue

    # Configure Max Degree of Parallelism
    Invoke-SqlConfig "EXEC sp_configure 'max degree of parallelism', $maxDOP; RECONFIGURE;" "Set max degree of parallelism"

    # Enable Optimize for Ad Hoc Workloads
    Invoke-SqlConfig "EXEC sp_configure 'optimize for ad hoc workloads', 1; RECONFIGURE;" "Enable optimize for ad hoc workloads"

    # Configure Cost Threshold for Parallelism (default 5 is usually good)
    Invoke-SqlConfig "EXEC sp_configure 'cost threshold for parallelism', 50; RECONFIGURE;" "Set cost threshold for parallelism"

    # Configure Max Worker Threads (let SQL Server auto-calculate)
    Invoke-SqlConfig "EXEC sp_configure 'max worker threads', 0; RECONFIGURE;" "Set max worker threads to auto"

    # Configure Access Check Cache (for high-end systems)
    Invoke-SqlConfig "EXEC sp_configure 'access check cache bucket count', 256; RECONFIGURE;" "Set access check cache bucket count"
    Invoke-SqlConfig "EXEC sp_configure 'access check cache quota', 1024; RECONFIGURE;" "Set access check cache quota"

    Write-Information "" -InformationAction Continue
}

if ($ApplyAll -or $SecurityOnly) {
    Write-Information "=== SECURITY CONFIGURATION ===" -InformationAction Continue

    # Enable Common Criteria compliance (only available in Enterprise/Developer editions)
    $result = Invoke-SqlConfig "EXEC sp_configure 'common criteria compliance enabled', 1; RECONFIGURE;" "Enable common criteria compliance"
    if (-not $result) {
        Write-Information "  Note: Common Criteria compliance not available in Express edition" -InformationAction Continue
    }

    # Configure server audit settings (if needed)
    Invoke-SqlConfig "EXEC sp_configure 'c2 audit mode', 0; RECONFIGURE;" "Configure C2 audit mode (disabled for performance)"

    Write-Information "" -InformationAction Continue
}

if ($ApplyAll -or $BackupOnly) {
    Write-Information "=== BACKUP CONFIGURATION ===" -InformationAction Continue

    # Configure backup compression (only available in Standard/Enterprise editions)
    $result = Invoke-SqlConfig "EXEC sp_configure 'backup compression default', 1; RECONFIGURE;" "Enable backup compression by default"
    if (-not $result) {
        Write-Information "  Note: Backup compression not available in Express edition" -InformationAction Continue
    }

    Write-Information "" -InformationAction Continue
}

# Database-specific configurations
Write-Information "=== DATABASE CONFIGURATIONS ===" -InformationAction Continue

# Configure WileyWidgetDev database
Invoke-SqlConfig "ALTER DATABASE [WileyWidgetDev] SET AUTO_SHRINK OFF, AUTO_CLOSE OFF;" "Configure WileyWidgetDev database options"

# Enable Query Store on WileyWidgetDev
Invoke-SqlConfig "ALTER DATABASE [WileyWidgetDev] SET QUERY_STORE = ON (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900);" "Enable Query Store on WileyWidgetDev"

# Note: tempdb configuration is handled automatically by SQL Server

# Show current configuration
Write-Information "=== CURRENT CONFIGURATION SUMMARY ===" -InformationAction Continue

$configQuery = @"
SELECT
    'Max Server Memory (MB)' as Setting,
    CAST(value_in_use as int) as Value
FROM sys.configurations WHERE name = 'max server memory (MB)'
UNION ALL
SELECT
    'Min Server Memory (MB)',
    CAST(value_in_use as int)
FROM sys.configurations WHERE name = 'min server memory (MB)'
UNION ALL
SELECT
    'Max Degree of Parallelism',
    CAST(value_in_use as int)
FROM sys.configurations WHERE name = 'max degree of parallelism'
UNION ALL
SELECT
    'Optimize for Ad Hoc Workloads',
    CAST(value_in_use as int)
FROM sys.configurations WHERE name = 'optimize for ad hoc workloads'
UNION ALL
SELECT
    'Backup Compression Default',
    CAST(value_in_use as int)
FROM sys.configurations WHERE name = 'backup compression default'
"@

Invoke-SqlConfig $configQuery "Display current configuration"

Write-Information "" -InformationAction Continue
Write-Information "=== RECOMMENDED NEXT STEPS ===" -InformationAction Continue
Write-Information "1. Restart SQL Server service to apply all changes" -InformationAction Continue
Write-Information "2. Enable 'Lock Pages in Memory' privilege for SQL Server service account in Windows" -InformationAction Continue
Write-Information "3. Grant 'Perform Volume Maintenance Tasks' privilege for instant file initialization" -InformationAction Continue
Write-Information "4. Monitor performance with the monitoring script" -InformationAction Continue
Write-Information "5. Consider enabling trace flags if needed (TF 7412 for query profiling)" -InformationAction Continue
Write-Information "" -InformationAction Continue
Write-Information "Configuration completed!" -InformationAction Continue
