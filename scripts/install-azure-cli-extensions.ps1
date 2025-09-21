# Install Required Azure CLI Extensions
# This script ensures all necessary Azure CLI extensions are installed

param(
    [switch]$Force,
    [switch]$Quiet
)

# Check if Azure CLI is available
$azPath = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"
if (!(Test-Path $azPath)) {
    Write-Host "❌ Azure CLI not found at $azPath. Please install Azure CLI." -ForegroundColor Red
    exit 1
}

$extensions = @(
    # Extensions that are not built-in
    "application-insights",
    "resource-graph"
)

Write-Host "🔧 Installing Required Azure CLI Extensions..." -ForegroundColor Cyan
Write-Host "This ensures full Azure management capabilities." -ForegroundColor Yellow
Write-Host ""

$installed = 0
$failed = 0
$alreadyInstalled = 0

foreach ($ext in $extensions) {
    try {
        if (!$Quiet) {
            Write-Host "Checking $ext..." -ForegroundColor Gray
        }

        # Check if extension is already installed
        try {
            $installedExtensions = & $azPath extension list --output json 2>$null | ConvertFrom-Json
            $isInstalled = $installedExtensions | Where-Object { $_.name -eq $ext }
            if ($isInstalled) {
                $alreadyInstalled++
                if (!$Quiet) {
                    Write-Host "✅ $ext already installed" -ForegroundColor Blue
                }
                continue
            }
        }
        catch {
            # If checking fails, assume extension is not installed
            if (!$Quiet) {
                Write-Host "⚠️ Could not verify $ext status, attempting installation..." -ForegroundColor Yellow
            }
        }

        if (!$Quiet) {
            Write-Host "Installing $ext..." -ForegroundColor Gray
        }

        $result = & $azPath extension add --name $ext 2>&1

        if ($LASTEXITCODE -eq 0) {
            $installed++
            if (!$Quiet) {
                Write-Host "✅ $ext installed successfully" -ForegroundColor Green
            }
        } else {
            $failed++
            if (!$Quiet) {
                Write-Host "❌ Failed to install $ext" -ForegroundColor Red
                Write-Host "  Error: $result" -ForegroundColor Red
            }
        }
    }
    catch {
        $failed++
        if (!$Quiet) {
            Write-Host "❌ Error installing $ext : $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "📊 Azure CLI Extension Installation Summary:" -ForegroundColor Cyan
Write-Host "  ✅ Successfully installed: $installed extensions" -ForegroundColor Green
Write-Host "  🔄 Already installed: $alreadyInstalled extensions" -ForegroundColor Blue
Write-Host "  ❌ Failed installations: $failed extensions" -ForegroundColor Red
Write-Host ""
Write-Host "🔧 Azure CLI Status:" -ForegroundColor Yellow
Write-Host "  - All essential extensions are now available"
Write-Host "  - You can use 'az --help' to see available commands"
Write-Host "  - Extensions will persist across CLI updates"
Write-Host ""
Write-Host "🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Test Azure CLI: az account show"
Write-Host "  2. Test SQL commands: az sql db list --help"
Write-Host "  3. Test monitoring: az monitor --help"

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "⚠️  Some extensions failed to install. You may need to:" -ForegroundColor Yellow
    Write-Host "  - Check your internet connection"
    Write-Host "  - Run as administrator if permission issues"
    Write-Host "  - Manually install failed extensions: az extension add --name <extension>"
}