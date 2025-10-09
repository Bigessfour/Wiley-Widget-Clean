#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Optimized Trunk formatting and checking for Wiley Widget development

.DESCRIPTION
    This script provides fast, targeted Trunk operations instead of running --all
    which can be slow on large repositories.

.PARAMETER Target
    What to format/check: 'changed', 'staged', 'recent', 'python', 'powershell', 'csharp', or 'all'

.PARAMETER Action
    Action to perform: 'fmt', 'check', or 'both' (default)

.EXAMPLE
    .\trunk-optimized.ps1 -Target changed
    # Formats only files with unstaged changes

.EXAMPLE
    .\trunk-optimized.ps1 -Target python -Action fmt
    # Formats only Python files that have been modified

.EXAMPLE
    .\trunk-optimized.ps1 -Target recent
    # Formats files changed in the last commit
#>

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('changed', 'staged', 'recent', 'python', 'powershell', 'csharp', 'all')]
    [string]$Target = 'changed',

    [Parameter(Mandatory = $false)]
    [ValidateSet('fmt', 'check', 'both')]
    [string]$Action = 'both'
)

function Write-OptimizedOutput {
    param([string]$Message, [string]$Color = "Green")
    Write-Information "🚀 $Message" -InformationAction Continue
}

function Get-TrunkCommand {
    param([string]$Target, [string]$Action)

    $baseArgs = @()

    switch ($Target) {
        'changed' {
            # Files with unstaged changes
            $baseArgs += '--filter=diff'
            Write-OptimizedOutput "Target: Files with unstaged changes"
        }
        'staged' {
            # Files in staging area
            $baseArgs += '--staged'
            Write-OptimizedOutput "Target: Staged files only"
        }
        'recent' {
            # Files changed in last commit
            $baseArgs += '--since=HEAD~1'
            Write-OptimizedOutput "Target: Files changed since last commit"
        }
        'python' {
            # Only Python files that have been modified
            $baseArgs += '--filter=**/*.py'
            $baseArgs += '--since=HEAD~5'  # Last 5 commits
            Write-OptimizedOutput "Target: Python files (last 5 commits)"
        }
        'powershell' {
            # Only PowerShell files that have been modified
            $baseArgs += '--filter=**/*.ps1'
            $baseArgs += '--since=HEAD~5'  # Last 5 commits
            Write-OptimizedOutput "Target: PowerShell files (last 5 commits)"
        }
        'csharp' {
            # Only C# files that have been modified
            $baseArgs += '--filter=**/*.cs'
            $baseArgs += '--since=HEAD~3'  # Last 3 commits
            Write-OptimizedOutput "Target: C# files (last 3 commits)"
        }
        'all' {
            $baseArgs += '--all'
            Write-OptimizedOutput "Target: ALL files (WARNING: This will be slow!)" "Yellow"
        }
    }

    return $baseArgs
}

function Invoke-TrunkAction {
    param([string]$Command, [array]$Args)

    $fullCommand = "trunk $Command " + ($Args -join ' ')
    Write-OptimizedOutput "Running: $fullCommand"

    $startTime = Get-Date
    try {
        & trunk $Command @Args
        $endTime = Get-Date
        $duration = $endTime - $startTime
        Write-OptimizedOutput "✅ Completed in $($duration.TotalSeconds.ToString('F2')) seconds" "Green"
        return $true
    }
    catch {
        Write-OptimizedOutput "❌ Error: $_" "Red"
        return $false
    }
}

# Main execution
Write-OptimizedOutput "Starting optimized Trunk operations..."

$baseArgs = Get-TrunkCommand -Target $Target -Action $Action

$success = $true

if ($Action -eq 'fmt' -or $Action -eq 'both') {
    Write-OptimizedOutput "🎨 Running formatter..."
    $fmtSuccess = Invoke-TrunkAction -Command 'fmt' -Args $baseArgs
    $success = $success -and $fmtSuccess
}

if ($Action -eq 'check' -or $Action -eq 'both') {
    Write-OptimizedOutput "🔍 Running checks..."
    $checkArgs = $baseArgs + @('--fix')  # Auto-fix when possible
    $checkSuccess = Invoke-TrunkAction -Command 'check' -Args $checkArgs
    $success = $success -and $checkSuccess
}

if ($success) {
    Write-OptimizedOutput "🎉 All operations completed successfully!" "Green"
    exit 0
}
else {
    Write-OptimizedOutput "⚠️ Some operations had issues. Check output above." "Red"
    exit 1
}
