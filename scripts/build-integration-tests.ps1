using namespace System.Management.Automation
using namespace System.Collections.Generic

<#
.SYNOPSIS
    Build and test WileyWidget.IntegrationTests with comprehensive logging
.DESCRIPTION
    Uses Microsoft-recommended MSBuild logging practices:
    - Binary logger (-bl) for structured log viewer analysis
    - File logger (-flp:v=diag) for detailed diagnostic text output
    - Error extraction and formatting for easy troubleshooting

    PowerShell 7.5.3 compliant with PSScriptAnalyzer validation.
.PARAMETER Clean
    Clean before building
.PARAMETER Test
    Run tests after successful build
.PARAMETER SkipBuild
    Skip build and only run tests
.EXAMPLE
    .\build-integration-tests.ps1 -Clean -Test
.NOTES
    Version: 1.0.0
    Requires: PowerShell 7.5.3+, .NET SDK 8.0+, PSScriptAnalyzer 1.22.0+
#>

#Requires -Version 7.5
#Requires -Modules @{ ModuleName='PSScriptAnalyzer'; ModuleVersion='1.22.0' }

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(HelpMessage = 'Clean before building')]
    [switch]$Clean,

    [Parameter(HelpMessage = 'Run tests after successful build')]
    [switch]$Test,

    [Parameter(HelpMessage = 'Skip build and only run tests')]
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

# Import required modules
Import-Module PSScriptAnalyzer -MinimumVersion 1.22.0 -ErrorAction Stop

# Resolve paths using Join-Path for cross-platform compatibility
$script:ProjectPath = Join-Path $PSScriptRoot '..' 'WileyWidget.IntegrationTests' 'WileyWidget.IntegrationTests.csproj' -Resolve -ErrorAction Stop
$script:LogDir = Join-Path $PSScriptRoot '..' 'WileyWidget.IntegrationTests' 'logs'
$script:BinLogPath = Join-Path $LogDir 'integration-tests.binlog'
$script:TextLogPath = Join-Path $LogDir 'integration-tests.log'
$script:ErrorLogPath = Join-Path $LogDir 'integration-tests-errors.log'

# Ensure log directory exists
if (-not (Test-Path -Path $LogDir -PathType Container)) {
    $null = New-Item -ItemType Directory -Path $LogDir -Force
}

Write-Information '🚀 WileyWidget Integration Tests Build Script'
Write-Information '============================================='
Write-Information "Project: $ProjectPath"
Write-Information "Logs: $LogDir"
Write-Information ''

# Clean if requested
if ($Clean) {
    Write-Information '🧹 Cleaning...'
    try {
        $cleanResult = & dotnet clean $ProjectPath --verbosity minimal 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Clean failed with exit code $LASTEXITCODE. Output: $cleanResult"
        }
        Write-Information '✅ Clean complete'
    }
    catch {
        Write-Error -Message "Clean failed: $($_.Exception.Message)" -ErrorAction Stop
    }
    Write-Information ''
}

# Build with comprehensive logging
if (-not $SkipBuild) {
    Write-Information '🔨 Building with comprehensive logging...'
    Write-Information "  - Binary log: $BinLogPath (use https://msbuildlog.com/ to view)"
    Write-Information "  - Text log: $TextLogPath (diagnostic verbosity)"
    Write-Information ''

    try {
        # Microsoft-recommended logging per docs:
        # -bl: Binary logger for structured analysis
        # -flp: File logger parameters with diagnostic verbosity
        # -v:minimal: Console verbosity (minimal to avoid clutter, all detail goes to logs)
        $buildArgs = [System.Collections.Generic.List[string]]::new()
        $buildArgs.Add('build')
        $buildArgs.Add($ProjectPath)
        $buildArgs.Add("-bl:$BinLogPath")
        $buildArgs.Add("-flp:LogFile=$TextLogPath;Verbosity=diagnostic;Encoding=UTF-8")
        $buildArgs.Add("-flp1:errorsonly;LogFile=$ErrorLogPath")
        $buildArgs.Add('-v:minimal')
        $buildArgs.Add('--no-restore')


        Write-Information "Executing: dotnet $($buildArgs -join ' ')"
        Write-Information ''

        $null = & dotnet @buildArgs 2>&1
        $buildExitCode = $LASTEXITCODE

        if ($buildExitCode -ne 0) {
            Write-Information ''
            Write-Information "📋 Error Summary from $ErrorLogPath"
            Write-Information '============================================='

            if (Test-Path -Path $ErrorLogPath) {
                $errors = Get-Content -Path $ErrorLogPath -Raw
                Write-Information $errors
            }

            Write-Information ''
            Write-Information '🔍 Troubleshooting:'
            Write-Information "  1. Review binary log: https://msbuildlog.com/ -> Open $BinLogPath"
            Write-Information "  2. Review text log: $TextLogPath"
            Write-Information "  3. Review error log: $ErrorLogPath"
            Write-Information '  4. Run error analyzer: .\scripts\analyze-integration-test-errors.ps1'

            throw "Build failed with exit code $buildExitCode"
        }

        Write-Information '✅ Build succeeded!'
        Write-Information ''

    }
    catch {
        Write-Error -Message "Build failed: $($_.Exception.Message)" -ErrorAction Stop
    }
}

# Run tests if requested
if ($Test) {
    Write-Information '🧪 Running tests with comprehensive logging...'
    Write-Information ''

    try {
        $testLogPath = Join-Path $LogDir 'integration-tests-run.log'
        $testResultsPath = Join-Path $LogDir 'integration-tests-results.trx'

        $testArgs = [System.Collections.Generic.List[string]]::new()
        $testArgs.Add('test')
        $testArgs.Add($ProjectPath)
        $testArgs.Add('--no-build')
        $testArgs.Add('--no-restore')
        $testArgs.Add('-v:normal')
        $testArgs.Add('--logger:console;verbosity=detailed')
        $testArgs.Add("--logger:trx;LogFileName=$testResultsPath")

        Write-Information "Executing: dotnet $($testArgs -join ' ')"
        Write-Information ''

        $null = & dotnet @testArgs 2>&1 | Tee-Object -FilePath $testLogPath
        $testExitCode = $LASTEXITCODE

        Write-Information ''
        if ($testExitCode -eq 0) {
            Write-Information '✅ All tests passed!'
        }
        else {
            Write-Warning "⚠️ Some tests failed (exit code: $testExitCode)"
            Write-Information "Test results: $testResultsPath"
            Write-Information "Test log: $testLogPath"
        }

    }
    catch {
        Write-Error -Message "Test run failed: $($_.Exception.Message)" -ErrorAction Stop
    }
}

Write-Information ''
Write-Information "📁 Logs available at: $LogDir"
Write-Information "  - Binary log (structured): $BinLogPath"
Write-Information "  - Text log (diagnostic): $TextLogPath"
Write-Information "  - Errors only: $ErrorLogPath"
Write-Information ''
Write-Information '✨ Done!'
