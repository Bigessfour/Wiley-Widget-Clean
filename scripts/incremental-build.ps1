# Incremental Build Script for Wiley Widget
# Optimizes build times by skipping package restore when packages haven't changed

param(
    [switch]$ForceRestore,
    [switch]$Clean
)

$projectFile = "WileyWidget.csproj"
$packagesLastModifiedFile = ".packages.lastmodified"
$projectRoot = Split-Path -Parent $PSScriptRoot

# Change to project root
Push-Location $projectRoot

try {
    if ($Clean) {
        Write-Information "Cleaning build artifacts..." -InformationAction Continue
        dotnet clean $projectFile
        Remove-Item $packagesLastModifiedFile -ErrorAction SilentlyContinue
        exit 0
    }

    # Check if packages have changed
    $packagesChanged = $true
    if (Test-Path $packagesLastModifiedFile) {
        $lastCheck = Get-Content $packagesLastModifiedFile | Out-String
        $currentHash = Get-FileHash $projectFile -Algorithm SHA256 | Select-Object -ExpandProperty Hash

        if ($lastCheck.Trim() -eq $currentHash.Trim()) {
            $packagesChanged = $false
        }
    }

    # Build arguments
    $buildArgs = @(
        "build",
        $projectFile,
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
    )

    if (-not $ForceRestore -and -not $packagesChanged) {
        Write-Information "Packages unchanged, using incremental build (--no-restore)..." -InformationAction Continue
        $buildArgs += "--no-restore"
    }
    else {
        Write-Information "Restoring packages and building..." -InformationAction Continue
        # Update the hash file after successful restore
        $currentHash = Get-FileHash $projectFile -Algorithm SHA256 | Select-Object -ExpandProperty Hash
        $currentHash | Out-File $packagesLastModifiedFile -Encoding UTF8
    }

    # Execute build
    & dotnet $buildArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Information "Build completed successfully" -InformationAction Continue
    }
    else {
        Write-Error "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

}
finally {
    Pop-Location
}
