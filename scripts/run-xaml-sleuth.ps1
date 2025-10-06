<#
.SYNOPSIS
Runs the xaml_sleuth static analyzer against every XAML view in the repository.

.DESCRIPTION
Discovers XAML files beneath the provided view root (defaults to src/Views),
invokes tools/python/xaml_sleuth.py for each file, and writes a sibling
<filename>.sleuth.txt report.

.PARAMETER ViewRoot
Root directory that contains XAML view files.

.PARAMETER PythonExe
Python interpreter path to use when launching the analyzer.

.PARAMETER MockData
Optional JSON file passed to xaml_sleuth's --mock-data argument.

.PARAMETER VerboseOutput
Switch to enable the analyzer's --verbose output.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$ViewRoot = "src/Views",

    [Parameter()]
    [string]$PythonExe = "C:/Users/biges/AppData/Local/Microsoft/WindowsApps/python3.11.exe",

    [Parameter()]
    [string]$MockData,

    [Parameter()]
    [switch]$VerboseOutput
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -Path $ViewRoot -PathType Container)) {
    throw "View root '$ViewRoot' was not found."
}

if (-not (Test-Path -Path $PythonExe -PathType Leaf)) {
    throw "Python executable '$PythonExe' was not found."
}

$analyzerPath = Join-Path -Path $PSScriptRoot -ChildPath "../tools/python/xaml_sleuth.py"
$analyzerPath = [System.IO.Path]::GetFullPath($analyzerPath)

if (-not (Test-Path -Path $analyzerPath -PathType Leaf)) {
    throw "Analyzer script '$analyzerPath' was not found."
}

if (-not $MockData) {
    $defaultMockDataPath = [System.IO.Path]::GetFullPath((Join-Path -Path $PSScriptRoot -ChildPath "../tools/python/mock-data/wiley-widget-default.json"))
    if (Test-Path -Path $defaultMockDataPath -PathType Leaf) {
        $MockData = $defaultMockDataPath
    }
}

$viewFiles = Get-ChildItem -Path $ViewRoot -Filter *.xaml -Recurse -File | Sort-Object -Property FullName

if (-not $viewFiles) {
    Write-Information "No XAML files discovered beneath '$ViewRoot'." -InformationAction Continue
    return
}

$results = @()

foreach ($view in $viewFiles) {
    $reportPath = [System.IO.Path]::ChangeExtension($view.FullName, ".sleuth.txt")
    $arguments = @($analyzerPath, $view.FullName, "--report", $reportPath)

    if ($MockData) {
        $arguments += @("--mock-data", (Resolve-Path -Path $MockData).Path)
    }

    if ($VerboseOutput.IsPresent) {
        $arguments += "--verbose"
    }

    Write-Information ("Analyzing {0}" -f $view.FullName) -InformationAction Continue

    $process = Start-Process -FilePath $PythonExe -ArgumentList $arguments -NoNewWindow -PassThru -Wait

    if ($process.ExitCode -ne 0) {
        throw "xaml_sleuth failed for '$($view.FullName)' with exit code $($process.ExitCode)."
    }

    $results += [PSCustomObject]@{
        View       = $view.FullName
        ReportPath = $reportPath
    }
}

$results | Write-Output
