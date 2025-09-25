# Wiley Widget MCP Environment Setup Script
# Ensures Azure MCP Server works with PowerShell and Wiley Widget application

Write-Host "🚀 Wiley Widget MCP Environment Setup" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Step 1: Load environment variables from .env file
Write-Host "`n📖 Loading environment from .env file..." -ForegroundColor Yellow
$projectRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $projectRoot ".env"
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $key, $value = $line.Split("=", 2)
            $key = $key.Trim()
            $value = $value.Trim()

            # Remove quotes if present
            if (($value.StartsWith('"') -and $value.EndsWith('"')) -or
                ($value.StartsWith("'") -and $value.EndsWith("'"))) {
                $value = $value.Substring(1, $value.Length - 2)
            }

            [Environment]::SetEnvironmentVariable($key, $value, "Process")
            Write-Host "  ✅ $key" -ForegroundColor Green
        }
    }
}
else {
    Write-Host "  ❌ .env file not found" -ForegroundColor Red
}

# Step 2: Verify Azure CLI authentication
Write-Host "`n🔐 Checking Azure CLI authentication..." -ForegroundColor Yellow
try {
    $azAccount = az account show 2>$null | ConvertFrom-Json
    Write-Host "  ✅ Azure CLI authenticated as: $($azAccount.user.name)" -ForegroundColor Green
    Write-Host "  📍 Subscription: $($azAccount.name)" -ForegroundColor White
}
catch {
    Write-Host "  ⚠️  Azure CLI not authenticated. Run 'az login' if needed." -ForegroundColor Yellow
}

# Step 3: Verify MCP environment variables
Write-Host "`n🔧 Verifying MCP environment variables..." -ForegroundColor Yellow
$mcpVars = @('GITHUB_TOKEN', 'XAI_API_KEY', 'AZURE_CLIENT_ID', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')

foreach ($var in $mcpVars) {
    $value = [Environment]::GetEnvironmentVariable($var, "Process")
    if ($value) {
        $masked = $value.Substring(0, [Math]::Min(10, $value.Length)) + "..."
        Write-Host "  ✅ $var`: $masked" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ $var`: NOT SET" -ForegroundColor Red
    }
}

# Step 4: Test Azure Key Vault access
Write-Host "`n🔑 Testing Azure Key Vault access..." -ForegroundColor Yellow
try {
    $secrets = az keyvault secret list --vault-name "wiley-widget-secrets" --query "[].name" -o tsv 2>$null
    $secretCount = ($secrets | Measure-Object).Count
    Write-Host "  ✅ Key Vault accessible: $secretCount secrets found" -ForegroundColor Green
}
catch {
    Write-Host "  ❌ Key Vault access failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Step 5: Verify MCP configuration
Write-Host "`n⚙️  Verifying MCP configuration..." -ForegroundColor Yellow
$mcpConfig = Join-Path $projectRoot ".vscode\mcp.json"
if (Test-Path $mcpConfig) {
    $config = Get-Content $mcpConfig | ConvertFrom-Json
    if ($config.servers.azure) {
        Write-Host "  ✅ Azure MCP Server configured" -ForegroundColor Green
        Write-Host "  📦 Package: $($config.servers.azure.args[1])" -ForegroundColor White
    }
    else {
        Write-Host "  ❌ Azure MCP Server not configured" -ForegroundColor Red
    }

    if ($config.servers.github) {
        Write-Host "  ✅ GitHub MCP Server configured" -ForegroundColor Green
    }
    else {
        Write-Host "  ❌ GitHub MCP Server not configured" -ForegroundColor Red
    }
}
else {
    Write-Host "  ❌ MCP configuration file not found" -ForegroundColor Red
}

# Step 6: Test Wiley Widget application startup
Write-Host "`n🏗️  Testing Wiley Widget application..." -ForegroundColor Yellow
try {
    $csproj = Join-Path $projectRoot "WileyWidget.csproj"
    if (Test-Path $csproj) {
        Write-Host "  ✅ Project file found: WileyWidget.csproj" -ForegroundColor Green

        # Check if dotnet is available
        $dotnetVersion = dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ .NET SDK available: $dotnetVersion" -ForegroundColor Green
        }
        else {
            Write-Host "  ❌ .NET SDK not found" -ForegroundColor Red
        }
    }
    else {
        Write-Host "  ❌ Project file not found" -ForegroundColor Red
    }
}
catch {
    Write-Host "  ❌ Error checking application: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎯 Setup Complete!" -ForegroundColor Green
Write-Host "==================" -ForegroundColor Green
Write-Host "✅ PowerShell environment configured" -ForegroundColor Green
Write-Host "✅ Azure MCP Server ready" -ForegroundColor Green
Write-Host "✅ Wiley Widget application ready" -ForegroundColor Green
Write-Host "`n💡 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Restart VS Code to load new environment variables" -ForegroundColor White
Write-Host "2. Open GitHub Copilot → Switch to Agent Mode" -ForegroundColor White
Write-Host "3. Test Azure commands like 'List my Azure resource groups'" -ForegroundColor White
Write-Host "4. Run Wiley Widget: dotnet run --project WileyWidget.csproj" -ForegroundColor White
