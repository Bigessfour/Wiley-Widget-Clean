# PowerShell Profile Performance Analyzer
# Run this to identify bottlenecks in your profile

param(
    [switch]$Detailed,
    [switch]$Export
)

Write-Host "🔍 PowerShell Profile Performance Analyzer" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Cyan

# Check profile files
$profilePaths = @(
    $PROFILE.AllUsersAllHosts,
    $PROFILE.AllUsersCurrentHost,
    $PROFILE.CurrentUserAllHosts,
    $PROFILE.CurrentUserCurrentHost
)

Write-Host "`n📁 Profile Files:" -ForegroundColor Yellow
foreach ($path in $profilePaths) {
    if (Test-Path $path) {
        $fileInfo = Get-Item $path
        $size = [math]::Round($fileInfo.Length / 1KB, 2)
        Write-Host "  ✅ $($fileInfo.Name) - ${size}KB" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ $(Split-Path $path -Leaf) - Not found" -ForegroundColor Red
    }
}

# Analyze profile content for common bottlenecks
function Analyze-ProfileContent {
    param([string]$ProfilePath)

    if (-not (Test-Path $ProfilePath)) { return }

    $content = Get-Content $ProfilePath -Raw
    $issues = @()

    # Check for synchronous module imports
    if ($content -match "Import-Module.*-Force") {
        $issues += "Synchronous module import with -Force flag"
    }

    # Check for network operations
    if ($content -match "Invoke-WebRequest|Invoke-RestMethod|curl|wget") {
        $issues += "Network operations in profile"
    }

    # Check for large loops or complex logic
    if ($content -match "for.*\{|foreach.*\{|while.*\{") {
        $issues += "Complex loops that might slow startup"
    }

    # Check for file system operations
    if ($content -match "Get-ChildItem.*-Recurse|dir.*-Recurse") {
        $issues += "Recursive file system operations"
    }

    return $issues
}

# Analyze each profile
Write-Host "`n🔧 Performance Issues Found:" -ForegroundColor Yellow
foreach ($path in $profilePaths) {
    if (Test-Path $path) {
        $issues = Analyze-ProfileContent -ProfilePath $path
        if ($issues.Count -gt 0) {
            Write-Host "  📄 $(Split-Path $path -Leaf):" -ForegroundColor Magenta
            foreach ($issue in $issues) {
                Write-Host "    ⚠️  $issue" -ForegroundColor Yellow
            }
        }
    }
}

# Measure current profile load time
Write-Host "`n⏱️  Profile Load Time Test:" -ForegroundColor Yellow

$startTime = Get-Date
# Simulate profile loading by dot-sourcing
foreach ($path in $profilePaths) {
    if (Test-Path $path) {
        try {
            . $path
        }
        catch {
            Write-Host "  ❌ Error loading $(Split-Path $path -Leaf): $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
$loadTime = (Get-Date) - $startTime

$loadTimeMs = [math]::Round($loadTime.TotalMilliseconds, 2)
if ($loadTimeMs -gt 3000) {
    $color = "Red"
    $recommendation = "🚨 CRITICAL: Profile is very slow (>3s)"
}
elseif ($loadTimeMs -gt 1000) {
    $color = "Yellow"
    $recommendation = "⚠️  SLOW: Consider optimization"
}
else {
    $color = "Green"
    $recommendation = "✅ FAST: Good performance"
}

Write-Host "  Load time: ${loadTimeMs}ms - $recommendation" -ForegroundColor $color

# Performance recommendations
Write-Host "`n💡 Optimization Recommendations:" -ForegroundColor Cyan
Write-Host "  1. Use lazy loading for heavy modules:" -ForegroundColor White
Write-Host "     - Replace 'Import-Module Az' with function that loads on first use" -ForegroundColor Gray
Write-Host "  2. Move non-essential operations to background:" -ForegroundColor White
Write-Host "     - Use Start-Job for telemetry, updates, etc." -ForegroundColor Gray
Write-Host "  3. Cache expensive operations:" -ForegroundColor White
Write-Host "     - Store results in global variables" -ForegroundColor Gray
Write-Host "  4. Use conditional loading:" -ForegroundColor White
Write-Host "     - Only load modules when actually needed" -ForegroundColor Gray
Write-Host "  5. Minimize synchronous operations:" -ForegroundColor White
Write-Host "     - Avoid network calls, recursive searches" -ForegroundColor Gray

if ($Detailed) {
    Write-Host "`n📊 Detailed Analysis:" -ForegroundColor Yellow

    # Check PowerShell version and modules
    Write-Host "  PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor White
    Write-Host "  Execution Policy: $(Get-ExecutionPolicy)" -ForegroundColor White

    # Check loaded modules
    $loadedModules = Get-Module | Measure-Object
    Write-Host "  Currently Loaded Modules: $($loadedModules.Count)" -ForegroundColor White

    # Check available modules that might be auto-loaded
    $availableModules = Get-Module -ListAvailable | Measure-Object
    Write-Host "  Available Modules: $($availableModules.Count)" -ForegroundColor White
}

if ($Export) {
    $exportPath = Join-Path $PSScriptRoot "profile-analysis-$(Get-Date -Format 'yyyyMMdd-HHmmss').txt"
    $analysis = @"
PowerShell Profile Performance Analysis
Generated: $(Get-Date)
Load Time: ${loadTimeMs}ms

Profile Files:
$(foreach ($path in $profilePaths) {
    if (Test-Path $path) {
        "  $(Split-Path $path -Leaf) - $((Get-Item $path).Length) bytes"
    }
})

Recommendations:
- Use lazy loading for heavy modules
- Move non-essential operations to background jobs
- Cache expensive operations
- Use conditional loading
- Minimize synchronous operations
"@

    $analysis | Out-File -FilePath $exportPath -Encoding UTF8
    Write-Host "`n📄 Analysis exported to: $exportPath" -ForegroundColor Green
}

Write-Host "`n✨ Analysis complete!" -ForegroundColor Green
