#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run StaFact tests with automatic cleanup of orphaned .NET processes
.DESCRIPTION
    Executes dotnet test with StaFact filter and cleans up any orphaned
    .NET processes that may remain after testing
.PARAMETER Verbosity
    Test output verbosity level (default: minimal)
#>

param(
    [string]$Verbosity = "minimal"
)

$ErrorActionPreference = "Stop"

Write-Information "=== Running StaFact Tests ===" -InformationAction Continue

# Run the tests
$testArgs = @(
    "test",
    "$PSScriptRoot/../WileyWidget.UiTests/WileyWidget.UiTests.csproj",
    "--filter", "Category=StaFact",
    "--verbosity", $Verbosity,
    "--logger", "console"
)

& dotnet @testArgs
$testExitCode = $LASTEXITCODE

Write-Information "`n=== Test Execution Complete ===" -InformationAction Continue

if ($testExitCode -eq 0) {
    Write-Information "✅ Tests passed" -InformationAction Continue
}
else {
    Write-Information "❌ Tests failed with exit code $testExitCode" -InformationAction Continue
}

# Always run cleanup to check for orphaned processes
Write-Information "`n=== Checking for Orphaned .NET Processes ===" -InformationAction Continue

try {
    # Run the cleanup script in dry-run mode first to show what would be cleaned
    & python "$PSScriptRoot/cleanup-dotnet.py" --dry-run

    # Then run actual cleanup
    Write-Information "`n=== Cleaning Up Orphaned Processes ===" -InformationAction Continue
    & python "$PSScriptRoot/cleanup-dotnet.py" --force

    Write-Information "✅ Cleanup complete" -InformationAction Continue
}
catch {
    Write-Information "⚠️  Cleanup failed: $($_.Exception.Message)" -InformationAction Continue
}

# Return the original test exit code
exit $testExitCode
