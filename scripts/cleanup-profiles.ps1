# PowerShell Profile Cleanup Script
# Safely analyze and clean up unused profiles

param(
    [switch]$Analyze,
    [switch]$Backup,
    [switch]$Cleanup,
    [switch]$WhatIf
)

Write-Host "🧹 PowerShell Profile Cleanup Tool" -ForegroundColor Cyan
Write-Host "=" * 40 -ForegroundColor Cyan

$profiles = @(
    @{
        Path = $PROFILE.AllUsersAllHosts
        Name = "AllUsersAllHosts"
        Description = "Global profile (affects all users)"
        SafeToDelete = $false
    },
    @{
        Path = $PROFILE.AllUsersCurrentHost
        Name = "AllUsersCurrentHost"
        Description = "Global current host profile"
        SafeToDelete = $true
    },
    @{
        Path = $PROFILE.CurrentUserAllHosts
        Name = "CurrentUserAllHosts"
        Description = "User-wide profile (all hosts)"
        SafeToDelete = $true
    },
    @{
        Path = $PROFILE.CurrentUserCurrentHost
        Name = "CurrentUserCurrentHost"
        Description = "Current user, current host (most specific)"
        SafeToDelete = $false
    }
)

if ($Analyze) {
    Write-Host "`n📊 Profile Analysis:" -ForegroundColor Yellow

    foreach ($profile in $profiles) {
        $exists = Test-Path $profile.Path

        if ($exists) {
            $file = Get-Item $profile.Path
            $size = [math]::Round($file.Length / 1KB, 2)
            $age = (Get-Date) - $file.LastWriteTime

            Write-Host "`n📄 $($profile.Name):" -ForegroundColor Green
            Write-Host "   Path: $($profile.Path)" -ForegroundColor Gray
            Write-Host "   Size: ${size}KB" -ForegroundColor White
            Write-Host "   Age: $([math]::Round($age.TotalDays, 1)) days old" -ForegroundColor White
            Write-Host "   Description: $($profile.Description)" -ForegroundColor White

            # Analyze content
            $content = Get-Content $profile.Path -Raw
            $lines = ($content -split "`n").Count
            Write-Host "   Lines: $lines" -ForegroundColor White

            # Check for important content
            $hasImportant = $false
            if ($content -match "MCP|mcp|Azure|Key.*Vault") {
                Write-Host "   ⚠️  Contains: Azure/MCP integration" -ForegroundColor Yellow
                $hasImportant = $true
            }
            if ($content -match "Import-Module.*Az|Azure") {
                Write-Host "   ⚠️  Contains: Azure module imports" -ForegroundColor Yellow
                $hasImportant = $true
            }

            if ($hasImportant) {
                $profile.SafeToDelete = $false
                Write-Host "   ❌ RECOMMENDATION: Keep (contains important content)" -ForegroundColor Red
            } elseif ($profile.SafeToDelete) {
                Write-Host "   ✅ RECOMMENDATION: Safe to delete" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️  RECOMMENDATION: Keep (system profile)" -ForegroundColor Yellow
            }

        } else {
            Write-Host "`n📄 $($profile.Name):" -ForegroundColor Red
            Write-Host "   Status: Not found (already clean)" -ForegroundColor Gray
        }
    }
}

if ($Backup) {
    Write-Host "`n💾 Creating Backups:" -ForegroundColor Yellow

    foreach ($profile in $profiles) {
        if (Test-Path $profile.Path) {
            $backupPath = "$($profile.Path).backup.$(Get-Date -Format 'yyyyMMdd-HHmmss')"
            if ($WhatIf) {
                Write-Host "   Would backup: $($profile.Path) → $backupPath" -ForegroundColor Gray
            } else {
                Copy-Item $profile.Path $backupPath
                Write-Host "   ✅ Backed up: $($profile.Name)" -ForegroundColor Green
            }
        }
    }
}

if ($Cleanup) {
    Write-Host "`n🗑️  Cleanup Recommendations:" -ForegroundColor Yellow

    $toDelete = $profiles | Where-Object { $_.SafeToDelete -and (Test-Path $_.Path) }

    if ($toDelete.Count -eq 0) {
        Write-Host "   ℹ️  No profiles safe to delete automatically" -ForegroundColor Blue
    } else {
        foreach ($profile in $toDelete) {
            if ($WhatIf) {
                Write-Host "   Would delete: $($profile.Name) - $($profile.Path)" -ForegroundColor Gray
            } else {
                Remove-Item $profile.Path -Force
                Write-Host "   ✅ Deleted: $($profile.Name)" -ForegroundColor Green
            }
        }
    }

    Write-Host "`n⚠️  Manual Cleanup Required:" -ForegroundColor Yellow
    Write-Host "   • Review AllUsersAllHosts profile manually" -ForegroundColor White
    Write-Host "   • Check for any custom functions you need" -ForegroundColor White
    Write-Host "   • Test PowerShell after cleanup" -ForegroundColor White
}

if (-not ($Analyze -or $Backup -or $Cleanup)) {
    Write-Host "`n📖 Usage Examples:" -ForegroundColor Cyan
    Write-Host "   .\cleanup-profiles.ps1 -Analyze           # Analyze all profiles" -ForegroundColor White
    Write-Host "   .\cleanup-profiles.ps1 -Backup            # Backup all profiles" -ForegroundColor White
    Write-Host "   .\cleanup-profiles.ps1 -Cleanup           # Remove safe-to-delete profiles" -ForegroundColor White
    Write-Host "   .\cleanup-profiles.ps1 -WhatIf -Cleanup   # Preview cleanup actions" -ForegroundColor White
    Write-Host "   .\cleanup-profiles.ps1 -Analyze -Backup   # Analyze and backup" -ForegroundColor White
}

Write-Host "`n✨ Profile cleanup complete!" -ForegroundColor Green