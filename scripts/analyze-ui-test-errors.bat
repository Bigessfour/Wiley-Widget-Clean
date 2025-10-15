@echo off
REM UI Test Error Log Analyzer
REM This script helps analyze UI test error logs for debugging

echo ========================================
echo UI Test Error Log Analyzer
echo ========================================

if not exist "test-logs" (
    echo No test-logs directory found. Run UI tests first.
    pause
    exit /b 1
)

echo.
echo === Test Output Log ===
if exist "test-logs\test-output.log" (
    type "test-logs\test-output.log" | findstr /C:"error(s)" /C:"warning(s)" /C:"Failed" /C:"Error"
) else (
    echo test-output.log not found
)

echo.
echo === UI Test Error Log ===
if exist "test-logs\ui-test-errors.log" (
    echo Recent errors:
    powershell "Get-Content 'test-logs\ui-test-errors.log' -Tail 20"
) else (
    echo ui-test-errors.log not found
)

echo.
echo === Test Results Summary ===
if exist "TestResults" (
    dir /b TestResults\*.trx 2>nul
    echo.
    echo To view detailed results, open the .trx files in Visual Studio
) else (
    echo No TestResults directory found
)

echo.
echo === Recommendations ===
echo 1. Check ui-test-errors.log for detailed error information
echo 2. Look for "No overload for method 'Show'" errors - views need Window wrapper
echo 3. Check for missing MainWindow type references
echo 4. Look for Bitmap.ToStream extension method issues
echo 5. Review Assert.Skip availability in test framework

pause