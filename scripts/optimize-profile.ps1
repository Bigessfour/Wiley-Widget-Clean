# PowerShell Profile Quick Optimizer
# Applies common performance optimizations

param(
    [string]$ProfilePath = $PROFILE,
    [switch]$Backup,
    [switch]$WhatIf
)

Write-Host "🚀 PowerShell Profile Quick Optimizer" -ForegroundColor Cyan
Write-Host "=" * 40 -ForegroundColor Cyan

if (-not (Test-Path $ProfilePath)) {
    Write-Host "❌ Profile not found: $ProfilePath" -ForegroundColor Red
    exit 1
}

# Create backup if requested
if ($Backup) {
    $backupPath = "$ProfilePath.backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item $ProfilePath $backupPath
    Write-Host "📦 Backup created: $backupPath" -ForegroundColor Green
}

$content = Get-Content $ProfilePath -Raw

# Optimizations to apply
$optimizations = @(
    @{
        Name = "Remove synchronous Import-Module calls"
        Pattern = 'Import-Module\s+(?!-ListAvailable)(?!#).*'
        Replacement = '# $0  # Moved to lazy loading'
        Applied = $false
    },
    @{
        Name = "Add lazy loading for Azure modules"
        Pattern = '(?s)^.*$'
        Replacement = @'
$0

# Lazy loading for Azure modules
function Import-AzureModules {
    if (-not (Get-Module -Name Az -ListAvailable)) {
        Write-Host "Loading Azure modules..." -ForegroundColor Yellow
        Import-Module Az -ErrorAction SilentlyContinue
    }
}

# Override az command to trigger lazy loading
if (Get-Command az -ErrorAction SilentlyContinue) {
    function global:az { Import-AzureModules; & (Get-Command az) @args }
}
'@
        Applied = $false
    },
    @{
        Name = "Add background job for telemetry"
        Pattern = '(\$env:.*TELEMETRY.*=.*1)'
        Replacement = @'
$1

# Move telemetry to background
Start-Job -ScriptBlock { $env:POWERSHELL_TELEMETRY_OPTOUT = "1" } | Out-Null
'@
        Applied = $false
    }
)

$changes = 0

foreach ($opt in $optimizations) {
    if ($content -match $opt.Pattern -and -not $opt.Applied) {
        if ($WhatIf) {
            Write-Host "🔍 Would apply: $($opt.Name)" -ForegroundColor Yellow
        } else {
            $content = $content -replace $opt.Pattern, $opt.Replacement
            Write-Host "✅ Applied: $($opt.Name)" -ForegroundColor Green
            $changes++
        }
        $opt.Applied = $true
    }
}

if (-not $WhatIf -and $changes -gt 0) {
    $content | Set-Content $ProfilePath -Encoding UTF8
    Write-Host "`n✨ Applied $changes optimizations!" -ForegroundColor Green
} elseif ($WhatIf) {
    Write-Host "`n🔍 WhatIf mode - no changes made" -ForegroundColor Yellow
} else {
    Write-Host "`nℹ️  No optimizations needed or already applied" -ForegroundColor Blue
}

Write-Host "`n💡 Manual optimizations you can consider:" -ForegroundColor Cyan
Write-Host "  • Replace heavy module imports with lazy loading functions" -ForegroundColor White
Write-Host "  • Move network operations to background jobs" -ForegroundColor White
Write-Host "  • Use Start-Job for non-critical initialization" -ForegroundColor White
Write-Host "  • Cache expensive operations in global variables" -ForegroundColor White
Write-Host "  • Use conditional loading based on context" -ForegroundColor White