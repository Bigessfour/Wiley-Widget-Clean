# UI Test Runner with Process Cleanup
# Runs UI tests with automatic cleanup of lingering processes

param(
    [Parameter(Mandatory=$true)]
    [string]$Project,
    [string]$Verbosity = "normal",
    [int]$TimeoutMinutes = 5
)

Write-Output "🚀 UI Test Runner with Process Cleanup"
Write-Output "Project: $Project"
Write-Output "Verbosity: $Verbosity"
Write-Output "Timeout: $TimeoutMinutes minutes"
Write-Output ""

# Ensure we're in the right directory
Push-Location $PSScriptRoot

try {
    # Clean up any lingering test processes before building
    Write-Output "🧹 Cleaning up lingering test processes..."
    $processNames = @('WileyWidget', 'testhost', 'vstest.console', 'dotnet', 'FlaUI', 'UIAutomation')

    foreach ($name in $processNames) {
        try {
            $processes = Get-Process -Name $name -ErrorAction SilentlyContinue
            if ($processes) {
                Write-Output "Stopping $name processes..."
                $processes | ForEach-Object {
                    Write-Output "  Stopping $($_.ProcessName) (pid=$($_.Id))"
                    $_ | Stop-Process -Force -ErrorAction SilentlyContinue
                }
            }
        }
        catch {
            Write-Output "  Warning: Could not stop $name processes: $($_.Exception.Message)"
        }
    }

    # Wait a moment for processes to fully terminate
    Start-Sleep -Seconds 2

    # Build the project first
    Write-Output "📦 Building project..."
    $buildArgs = @(
        "build",
        "$PSScriptRoot\..\WileyWidget.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
    )

    & dotnet $buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    # Run UI tests
    Write-Output "🧪 Running UI tests..."
    $testArgs = @(
        "test",
        $Project,
        "--logger",
        "console;verbosity=$Verbosity"
    )

    # Add timeout (convert minutes to milliseconds for blame-hang-timeout)
    $timeoutMs = $TimeoutMinutes * 60 * 1000
    $testArgs += "--blame-hang-timeout", "$timeoutMs"

    Write-Output "Command: dotnet $($testArgs -join ' ')"
    Write-Output ""

    $startTime = Get-Date
    & dotnet $testArgs
    $exitCode = $LASTEXITCODE
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Output ""
    Write-Output "⏱️  UI Testing completed in $($duration.TotalSeconds.ToString("F1")) seconds"

    if ($exitCode -eq 0) {
        Write-Output "✅ All UI tests passed!"
    }
    else {
        Write-Output "❌ Some UI tests failed (Exit code: $exitCode)"
    }

    exit $exitCode

}
catch {
    Write-Error "UI test execution failed: $($_.Exception.Message)"
    exit 1
}
finally {
    # Final cleanup
    Write-Output "🧹 Final cleanup..."
    try {
        foreach ($name in $processNames) {
            Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Output "  Force stopping $($_.ProcessName) (pid=$($_.Id))"
                $_ | Stop-Process -Force -ErrorAction SilentlyContinue
            }
        }
    }
    catch { }

    Pop-Location
}