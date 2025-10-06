#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run dotnet test with process cleanup to prevent MSBUILD errors
.DESCRIPTION
    Kills any existing dotnet/testhost processes before running tests
    to prevent "Child node exited prematurely" errors
.PARAMETER Project
    Test project path (default: WileyWidget.UiTests/WileyWidget.UiTests.csproj)
.PARAMETER Filter
    Test filter expression
.PARAMETER Verbosity
    Test output verbosity (default: normal)
#>

param(
    [string]$Project = "WileyWidget.UiTests/WileyWidget.UiTests.csproj",
    [string]$Filter,
    [string]$Verbosity = "normal"
)

$ErrorActionPreference = "Stop"

Write-Information "🔧 Killing existing dotnet/testhost processes..." -InformationAction Continue

# Kill dotnet and testhost processes
try {
    $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    $testhostProcesses = Get-Process -Name "testhost*" -ErrorAction SilentlyContinue

    $killedCount = 0

    foreach ($proc in $dotnetProcesses) {
        try {
            $proc.Kill()
            $proc.WaitForExit(2000)  # Wait up to 2 seconds
            $killedCount++
        }
        catch {
            Write-Warning "Could not kill dotnet process $($proc.Id): $($_.Exception.Message)"
        }
    }

    foreach ($proc in $testhostProcesses) {
        try {
            $proc.Kill()
            $proc.WaitForExit(2000)
            $killedCount++
        }
        catch {
            Write-Warning "Could not kill testhost process $($proc.Id): $($_.Exception.Message)"
        }
    }

    if ($killedCount -gt 0) {
        Write-Information "✅ Killed $killedCount process(es)" -InformationAction Continue
        Start-Sleep -Seconds 2  # Give system time to clean up
    }
    else {
        Write-Information "✅ No existing processes to kill" -InformationAction Continue
    }
}
catch {
    Write-Warning "Error during process cleanup: $($_.Exception.Message)"
}

Write-Information "🧪 Running dotnet test..." -InformationAction Continue

# Build test arguments
$testArgs = @(
    "test",
    $Project,
    "--verbosity", $Verbosity,
    "--logger", "console"
)

if ($Filter) {
    $testArgs += @("--filter", $Filter)
}

# Run the test
& dotnet @testArgs

$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Information "✅ Tests completed successfully" -InformationAction Continue
}
else {
    Write-Information "❌ Tests failed with exit code $exitCode" -InformationAction Continue
}

exit $exitCode
