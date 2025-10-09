[CmdletBinding(SupportsShouldProcess = $false)]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$FilePath,

    [Parameter()]
    [string[]]$ArgumentList = @(),

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$WorkingDirectory = (Get-Location).Path,

    [Parameter()]
    [string]$StdOutLog,

    [Parameter()]
    [string]$StdErrLog,

    [Parameter()]
    [int]$TimeoutSeconds = 120,

    [Parameter()]
    [switch]$NoNewWindow,

    [Parameter()]
    [switch]$PassThru,

    [Parameter()]
    [hashtable]$Environment = @{}
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-LogPath {
    param(
        [string]$Path
    )
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    $resolved = if (Test-Path -LiteralPath $Path) {
        (Resolve-Path -LiteralPath $Path).Path
    }
    else {
        [System.IO.Path]::GetFullPath((Join-Path -Path (Get-Location).Path -ChildPath $Path))
    }

    $directory = [System.IO.Path]::GetDirectoryName($resolved)
    if (-not [string]::IsNullOrEmpty($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }
    if (-not (Test-Path -LiteralPath $resolved)) {
        New-Item -ItemType File -Path $resolved -Force | Out-Null
    }
    return $resolved
}

if (-not (Test-Path -LiteralPath $WorkingDirectory)) {
    throw "Working directory '$WorkingDirectory' does not exist."
}

$stdOutPath = Resolve-LogPath -Path $StdOutLog
$stdErrPath = Resolve-LogPath -Path $StdErrLog

if ($stdOutPath -and $stdErrPath -and $stdOutPath -eq $stdErrPath) {
    throw "StdOutLog and StdErrLog must point to different files."
}

$startInfo = @{
    FilePath         = $FilePath
    ArgumentList     = $ArgumentList
    WorkingDirectory = $WorkingDirectory
    PassThru         = $true
}

if ($NoNewWindow.IsPresent) {
    $startInfo["NoNewWindow"] = $true
}

if ($stdOutPath) {
    $startInfo["RedirectStandardOutput"] = $stdOutPath
}

if ($stdErrPath) {
    $startInfo["RedirectStandardError"] = $stdErrPath
}

if ($Environment.Count -gt 0) {
    $startInfo["Environment"] = $Environment
}

Write-Verbose "Starting process: $FilePath $([string]::Join(' ', $ArgumentList))"
$process = Start-Process @startInfo

if (-not $process) {
    throw "Failed to start process '$FilePath'."
}

$waitMilliseconds = if ($TimeoutSeconds -gt 0) { $TimeoutSeconds * 1000 } else { -1 }
$exited = $process.WaitForExit($waitMilliseconds)

if (-not $exited) {
    try {
        Write-Warning "Process exceeded timeout (${TimeoutSeconds}s); terminating."
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    finally {
        throw "Process '$FilePath' did not exit within ${TimeoutSeconds}s."
    }
}

$process.Refresh()
$result = [PSCustomObject]@{
    FilePath   = $FilePath
    Arguments  = $ArgumentList
    ExitCode   = $process.ExitCode
    StdOutPath = $stdOutPath
    StdErrPath = $stdErrPath
    DurationMs = $process.ExitTime.Subtract($process.StartTime).TotalMilliseconds
}

if ($PassThru.IsPresent) {
    return $result
}

Write-Output $result
