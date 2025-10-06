# MCP Server Status Checker
# Run this script to verify MCP server configuration and status

Write-Host "🔍 MCP Server Status Check" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan

# Check environment variables
Write-Host "`n📋 Environment Variables Status:" -ForegroundColor Yellow
$envVars = @('GITHUB_TOKEN', 'XAI_API_KEY', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')
$allSet = $true

foreach ($var in $envVars) {
    $value = [Environment]::GetEnvironmentVariable($var, 'User')
    if ($value) {
        Write-Host "✅ $var`: Configured" -ForegroundColor Green
    }
    else {
        Write-Host "❌ $var`: Missing" -ForegroundColor Red
        $allSet = $false
    }
}

# Check MCP configuration file
Write-Host "`n📄 MCP Configuration:" -ForegroundColor Yellow
$mcpConfigPath = "$PSScriptRoot\..\.vscode\mcp.json"
if (Test-Path $mcpConfigPath) {
    Write-Host "✅ MCP config file exists: $mcpConfigPath" -ForegroundColor Green

    try {
        $config = Get-Content $mcpConfigPath | ConvertFrom-Json
        Write-Host "✅ Config is valid JSON" -ForegroundColor Green
        Write-Host "📊 Configured servers: $($config.servers.PSObject.Properties.Name -join ', ')" -ForegroundColor White
    }
    catch {
        Write-Host "❌ Config file contains invalid JSON: $($_.Exception.Message)" -ForegroundColor Red
    }
}
else {
    Write-Host "❌ MCP config file not found: $mcpConfigPath" -ForegroundColor Red
}

# Check Azure MCP binary
Write-Host "`n🔧 Azure MCP Binary:" -ForegroundColor Yellow
try {
    $azmcp = Get-Command azmcp-win32-x64 -ErrorAction Stop
    Write-Host "✅ Azure MCP binary found: $($azmcp.Source)" -ForegroundColor Green
}
catch {
    Write-Host "❌ Azure MCP binary not found in PATH" -ForegroundColor Red
    Write-Host "   This may cause Azure MCP server to fail" -ForegroundColor Yellow
}

# Summary
Write-Host "`n📊 Summary:" -ForegroundColor Cyan
if ($allSet) {
    Write-Host "✅ All environment variables are configured" -ForegroundColor Green
    Write-Host "🚀 MCP servers should start successfully after VS Code restart" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Some environment variables are missing" -ForegroundColor Yellow
    Write-Host "💡 Run setup-mcp-environment.ps1 to configure missing variables" -ForegroundColor White
}

Write-Host "`n🔄 To test MCP servers:" -ForegroundColor Cyan
Write-Host "1. Restart VS Code completely" -ForegroundColor White
Write-Host "2. Open MCP output panel (View → Output → MCP)" -ForegroundColor White
Write-Host "3. Check for server startup messages" -ForegroundColor White
Write-Host "4. Try using MCP features in chat" -ForegroundColor White
