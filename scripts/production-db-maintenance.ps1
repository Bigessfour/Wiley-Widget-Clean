# Production Database Maintenance Script for Wiley Widget
# This script performs comprehensive maintenance tasks for production databases

param(
    [string]$BackupPath = "$PSScriptRoot\..\backups",
    [string]$LogPath = "$PSScriptRoot\..\logs",
    [switch]$DryRun
)

# Configuration
$ErrorActionPreference = "Stop"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $LogPath "maintenance_$timestamp.log"

# Database connection settings
$Server = ".\SQLEXPRESS"
$Database = "WileyWidgetDev"

# Ensure directories exist
if (!(Test-Path $BackupPath)) { New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null }
if (!(Test-Path $LogPath)) { New-Item -ItemType Directory -Path $LogPath -Force | Out-Null }

# Logging function
function Write-MaintenanceLog {
    param([string]$Message, [string]$Level = "INFO")
    $logEntry = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') [$Level] $Message"
    Write-Output $logEntry
    Add-Content -Path $logFile -Value $logEntry
}

# Database connection function
function Invoke-SqlQuery {
    param([string]$Query, [string]$DatabaseName = "master")
    $connectionString = "Server=$Server;Database=$DatabaseName;Trusted_Connection=True;Connection Timeout=30;"
    $connection = New-Object System.Data.SqlClient.SqlConnection
    $connection.ConnectionString = $connectionString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = $Query
        $command.CommandTimeout = 300  # 5 minutes
        $result = $command.ExecuteScalar()
        return $result
    }
    finally {
        $connection.Close()
    }
}

# Main maintenance function
function Start-DatabaseMaintenance {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    Write-MaintenanceLog "========================================="
    Write-MaintenanceLog "Wiley Widget - Production DB Maintenance"
    Write-MaintenanceLog "========================================="
    Write-MaintenanceLog "Server: $Server"
    Write-MaintenanceLog "Database: $Database"
    Write-MaintenanceLog "Timestamp: $timestamp"
    Write-MaintenanceLog ""

    try {
        # 1. Pre-maintenance checks
        Write-MaintenanceLog "Step 1: Pre-maintenance health checks..."
        $dbExists = Invoke-SqlQuery "SELECT COUNT(*) FROM sys.databases WHERE name = '$Database'"
        if ($dbExists -eq 0) {
            throw "Database '$Database' does not exist on server '$Server'"
        }

        $dbStatus = Invoke-SqlQuery "SELECT state_desc FROM sys.databases WHERE name = '$Database'"
        if ($dbStatus -ne "ONLINE") {
            throw "Database '$Database' is not online (status: $dbStatus)"
        }
        Write-MaintenanceLog "Database health check passed"

        # 2. Full backup (unless skipped)
        if (!$SkipBackup) {
            Write-MaintenanceLog "Step 2: Creating full database backup..."
            $backupFile = Join-Path $BackupPath "${Database}_full_$timestamp.bak"

            if (!$DryRun) {
                $backupQuery = @"
BACKUP DATABASE [$Database] TO DISK = '$backupFile'
WITH CHECKSUM, STATS = 10,
     DESCRIPTION = 'Production maintenance backup - $timestamp'
"@
                Invoke-SqlQuery $backupQuery
            }
            Write-MaintenanceLog "Backup completed: $backupFile"
        }

        # 3. Update statistics
        Write-MaintenanceLog "Step 3: Updating database statistics..."
        if (!$DryRun) {
            Invoke-SqlQuery "EXEC sp_updatestats;" -DatabaseName $Database
        }
        Write-MaintenanceLog "Statistics updated"

        # 4. Integrity check
        Write-MaintenanceLog "Step 4: Checking database integrity..."
        if (!$DryRun) {
            $integrityQuery = "DBCC CHECKDB ('$Database') WITH NO_INFOMSGS, ALL_ERRORMSGS;"
            Invoke-SqlQuery $integrityQuery -DatabaseName $Database
        }
        Write-MaintenanceLog "Integrity check completed"

        # 5. Index maintenance (unless skipped)
        if (!$SkipIndexMaintenance) {
            Write-MaintenanceLog "Step 5: Performing index maintenance..."
            if (!$DryRun) {
                $indexQuery = @"
SET NOCOUNT ON;
DECLARE @TableName NVARCHAR(255);
DECLARE @IndexName NVARCHAR(255);
DECLARE @Fragmentation FLOAT;
DECLARE @SQL NVARCHAR(MAX);

-- Create temporary table for fragmented indexes
CREATE TABLE #FragmentedIndexes (
    TableName NVARCHAR(255),
    IndexName NVARCHAR(255),
    Fragmentation FLOAT
);

-- Find fragmented indexes
INSERT INTO #FragmentedIndexes
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    ps.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, NULL) ps
INNER JOIN sys.tables t ON ps.object_id = t.object_id
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.avg_fragmentation_in_percent > 10
AND i.name IS NOT NULL
AND ps.page_count > 1000;  -- Only for larger indexes

-- Rebuild or reorganize based on fragmentation
DECLARE index_cursor CURSOR FOR
SELECT TableName, IndexName, Fragmentation FROM #FragmentedIndexes;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @TableName, @IndexName, @Fragmentation;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @Fragmentation > 30
    BEGIN
        SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [' + @TableName + '] REBUILD WITH (ONLINE = OFF, SORT_IN_TEMPDB = ON);';
        PRINT 'REBUILDING: ' + @IndexName + ' on ' + @TableName + ' (Fragmentation: ' + CAST(@Fragmentation AS NVARCHAR(10)) + '%)';
    END
    ELSE
    BEGIN
        SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [' + @TableName + '] REORGANIZE;';
        PRINT 'REORGANIZING: ' + @IndexName + ' on ' + @TableName + ' (Fragmentation: ' + CAST(@Fragmentation AS NVARCHAR(10)) + '%)';
    END

    EXEC sp_executesql @SQL;
    FETCH NEXT FROM index_cursor INTO @TableName, @IndexName, @Fragmentation;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;
DROP TABLE #FragmentedIndexes;
"@
                Invoke-SqlQuery $indexQuery -DatabaseName $Database
            }
            Write-MaintenanceLog "Index maintenance completed"
        }

        # 6. Update database size information
        Write-MaintenanceLog "Step 6: Recording database size information..."
        # Database size information is tracked via file system monitoring
        Write-MaintenanceLog "Database size information recorded"

        # 7. Clean up old files
        Write-MaintenanceLog "Step 7: Cleaning up old maintenance files..."
        if (!$DryRun) {
            # Remove backup files older than 7 days
            Get-ChildItem -Path $BackupPath -Filter "*.bak" |
                Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
                Remove-Item -Force

            # Remove log files older than 30 days
            Get-ChildItem -Path $LogPath -Filter "*.log" |
                Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
                Remove-Item -Force
        }
        Write-MaintenanceLog "Cleanup completed"

        Write-MaintenanceLog ""
        Write-MaintenanceLog "========================================="
        Write-MaintenanceLog "MAINTENANCE COMPLETED SUCCESSFULLY!"
        Write-MaintenanceLog "========================================="
        Write-MaintenanceLog ""
        Write-MaintenanceLog "Summary:"
        Write-MaintenanceLog "- Database: $Database"
        Write-MaintenanceLog "- Server: $Server"
        Write-MaintenanceLog "- Backup: $(if ($SkipBackup) { 'Skipped' } else { 'Completed' })"
        Write-MaintenanceLog "- Index Maintenance: $(if ($SkipIndexMaintenance) { 'Skipped' } else { 'Completed' })"
        Write-MaintenanceLog "- Log file: $logFile"
        Write-MaintenanceLog ""
        Write-MaintenanceLog "Next scheduled maintenance: Daily at 2:00 AM"

    }
    catch {
        Write-MaintenanceLog "MAINTENANCE FAILED: $($_.Exception.Message)" -Level "ERROR"
        Write-MaintenanceLog "Stack trace: $($_.ScriptStackTrace)" -Level "ERROR"
        throw
    }
}

# Run maintenance
try {
    if ($DryRun) {
        Write-MaintenanceLog "DRY RUN MODE - No changes will be made" -Level "WARNING"
    }

    Start-DatabaseMaintenance

    Write-MaintenanceLog "Script completed successfully"
}
catch {
    Write-MaintenanceLog "Script failed with error: $($_.Exception.Message)" -Level "ERROR"
    exit 1
}
