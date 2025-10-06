# Build Cache Management Script
# Manages .NET build caching for improved development workflow performance

param(
    [switch]$Clean,
    [switch]$Stats,
    [switch]$Clear
)

$buildCacheDir = ".buildcache"
$projectRoot = Split-Path -Parent $PSScriptRoot

Push-Location $projectRoot

try {
    if ($Clean) {
        Write-Information "Cleaning build cache..." -InformationAction Continue
        if (Test-Path $buildCacheDir) {
            Remove-Item $buildCacheDir -Recurse -Force
        }
        Write-Information "Build cache cleaned" -InformationAction Continue
        exit 0
    }

    if ($Clear) {
        Write-Information "Clearing build cache..." -InformationAction Continue
        if (Test-Path $buildCacheDir) {
            Get-ChildItem $buildCacheDir | Remove-Item -Recurse -Force
        }
        Write-Information "Build cache cleared" -InformationAction Continue
        exit 0
    }

    if ($Stats) {
        Write-Information "Build cache statistics:" -InformationAction Continue

        if (Test-Path $buildCacheDir) {
            $cacheItems = Get-ChildItem $buildCacheDir -Recurse -File -ErrorAction SilentlyContinue
            $totalSize = 0
            $itemCount = 0
            if ($cacheItems) {
                $totalSize = ($cacheItems | Measure-Object -Property Length -Sum).Sum
                $itemCount = $cacheItems.Count
            }

            Write-Information "Cache directory: $buildCacheDir" -InformationAction Continue
            Write-Information "Total items: $itemCount" -InformationAction Continue
            Write-Information "Total size: $([math]::Round($totalSize / 1MB, 2)) MB" -InformationAction Continue

            # Show cache files by type
            $cacheItems | Group-Object Extension | ForEach-Object {
                $size = ($_.Group | Measure-Object Length -Sum).Sum
                Write-Information "  $($_.Name): $($_.Count) files, $([math]::Round($size / 1MB, 2)) MB" -InformationAction Continue
            }
        }
        else {
            Write-Information "No build cache found" -InformationAction Continue
        }

        exit 0
    }

    # Default: Show usage
    Write-Information "Build Cache Management Script" -InformationAction Continue
    Write-Information "Usage:" -InformationAction Continue
    Write-Information "  .\manage-build-cache.ps1 -Clean    # Clean build cache on next build" -InformationAction Continue
    Write-Information "  .\manage-build-cache.ps1 -Clear    # Clear existing build cache" -InformationAction Continue
    Write-Information "  .\manage-build-cache.ps1 -Stats    # Show build cache statistics" -InformationAction Continue
    Write-Information "" -InformationAction Continue
    Write-Information "The build cache improves incremental build performance by caching:" -InformationAction Continue
    Write-Information "  - Intermediate compilation outputs" -InformationAction Continue
    Write-Information "  - Reference assemblies" -InformationAction Continue
    Write-Information "  - Shared compilation artifacts" -InformationAction Continue

}
finally {
    Pop-Location
}
