#region FAST POWERSHELL PROFILE - OPTIMIZED FOR SPEED
# ========================================================
# Performance optimizations applied:
# - Lazy loading of heavy modules
# - Asynchronous initialization
# - Conditional loading
# - Minimal synchronous operations
# ========================================================

# Measure profile load time
$profileStartTime = Get-Date

# Set execution policy for this session (fast)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -Force

# Fast path setup - only essential environment variables
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:POWERSHELL_TELEMETRY_OPTOUT = "1"

# Fast aliases (immediate)
Set-Alias -Name ll -Value Get-ChildItem
Set-Alias -Name grep -Value Select-String
Set-Alias -Name touch -Value New-Item

# Lazy loading functions for heavy modules
function Import-AzureModules {
    if (-not (Get-Module -Name Az -ListAvailable)) {
        Write-Host "Loading Azure modules..." -ForegroundColor Yellow
        Import-Module Az -ErrorAction SilentlyContinue
    }
}

function Import-PoshGit {
    if (-not (Get-Module -Name posh-git -ListAvailable)) {
        Write-Host "Loading Posh-Git..." -ForegroundColor Yellow
        Import-Module posh-git -ErrorAction SilentlyContinue
    }
}

function Import-OhMyPosh {
    if (-not (Get-Module -Name oh-my-posh -ListAvailable)) {
        Write-Host "Loading Oh-My-Posh..." -ForegroundColor Yellow
        Import-Module oh-my-posh -ErrorAction SilentlyContinue
        oh-my-posh init pwsh | Invoke-Expression
    }
}

# Background initialization for non-critical components
$backgroundInit = {
    # Load heavy modules in background
    Start-Job -ScriptBlock {
        # Import modules that take time but aren't immediately needed
        Import-Module PSReadLine -ErrorAction SilentlyContinue
        Import-Module Terminal-Icons -ErrorAction SilentlyContinue
    } | Out-Null

    # Cache expensive operations
    Start-Job -ScriptBlock {
        # Pre-warm .NET assemblies or cache data
        [System.AppDomain]::CurrentDomain.GetAssemblies() | Out-Null
    } | Out-Null
}

# Start background initialization (non-blocking)
Start-Job -ScriptBlock $backgroundInit | Out-Null

# Fast prompt setup (immediate)
function prompt {
    $lastCommand = Get-History -Count 1
    if ($lastCommand) {
        $duration = $lastCommand.EndExecutionTime - $lastCommand.StartExecutionTime
        $durationString = if ($duration.TotalSeconds -gt 1) {
            " [$($duration.TotalSeconds.ToString("F2"))s]"
        } else { "" }
    }

    "$pwd$durationString$('>' * ($nestedPromptLevel + 1)) "
}

# Lazy loading aliases - only load when first used
$lazyLoadAliases = @{
    'az' = { Import-AzureModules }
    'git' = { Import-PoshGit }
    'posh' = { Import-OhMyPosh }
}

# Override default aliases to trigger lazy loading
foreach ($cmd in $lazyLoadAliases.Keys) {
    if (Get-Command $cmd -ErrorAction SilentlyContinue) {
        $originalCommand = Get-Command $cmd
        $lazyLoader = $lazyLoadAliases[$cmd]

        # Create wrapper function
        $wrapper = @"
function global:$cmd {
    `$lazyLoader.Invoke()
    & `$originalCommand @args
}
"@
        Invoke-Expression $wrapper
    }
}

# Fast utility functions
function Get-ProfileLoadTime {
    $loadTime = (Get-Date) - $profileStartTime
    Write-Host "Profile loaded in: $($loadTime.TotalMilliseconds)ms" -ForegroundColor Green
}

function Reload-Profile {
    . $PROFILE
    Write-Host "Profile reloaded!" -ForegroundColor Green
}

# Performance monitoring
function Start-ProfileTimer { $global:profileTimer = Get-Date }
function Stop-ProfileTimer {
    if ($global:profileTimer) {
        $elapsed = (Get-Date) - $global:profileTimer
        Write-Host "Operation completed in: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Cyan
        Remove-Variable profileTimer -Scope Global
    }
}

# Fast path completion (if PSReadLine is available)
if (Get-Module -Name PSReadLine -ListAvailable) {
    # Enable fast menu completion
    Set-PSReadLineOption -PredictionSource History
    Set-PSReadLineOption -PredictionViewStyle ListView
    Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete
}

# Profile load time reporting
$profileLoadTime = (Get-Date) - $profileStartTime
if ($profileLoadTime.TotalMilliseconds -gt 1000) {
    Write-Host "Profile loaded in: $($profileLoadTime.TotalMilliseconds)ms (consider optimization)" -ForegroundColor Yellow
} elseif ($profileLoadTime.TotalMilliseconds -gt 500) {
    Write-Host "Profile loaded in: $($profileLoadTime.TotalMilliseconds)ms" -ForegroundColor Green
}

#endregion