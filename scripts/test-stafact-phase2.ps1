# Phase 2 Testing Infrastructure - StaFact Tests Only
# Runs only WPF STA-threaded tests for rapid feedback during development

param(
    [switch]$Quick,
    [switch]$Verbose,
    [switch]$Coverage,
    [int]$TimeoutMinutes = 2
)

Write-Output "🚀 Phase 2 Testing: StaFact Tests Only"
Write-Output "Target: WPF STA-threaded tests for rapid development feedback"
Write-Output ""

# Ensure we're in the right directory
Push-Location $PSScriptRoot

try {
    # Clean up any lingering test processes before building
    Write-Output "🧹 Cleaning up lingering test processes..."
    foreach ($name in 'WileyWidget', 'testhost', 'vstest.console', 'dotnet') {
        try {
            Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
                Write-Output "Stopping $($_.ProcessName) (pid=$($_.Id))"
                $_ | Stop-Process -Force -ErrorAction SilentlyContinue
            }
        }
        catch { }
    }

    # Build first (incremental if possible)
    Write-Output "📦 Building project..."
    $buildArgs = @(
        "build",
        "$PSScriptRoot\..\WileyWidget.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
    )

    if ($Quick) {
        $buildArgs += "/t:Build"
    }

    & dotnet $buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit $LASTEXITCODE
    }

    # Run only StaFact tests from UiTests project
    Write-Output "🧪 Running StaFact tests only..."
    $testArgs = @(
        "test",
        "$PSScriptRoot\..\WileyWidget.UiTests\WileyWidget.UiTests.csproj",
        "--filter",
        "Category=StaFact",
        "--logger",
        "console;verbosity=normal"
    )

    if ($Verbose) {
        $testArgs += "--verbosity", "detailed"
    }

    if ($Coverage) {
        $testArgs += "--collect:XPlat Code Coverage"
        $testArgs += "--results-directory:$PSScriptRoot\..\TestResults\Coverage"
    }

    # Add timeout (convert minutes to milliseconds for blame-hang-timeout)
    $timeoutMs = $TimeoutMinutes * 60 * 1000
    $testArgs += "--blame-hang-timeout", "$($TimeoutMinutes)m"

    $startTime = Get-Date
    & dotnet $testArgs
    $exitCode = $LASTEXITCODE
    $endTime = Get-Date
    $duration = $endTime - $startTime

    Write-Output ""
    Write-Output "⏱️  Phase 2 Testing completed in $($duration.TotalSeconds.ToString("F1")) seconds"

    if ($exitCode -eq 0) {
        Write-Output "✅ All StaFact tests passed!"
    }
    else {
        Write-Output "❌ Some StaFact tests failed (Exit code: $exitCode)"
    }

    exit $exitCode

}
finally {
    Pop-Location
}
