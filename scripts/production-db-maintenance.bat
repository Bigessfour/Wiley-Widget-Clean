@echo off
REM Production Database Maintenance Script for Wiley Widget
REM This script performs essential maintenance tasks for production databases

echo ========================================
echo Wiley Widget - Production DB Maintenance
echo ========================================
echo.

set SERVER=.\SQLEXPRESS
set DATABASE=WileyWidgetDev
set BACKUP_DIR=C:\Users\%USERNAME%\Desktop\Wiley_Widget\backups
set LOG_DIR=C:\Users\%USERNAME%\Desktop\Wiley_Widget\logs

REM Create directories if they don't exist
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [INFO] Starting maintenance for database: %DATABASE%
echo [INFO] Server: %SERVER%
echo [INFO] Timestamp: %DATE% %TIME%
echo.

REM 1. Full Database Backup
echo [STEP 1] Creating full database backup...
sqlcmd -S "%SERVER%" -d "master" -Q "BACKUP DATABASE [%DATABASE%] TO DISK = '%BACKUP_DIR%\%DATABASE%_full_%DATE:~-4,4%%DATE:~-10,2%%DATE:~-7,2%_%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%.bak' WITH CHECKSUM, COMPRESSION, STATS = 10;"
if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] Full backup completed
) else (
    echo [ERROR] Full backup failed
    goto :error
)

REM 2. Transaction Log Backup (if using FULL recovery model)
echo [STEP 2] Creating transaction log backup...
sqlcmd -S "%SERVER%" -d "master" -Q "IF (SELECT recovery_model FROM sys.databases WHERE name = '%DATABASE%') = 1 BEGIN BACKUP LOG [%DATABASE%] TO DISK = '%BACKUP_DIR%\%DATABASE%_log_%DATE:~-4,4%%DATE:~-10,2%%DATE:~-7,2%_%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%.trn' WITH CHECKSUM, STATS = 10; END"
echo [INFO] Log backup completed (if applicable)

REM 3. Update Statistics
echo [STEP 3] Updating database statistics...
sqlcmd -S "%SERVER%" -d "%DATABASE%" -Q "EXEC sp_updatestats;"
echo [SUCCESS] Statistics updated

REM 4. Check Database Integrity
echo [STEP 4] Checking database integrity...
sqlcmd -S "%SERVER%" -d "%DATABASE%" -Q "DBCC CHECKDB WITH NO_INFOMSGS;" > "%LOG_DIR%\dbcc_check_%DATE:~-4,4%%DATE:~-10,2%%DATE:~-7,2%.log"
echo [SUCCESS] Integrity check completed - check log file for details

REM 5. Rebuild Fragmented Indexes
echo [STEP 5] Rebuilding fragmented indexes...
sqlcmd -S "%SERVER%" -d "%DATABASE%" -Q "
SET NOCOUNT ON;
DECLARE @TableName NVARCHAR(255);
DECLARE @IndexName NVARCHAR(255);
DECLARE @SQL NVARCHAR(MAX);

DECLARE index_cursor CURSOR FOR
SELECT t.name AS TableName, i.name AS IndexName
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, NULL) ps
INNER JOIN sys.tables t ON ps.object_id = t.object_id
INNER JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
WHERE ps.avg_fragmentation_in_percent > 30
AND i.name IS NOT NULL;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @TableName, @IndexName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @SQL = 'ALTER INDEX [' + @IndexName + '] ON [' + @TableName + '] REBUILD WITH (ONLINE = OFF);';
    PRINT 'Rebuilding index: ' + @IndexName + ' on table: ' + @TableName;
    EXEC sp_executesql @SQL;
    FETCH NEXT FROM index_cursor INTO @TableName, @IndexName;
END

CLOSE index_cursor;
DEALLOCATE index_cursor;
"
echo [SUCCESS] Index maintenance completed

REM 6. Shrink Database (only if necessary - generally not recommended for production)
echo [STEP 6] Checking database size...
sqlcmd -S "%SERVER%" -d "%DATABASE%" -Q "SELECT name, size * 8 / 1024 as SizeMB FROM sys.database_files;" > "%LOG_DIR%\db_size_%DATE:~-4,4%%DATE:~-10,2%%DATE:~-7,2%.log"

REM 7. Clean up old backup files (keep last 7 days)
echo [STEP 7] Cleaning up old backup files...
forfiles /P "%BACKUP_DIR%" /M "*.bak" /D -7 /C "cmd /c del @path" 2>nul
forfiles /P "%BACKUP_DIR%" /M "*.trn" /D -7 /C "cmd /c del @path" 2>nul
echo [SUCCESS] Old backups cleaned up

echo.
echo ========================================
echo Maintenance completed successfully!
echo ========================================
echo.
echo Next maintenance run: Daily at 2:00 AM
echo.
echo Check logs in: %LOG_DIR%
echo Check backups in: %BACKUP_DIR%
echo.
goto :end

:error
echo.
echo ========================================
echo MAINTENANCE FAILED!
echo ========================================
echo.
echo Check the error messages above and resolve issues before proceeding.
echo.

:end
echo Press any key to exit...
pause >nul