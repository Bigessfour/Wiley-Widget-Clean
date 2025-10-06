#!/usr/bin/env pwsh

# Simple script to archive obsolete scripts
$obsolete = @(
    'check_syncfusion_nuget.py',
    'ci-generate-manifest.ps1',
    'cleanup-profiles.ps1',
    'dev-start-debug.py',
    'extract_chapter.py',
    'Generate-FetchabilityManifest.ps1',
    'Index-RemoteRepository-Enhanced.ps1',
    'quick_ui_inspect.py',
    'setup-liberal-firewall.ps1',
    'temp_extract.py',
    'test_configuration.py',
    'ui_debug.py',
    'ui_test_runner.py',
    'verify_ui_setup.py'
)

$archiveDir = "scripts\archive"
if (-not (Test-Path $archiveDir)) {
    New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null
}

Write-Output "Archiving obsolete scripts...`n"
$moved = 0
foreach ($script in $obsolete) {
    $path = "scripts\$script"
    if (Test-Path $path) {
        Move-Item $path -Destination $archiveDir -Force
        Write-Output "  ✅ Archived: $script"
        $moved++
    }
}

Write-Output "`n✅ Archived $moved scripts to scripts/archive/"
