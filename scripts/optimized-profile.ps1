<#
.SYNOPSIS
    Wiley Widget Optimized PowerShell Profile with MCP Environment Support

.DESCRIPTION
    This profile provides optimized PowerShell initialization for the Wiley Widget project,
    including Model Context Protocol (MCP) environment setup with secure credential management.

    Key features:
    - Fast profile loading with background MCP initialization
    - Secure credential caching with Azure Key Vault integration
    - Lazy loading of heavy modules
    - Performance monitoring and optimization

.NOTES
    Author: Wiley Widget Development Team
    Version: 2.0.0
    Created: September 23, 2025
    Last Modified: September 23, 2025

    This script follows Microsoft PowerShell scripting best practices as documented at:
    https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/

.LINK
    https://docs.microsoft.com/en-us/powershell/scripting/developer/cmdlet/best-practices
#>

[CmdletBinding()]
param()

#region Initialization
#Requires -Version 5.1

# Set strict mode for better error handling
Set-StrictMode -Version Latest

# Enable verbose output if requested
$VerbosePreference = if ($PSBoundParameters.ContainsKey('Verbose')) { "Continue" } else { "SilentlyContinue" }

# Measure profile load time for performance monitoring
$script:ProfileStartTime = Get-Date
$script:ProfileLoadMetrics = @{
    StartTime = $script:ProfileStartTime
    Phases    = @()
}

function Write-ProfilePhase {
    <#
    .SYNOPSIS
        Records a profile loading phase for performance monitoring
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$PhaseName,

        [Parameter(Mandatory = $false)]
        [string]$Details
    )

    $phase = @{
        Name      = $PhaseName
        Timestamp = Get-Date
        Details   = $Details
    }
    $script:ProfileLoadMetrics.Phases += $phase

    Write-Verbose "Profile Phase: $PhaseName - $(Get-Date -Format 'HH:mm:ss.fff')"
    if ($Details) {
        Write-Verbose "  Details: $Details"
    }
}

Write-ProfilePhase -PhaseName "ProfileStart" -Details "Beginning profile initialization"
#endregion

# Initialize global MCP state
$global:MCPInitialized = $false
$script:MCPConfiguration = [PSCustomObject]@{
    # Azure Key Vault settings
    KeyVault         = [PSCustomObject]@{
        Name           = "wiley-widget-secrets"
        SubscriptionId = "89c2076a-8c6f-41fe-b03c-850d46a57abf"
        TenantId       = "cb097857-10d5-410b-8e09-6073de3ab035"
    }

    # Cache settings
    Cache            = [PSCustomObject]@{
        FilePath                   = "$env:APPDATA\WileyWidget\mcp-cache.json"
        ExpiryHours                = 24
        BackgroundRefreshThreshold = 0.8  # Refresh when 80% of TTL reached
    }

    # Secret mappings (environment variable name -> Key Vault secret name)
    SecretMappings   = [ordered]@{
        "GITHUB_TOKEN"           = "GITHUB-PAT"
        "XAI_API_KEY"            = "XAI-API-KEY"
        "SYNCFUSION_LICENSE_KEY" = "SYNCFUSION-LICENSE-KEY"
    }

    # Azure service principal credentials (loaded from secure storage)
    AzureCredentials = [PSCustomObject]@{
        ClientId       = $null
        ClientSecret   = $null
        TenantId       = $null
        SubscriptionId = $null
    }
}

# Initialize Syncfusion license registration state
$script:SyncfusionLicenseRegistered = $null

# Load Azure credentials from secure storage (environment variables or secure store)
function Initialize-AzureCredentials {
    <#
    .SYNOPSIS
        Initializes Azure service principal credentials from secure storage
    #>
    [CmdletBinding()]
    param()

    Write-ProfilePhase -PhaseName "AzureCredentialsInit" -Details "Loading Azure service principal credentials"

    $azureCreds = $script:MCPConfiguration.AzureCredentials

    # Try to load from environment variables first (most secure)
    $azureCreds.ClientId = [Environment]::GetEnvironmentVariable("AZURE_CLIENT_ID", "User")
    $azureCreds.ClientSecret = [Environment]::GetEnvironmentVariable("AZURE_CLIENT_SECRET", "User")
    $azureCreds.TenantId = [Environment]::GetEnvironmentVariable("AZURE_TENANT_ID", "User")
    $azureCreds.SubscriptionId = [Environment]::GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", "User")

    # Validate that all required credentials are present
    $missingCredentials = @()
    if (-not $azureCreds.ClientId) { $missingCredentials += "AZURE_CLIENT_ID" }
    if (-not $azureCreds.ClientSecret) { $missingCredentials += "AZURE_CLIENT_SECRET" }
    if (-not $azureCreds.TenantId) { $missingCredentials += "AZURE_TENANT_ID" }
    if (-not $azureCreds.SubscriptionId) { $missingCredentials += "AZURE_SUBSCRIPTION_ID" }

    if ($missingCredentials.Count -gt 0) {
        Write-Warning "Azure credentials not found in environment variables: $($missingCredentials -join ', ')"
        Write-Warning "Please set these environment variables or run the Azure setup script"
        return $false
    }

    Write-Verbose "Azure credentials loaded successfully"
    return $true
}
#endregion

#region Core Setup
Write-ProfilePhase -PhaseName "CoreSetup" -Details "Setting up core PowerShell environment"

# Set execution policy for current process only (secure approach)
try {
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process -ErrorAction Stop
    Write-Verbose "Execution policy set to RemoteSigned for current process"
}
catch {
    Write-Warning "Failed to set execution policy: $($_.Exception.Message)"
}

# Essential environment variables (telemetry opt-out)
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:POWERSHELL_TELEMETRY_OPTOUT = "1"

Write-Verbose "Core environment setup completed"
#endregion

#region MCP Environment Management
function Test-CacheValidity {
    <#
    .SYNOPSIS
        Tests if the MCP cache is valid and not expired
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CacheFile,

        [Parameter(Mandatory = $true)]
        [int]$ExpiryHours
    )

    if (-not (Test-Path $CacheFile)) {
        Write-Verbose "Cache file does not exist: $CacheFile"
        return $false
    }

    try {
        $cacheData = Get-Content $CacheFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop

        # Validate cache structure
        if (-not $cacheData.Timestamp -or -not $cacheData.Secrets) {
            Write-Verbose "Cache file has invalid structure"
            return $false
        }

        $cacheAge = (Get-Date) - [DateTime]::Parse($cacheData.Timestamp)
        $isValid = $cacheAge.TotalHours -lt $ExpiryHours

        Write-Verbose "Cache age: $([math]::Round($cacheAge.TotalHours, 2)) hours, Valid: $isValid"
        return $isValid
    }
    catch {
        Write-Verbose "Failed to read cache file: $($_.Exception.Message)"
        return $false
    }
}

function Get-CachedSecrets {
    <#
    .SYNOPSIS
        Retrieves secrets from the MCP cache
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CacheFile
    )

    try {
        $cacheData = Get-Content $CacheFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop

        if ($cacheData.Secrets -and $cacheData.Secrets -is [PSCustomObject]) {
            # Convert PSCustomObject to hashtable
            $secrets = @{}
            $cacheData.Secrets.PSObject.Properties | ForEach-Object {
                $secrets[$_.Name] = $_.Value
            }
            return $secrets
        }

        return @{}
    }
    catch {
        Write-Verbose "Failed to retrieve cached secrets: $($_.Exception.Message)"
        return @{}
    }
}

function Set-EnvironmentVariables {
    <#
    .SYNOPSIS
        Sets environment variables from a hashtable
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Variables,

        [Parameter(Mandatory = $false)]
        [EnvironmentVariableTarget]$Scope = [EnvironmentVariableTarget]::User
    )

    foreach ($key in $Variables.Keys) {
        $value = $Variables[$key]
        if ($value -and $value -is [string]) {
            if ($PSCmdlet.ShouldProcess("Environment variable '$key'", "Set value")) {
                [Environment]::SetEnvironmentVariable($key, $value, $Scope)
                Write-Verbose "Set environment variable: $key"
            }
        }
    }
}

function Update-MCPCacheInternal {
    <#
    .SYNOPSIS
        Updates the MCP cache with fresh secrets from Azure Key Vault
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$CacheFile
    )

    if (-not $PSCmdlet.ShouldProcess("MCP Cache", "Update from Azure Key Vault")) {
        return
    }

    Write-ProfilePhase -PhaseName "CacheUpdate" -Details "Refreshing MCP cache from Azure Key Vault"

    $vaultName = $Configuration.KeyVault.Name
    $secretMappings = $Configuration.SecretMappings
    $cachedSecrets = @{}

    Write-Verbose "Retrieving secrets from Azure Key Vault: $vaultName"

    foreach ($envVar in $secretMappings.Keys) {
        $kvSecret = $secretMappings[$envVar]

        try {
            # Use Azure CLI to retrieve secret (secure approach)
            $secretValue = az keyvault secret show --vault-name $vaultName --name $kvSecret --query value -o tsv 2>$null

            if ($secretValue -and $secretValue -ne "") {
                $cachedSecrets[$envVar] = $secretValue
                Write-Verbose "Retrieved secret: $envVar"
            }
            else {
                Write-Warning "Secret not found in Key Vault: $kvSecret"
            }
        }
        catch {
            Write-Warning "Failed to retrieve secret '\''$kvSecret'\'' from Key Vault: $($_.Exception.Message)"
        }
    }

    # Save to cache
    try {
        $cacheData = [PSCustomObject]@{
            Timestamp = (Get-Date).ToString("o")
            Secrets   = $cachedSecrets
        }

        $cacheDirectory = Split-Path $CacheFile -Parent
        if (-not (Test-Path $cacheDirectory)) {
            New-Item -ItemType Directory -Path $cacheDirectory -Force -ErrorAction Stop | Out-Null
        }

        $cacheData | ConvertTo-Json -Depth 10 | Set-Content $CacheFile -Force -ErrorAction Stop
        Write-Verbose "Cache updated successfully with $($cachedSecrets.Count) secrets"
    }
    catch {
        Write-Error "Failed to update MCP cache: $($_.Exception.Message)"
    }
}

function Start-BackgroundCacheRefresh {
    <#
    .SYNOPSIS
        Starts a background job to refresh the MCP cache
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Configuration,

        [Parameter(Mandatory = $true)]
        [string]$CacheFile
    )

    if (-not $PSCmdlet.ShouldProcess("Background MCP Cache Refresh", "Start job")) {
        return
    }

    $jobName = "MCP-Cache-Refresh"

    # Clean up any existing job with the same name
    Get-Job -Name $jobName -ErrorAction SilentlyContinue | Remove-Job -Force -ErrorAction SilentlyContinue

    Write-Verbose "Starting background cache refresh job"

    $job = Start-Job -Name $jobName -ScriptBlock {
        param($Config, $CacheFilePath)

        try {
            # Call the internal cache update function
            Update-MCPCacheInternal -Configuration $Config -CacheFile $CacheFilePath
        }
        catch {
            Write-Error "Background cache refresh failed: $($_.Exception.Message)"
        }
    } -ArgumentList $Configuration, $CacheFile

    Write-Verbose "Background cache refresh job started: $($job.Id)"
}

function Initialize-MCPEnvironment {
    <#
    .SYNOPSIS
        Initializes the MCP environment with caching and background refresh
    #>
    [CmdletBinding()]
    param()

    if ($global:MCPInitialized) {
        Write-Verbose "MCP environment already initialized"
        return
    }

    Write-ProfilePhase -PhaseName "MCPInit" -Details "Initializing MCP environment"

    $config = $script:MCPConfiguration
    $cacheFile = $config.Cache.FilePath
    $cacheExpiryHours = $config.Cache.ExpiryHours

    # Initialize Azure credentials first
    if (-not (Initialize-AzureCredentials)) {
        Write-Warning "Azure credentials not available. MCP environment may not function correctly."
    }

    # Check cache validity
    if (Test-CacheValidity -CacheFile $cacheFile -ExpiryHours $cacheExpiryHours) {
        Write-Verbose "Loading MCP environment from cache"

        # Load secrets from cache
        $cachedSecrets = Get-CachedSecrets -CacheFile $cacheFile
        Set-EnvironmentVariables -Variables $cachedSecrets

        # Set Azure credentials
        $azureCreds = $config.AzureCredentials
        $azureVars = @{
            "AZURE_CLIENT_ID"       = $azureCreds.ClientId
            "AZURE_CLIENT_SECRET"   = $azureCreds.ClientSecret
            "AZURE_TENANT_ID"       = $azureCreds.TenantId
            "AZURE_SUBSCRIPTION_ID" = $azureCreds.SubscriptionId
        }
        Set-EnvironmentVariables -Variables $azureVars

        Write-Host "🚀 MCP Environment loaded from cache" -ForegroundColor Green

        # Start background refresh if cache is getting stale
        $cacheAge = (Get-Date) - [DateTime]::Parse((Get-Content $cacheFile -Raw | ConvertFrom-Json).Timestamp)
        if ($cacheAge.TotalHours -gt ($cacheExpiryHours * $config.Cache.BackgroundRefreshThreshold)) {
            Start-BackgroundCacheRefresh -Configuration $config -CacheFile $cacheFile
        }
    }
    else {
        Write-Verbose "Cache invalid or missing, refreshing from Key Vault"

        # Start background refresh for initial load
        Start-BackgroundCacheRefresh -Configuration $config -CacheFile $cacheFile

        # Set Azure credentials immediately (always available)
        $azureCreds = $config.AzureCredentials
        $azureVars = @{
            "AZURE_CLIENT_ID"       = $azureCreds.ClientId
            "AZURE_CLIENT_SECRET"   = $azureCreds.ClientSecret
            "AZURE_TENANT_ID"       = $azureCreds.TenantId
            "AZURE_SUBSCRIPTION_ID" = $azureCreds.SubscriptionId
        }
        Set-EnvironmentVariables -Variables $azureVars

        Write-Host "🔄 Initializing MCP Environment..." -ForegroundColor Yellow
    }

    $global:MCPInitialized = $true
}
#endregion

#region Syncfusion License Management
function Register-SyncfusionLicense {
    <#
    .SYNOPSIS
        Registers the Syncfusion license key for the current session
    #>
    [CmdletBinding()]
    param()

    # Only attempt registration if we haven't tried before
    if ($script:SyncfusionLicenseRegistered -ne $null) {
        return $script:SyncfusionLicenseRegistered
    }

    Write-ProfilePhase -PhaseName "SyncfusionLicense" -Details "Checking Syncfusion license"

    $licenseKey = [Environment]::GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY", "User")

    if (-not $licenseKey -or $licenseKey -like "*YOUR_*" -or $licenseKey -like "*PLACEHOLDER*") {
        Write-Verbose "Syncfusion license key not found or is placeholder"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }

    # Check if any Syncfusion assemblies are loaded
    $syncfusionAssemblies = [System.AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.FullName -like "*Syncfusion*" }

    if (-not $syncfusionAssemblies) {
        Write-Verbose "No Syncfusion assemblies loaded yet - license registration deferred"
        $script:SyncfusionLicenseRegistered = $null  # Null means not tried yet
        return $null
    }

    try {
        # Try the modern Syncfusion licensing API using reflection
        foreach ($assembly in $syncfusionAssemblies) {
            $licenseProviderType = $assembly.GetTypes() | Where-Object { $_.Name -like "*License*" -and $_.Name -notlike "*Exception*" } | Select-Object -First 1
            if ($licenseProviderType) {
                $registerMethod = $licenseProviderType.GetMethods() | Where-Object { $_.Name -like "*Register*" -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
                if ($registerMethod) {
                    $registerMethod.Invoke($null, @($licenseKey))
                    Write-Verbose "Syncfusion license registered successfully"
                    $script:SyncfusionLicenseRegistered = $true
                    return $true
                }
            }
        }

        Write-Verbose "No suitable Syncfusion licensing API found"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }
    catch {
        Write-Verbose "Failed to register Syncfusion license: $($_.Exception.Message)"
        $script:SyncfusionLicenseRegistered = $false
        return $false
    }
}
#endregion

#region Module Management
Write-ProfilePhase -PhaseName "ModuleSetup" -Details "Setting up module lazy loading"

# Lazy loading functions for heavy modules (Microsoft recommended approach)
function Import-AzureModules {
    <#
    .SYNOPSIS
        Lazily imports Azure PowerShell modules
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name Az -ListAvailable)) {
        Write-Host "Loading Azure modules..." -ForegroundColor Yellow
        Import-Module Az -ErrorAction SilentlyContinue -Verbose:$false
    }
}

function Import-PoshGit {
    <#
    .SYNOPSIS
        Lazily imports Posh-Git module
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name posh-git -ListAvailable)) {
        Write-Host "Loading Posh-Git..." -ForegroundColor Yellow
        Import-Module posh-git -ErrorAction SilentlyContinue -Verbose:$false
    }
}

function Import-OhMyPosh {
    <#
    .SYNOPSIS
        Lazily imports Oh-My-Posh module and initializes it
    #>
    [CmdletBinding()]
    param()

    if (-not (Get-Module -Name oh-my-posh -ListAvailable)) {
        Write-Host "Loading Oh-My-Posh..." -ForegroundColor Yellow
        Import-Module oh-my-posh -ErrorAction SilentlyContinue -Verbose:$false
        if (Get-Command oh-my-posh -ErrorAction SilentlyContinue) {
            oh-my-posh init pwsh | Invoke-Expression
        }
    }
}

# Background initialization for non-critical components
$backgroundInit = {
    try {
        # Load heavy modules in background (Microsoft recommended pattern)
        Start-Job -ScriptBlock {
            try {
                Import-Module PSReadLine -ErrorAction SilentlyContinue -Verbose:$false
                Import-Module Terminal-Icons -ErrorAction SilentlyContinue -Verbose:$false
            }
            catch {
                # Silent failure for background operations
            }
        } | Out-Null
    }
    catch {
        # Silent failure for background initialization
    }
}

# Start background initialization (non-blocking)
try {
    Start-Job -ScriptBlock $backgroundInit -ErrorAction SilentlyContinue | Out-Null
}
catch {
    Write-Verbose "Failed to start background initialization: $($_.Exception.Message)"
}
#endregion

#region Prompt and Aliases
Write-ProfilePhase -PhaseName "PromptSetup" -Details "Setting up prompt and aliases"

# Fast aliases (immediate setup)
$fastAliases = @{
    "ll"    = "Get-ChildItem"
    "grep"  = "Select-String"
    "touch" = "New-Item"
}

foreach ($alias in $fastAliases.GetEnumerator()) {
    try {
        Set-Alias -Name $alias.Key -Value $alias.Value -ErrorAction Stop
    }
    catch {
        Write-Verbose "Failed to set alias '\''$($alias.Key)'\'': $($_.Exception.Message)"
    }
}

# Optimized prompt function (Microsoft recommended pattern)
function prompt {
    <#
    .SYNOPSIS
        Custom prompt function with performance metrics
    #>
    $lastCommand = Get-History -Count 1 -ErrorAction SilentlyContinue
    if ($lastCommand) {
        $duration = $lastCommand.EndExecutionTime - $lastCommand.StartExecutionTime
        $durationString = if ($duration.TotalSeconds -gt 1) {
            " [$($duration.TotalSeconds.ToString("F2"))s]"
        }
        else { "" }
    }
    else {
        $durationString = ""
    }

    "$pwd$durationString$('>' * ($nestedPromptLevel + 1)) "
}

# Lazy loading aliases - only load when first used (Microsoft recommended)
$lazyLoadAliases = [ordered]@{
    "az"   = ${function:Import-AzureModules}
    "git"  = ${function:Import-PoshGit}
    "posh" = ${function:Import-OhMyPosh}
}

# Override default aliases to trigger lazy loading
foreach ($cmd in $lazyLoadAliases.Keys) {
    if (Get-Command $cmd -ErrorAction SilentlyContinue) {
        # Store the original command before creating wrapper
        $originalCommand = Get-Command $cmd

        # Create wrapper function (secure approach)
        $wrapperScript = @"
function global:$cmd {
    (`$lazyLoadAliases['$cmd']).Invoke()
    & `"$($originalCommand.Source)`" @args
}
"@
        try {
            Invoke-Expression $wrapperScript
            Write-Verbose "Created lazy loading wrapper for: $cmd"
        }
        catch {
            Write-Verbose "Failed to create lazy loading wrapper for '\''$cmd'\'': $($_.Exception.Message)"
        }
    }
}
#endregion

#region Utility Functions
function Get-ProfileLoadTime {
    <#
    .SYNOPSIS
        Gets the profile load time for performance monitoring
    #>
    [CmdletBinding()]
    param()

    $loadTime = (Get-Date) - $script:ProfileStartTime
    Write-Host "Profile loaded in: $($loadTime.TotalMilliseconds)ms" -ForegroundColor Green
}

function Get-ProfileMetrics {
    <#
    .SYNOPSIS
        Displays detailed profile loading metrics
    #>
    [CmdletBinding()]
    param()

    $totalTime = (Get-Date) - $script:ProfileStartTime

    Write-Host "📊 Profile Load Metrics" -ForegroundColor Cyan
    Write-Host "======================" -ForegroundColor Cyan
    Write-Host "Total load time: $([math]::Round($totalTime.TotalMilliseconds, 0))ms" -ForegroundColor White

    if ($script:ProfileLoadMetrics.Phases) {
        Write-Host "`nPhase breakdown:" -ForegroundColor Yellow
        for ($i = 1; $i -lt $script:ProfileLoadMetrics.Phases.Count; $i++) {
            $phase = $script:ProfileLoadMetrics.Phases[$i]
            $prevPhase = $script:ProfileLoadMetrics.Phases[$i - 1]
            $phaseTime = $phase.Timestamp - $prevPhase.Timestamp

            Write-Host ("  {0}: {1}ms" -f $phase.Name, [math]::Round($phaseTime.TotalMilliseconds, 0)) -ForegroundColor White
            if ($phase.Details) {
                Write-Host "    $($phase.Details)" -ForegroundColor Gray
            }
        }
    }
}

function Update-Profile {
    <#
    .SYNOPSIS
        Updates/reloads the PowerShell profile
    #>
    [CmdletBinding()]
    param()

    try {
        . $PROFILE
        Write-Host "Profile updated successfully!" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to update profile: $($_.Exception.Message)"
    }
}
#endregion

#region MCP Cache Management Functions
function Clear-MCPCache {
    <#
    .SYNOPSIS
        Clears the MCP environment cache, forcing refresh from Key Vault

    .DESCRIPTION
        This function removes the cached MCP secrets and initiates a fresh
        download from Azure Key Vault. Use this when you suspect the cache
        is corrupted or when secrets have been updated in Key Vault.

    .EXAMPLE
        Clear-MCPCache

        Clears the cache and forces a refresh from Azure Key Vault.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    $config = $script:MCPConfiguration
    $cacheFile = $config.Cache.FilePath

    if ($PSCmdlet.ShouldProcess("MCP Cache", "Clear and refresh")) {
        if (Test-Path $cacheFile) {
            Remove-Item $cacheFile -Force -ErrorAction Stop
            Write-Host "🗑️ MCP cache cleared" -ForegroundColor Yellow
        }

        # Force refresh
        Update-MCPCacheInternal -Configuration $config -CacheFile $cacheFile
        Write-Host "🔄 MCP cache refresh initiated" -ForegroundColor Green
    }
}

function Get-MCPCacheStatus {
    <#
    .SYNOPSIS
        Shows the current status of MCP cache and environment variables

    .DESCRIPTION
        Displays detailed information about the MCP cache status, including
        cache file location, age, cached secrets, and current environment
        variable values.

    .EXAMPLE
        Get-MCPCacheStatus

        Displays the current MCP cache and environment variable status.
    #>
    [CmdletBinding()]
    param()

    $config = $script:MCPConfiguration
    $cacheFile = $config.Cache.FilePath

    Write-Host "🔍 MCP Cache Status" -ForegroundColor Cyan
    Write-Host "==================" -ForegroundColor Cyan

    # Cache file status
    if (Test-Path $cacheFile) {
        try {
            $cacheData = Get-Content $cacheFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
            $cacheAge = (Get-Date) - [DateTime]::Parse($cacheData.Timestamp)
            Write-Host "📁 Cache file: $cacheFile" -ForegroundColor White
            Write-Host "⏰ Cache age: $([math]::Round($cacheAge.TotalHours, 1)) hours" -ForegroundColor White

            $cachedSecretNames = if ($cacheData.Secrets) {
                $cacheData.Secrets.PSObject.Properties.Name -join ", "
            }
            else { "None" }
            Write-Host "📋 Cached secrets: $cachedSecretNames" -ForegroundColor White
        }
        catch {
            Write-Host "❌ Cache file corrupted" -ForegroundColor Red
        }
    }
    else {
        Write-Host "📁 Cache file: Not found" -ForegroundColor Yellow
    }

    # Environment variables status
    Write-Host "`n🔧 Environment Variables:" -ForegroundColor Cyan
    foreach ($envVar in $config.SecretMappings.Keys) {
        $value = [Environment]::GetEnvironmentVariable($envVar, "User")
        if ($value) {
            $maskedValue = $value.Substring(0, [Math]::Min(10, $value.Length)) + "..."
            Write-Host "  ✅ $envVar`: $maskedValue" -ForegroundColor Green
        }
        else {
            Write-Host "  ❌ $envVar`: Not set" -ForegroundColor Red
        }
    }

    # Azure credentials status
    Write-Host "`n☁️ Azure Credentials:" -ForegroundColor Cyan
    $azureVars = @("AZURE_CLIENT_ID", "AZURE_CLIENT_SECRET", "AZURE_TENANT_ID", "AZURE_SUBSCRIPTION_ID")
    foreach ($var in $azureVars) {
        $value = [Environment]::GetEnvironmentVariable($var, "User")
        if ($value) {
            Write-Host "  ✅ $var`: Set" -ForegroundColor Green
        }
        else {
            Write-Host "  ❌ $var`: Not set" -ForegroundColor Red
        }
    }
}

function Update-MCPCache {
    <#
    .SYNOPSIS
        Forces an immediate refresh of MCP cache from Key Vault

    .DESCRIPTION
        Immediately refreshes the MCP cache by downloading fresh secrets
        from Azure Key Vault. This is a synchronous operation that may take
        some time depending on network conditions.

    .EXAMPLE
        Update-MCPCache

        Forces an immediate refresh of the MCP cache from Azure Key Vault.
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("MCP Cache", "Refresh from Azure Key Vault")) {
        Write-Host "🔄 Refreshing MCP cache from Key Vault..." -ForegroundColor Yellow
        $config = $script:MCPConfiguration
        Update-MCPCacheInternal -Configuration $config -CacheFile $config.Cache.FilePath
    }
}
#endregion

#region Performance Monitoring
function Start-ProfileTimer {
    <#
    .SYNOPSIS
        Starts a profile performance timer
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("Profile Timer", "Start")) {
        $global:ProfileTimer = Get-Date
    }
}

function Stop-ProfileTimer {
    <#
    .SYNOPSIS
        Stops the profile performance timer and displays elapsed time
    #>
    [CmdletBinding(SupportsShouldProcess = $true)]
    param()

    if ($PSCmdlet.ShouldProcess("Profile Timer", "Stop and display")) {
        if ($global:ProfileTimer) {
            $elapsed = (Get-Date) - $global:ProfileTimer
            Write-Host "Operation completed in: $($elapsed.TotalMilliseconds)ms" -ForegroundColor Cyan
            Remove-Variable ProfileTimer -Scope Global -ErrorAction SilentlyContinue
        }
    }
}
#endregion

#region PSReadLine Configuration
Write-ProfilePhase -PhaseName "PSReadLineSetup" -Details "Configuring PSReadLine for enhanced experience"

# Fast path completion (if PSReadLine is available) - Microsoft recommended settings
if (Get-Module -Name PSReadLine -ListAvailable) {
    try {
        # Enable fast menu completion (Microsoft recommended)
        Set-PSReadLineOption -PredictionSource History -ErrorAction SilentlyContinue
        Set-PSReadLineOption -PredictionViewStyle ListView -ErrorAction SilentlyContinue
        Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete -ErrorAction SilentlyContinue

        Write-Verbose "PSReadLine configured with enhanced completion"
    }
    catch {
        Write-Verbose "Failed to configure PSReadLine: $($_.Exception.Message)"
    }
}
#endregion

#region MCP Status Function
function Get-MCPStatus {
    <#
    .SYNOPSIS
        Gets the current status of MCP initialization
    #>
    [CmdletBinding()]
    param()

    # Check if MCP is initialized
    if ($global:MCPInitialized) {
        Write-Host "✅ MCP Environment: Initialized" -ForegroundColor Green
        return $true
    }

    # MCP initialization is lazy - initialize now if needed
    Write-Host "🔄 Initializing MCP Environment..." -ForegroundColor Yellow
    try {
        Initialize-MCPEnvironment
        Write-Host "✅ MCP Environment: Initialized" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "❌ MCP Environment: Initialization failed - $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}
#endregion

#region Finalization
Write-ProfilePhase -PhaseName "Finalization" -Details "Completing profile initialization"

# Initialize MCP environment lazily (only when first MCP command is used)
# This avoids blocking profile load with Azure CLI calls
Write-ProfilePhase -PhaseName "MCPInit" -Details "MCP initialization deferred until needed"

# Register Syncfusion license asynchronously (deferred until assemblies are loaded)
try {
    $syncfusionResult = Register-SyncfusionLicense
    if ($syncfusionResult -eq $true) {
        Write-ProfilePhase -PhaseName "SyncfusionInit" -Details "Syncfusion license registered successfully"
    }
    elseif ($syncfusionResult -eq $null) {
        Write-ProfilePhase -PhaseName "SyncfusionInit" -Details "Syncfusion license registration deferred"
    }
    else {
        Write-Verbose "Syncfusion license registration failed"
    }
}
catch {
    Write-Verbose "Failed to check Syncfusion license: $($_.Exception.Message)"
}

# Profile load time reporting (only if slow loading detected)
$profileLoadTime = (Get-Date) - $script:ProfileStartTime
if ($profileLoadTime.TotalMilliseconds -gt 1000) {
    Write-Host "Profile loaded in: $([math]::Round($profileLoadTime.TotalMilliseconds, 0))ms (consider optimization)" -ForegroundColor Yellow
}
elseif ($profileLoadTime.TotalMilliseconds -gt 500) {
    Write-Host "Profile loaded in: $([math]::Round($profileLoadTime.TotalMilliseconds, 0))ms" -ForegroundColor Green
}

Write-ProfilePhase -PhaseName "ProfileComplete" -Details "Profile initialization completed"
Write-Verbose "Wiley Widget optimized profile loaded successfully"
#endregion
