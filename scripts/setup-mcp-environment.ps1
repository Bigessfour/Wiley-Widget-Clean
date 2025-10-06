# MCP Environment Setup Script
# This script helps configure the required environment variables for MCP servers

Write-Host "🔧 MCP Environment Setup" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Check current environment variables
Write-Host "`n📋 Current MCP Environment Variables:" -ForegroundColor Yellow
$envVars = @('GITHUB_TOKEN', 'XAI_API_KEY', 'AZURE_CLIENT_ID', 'AZURE_CLIENT_SECRET', 'AZURE_TENANT_ID', 'AZURE_SUBSCRIPTION_ID')

foreach ($var in $envVars) {
    $value = [Environment]::GetEnvironmentVariable($var, 'User')
    if ($value) {
        Write-Host "✅ $var`: Set (length: $($value.Length))" -ForegroundColor Green
    }
    else {
        Write-Host "❌ $var`: NOT SET" -ForegroundColor Red
    }
}

Write-Host "`n🔑 To set up MCP servers, you need to configure these environment variables:" -ForegroundColor Cyan
Write-Host "1. GITHUB_TOKEN - Get from: https://github.com/settings/tokens" -ForegroundColor White
Write-Host "2. XAI_API_KEY - Your XAI API key (already set)" -ForegroundColor White
Write-Host "3. Azure variables - Get from Azure portal or service principal" -ForegroundColor White

Write-Host "`n💡 Quick Setup Commands:" -ForegroundColor Yellow
Write-Host "# Set GitHub Token (replace YOUR_TOKEN with actual token)" -ForegroundColor Gray
Write-Host '[Environment]::SetEnvironmentVariable("GITHUB_TOKEN", "YOUR_TOKEN", "User")' -ForegroundColor White

Write-Host "`n# Set Azure Variables (replace with your values)" -ForegroundColor Gray
Write-Host '[Environment]::SetEnvironmentVariable("AZURE_CLIENT_ID", "your-client-id", "User")' -ForegroundColor White
Write-Host '[Environment]::SetEnvironmentVariable("AZURE_CLIENT_SECRET", "your-client-secret", "User")' -ForegroundColor White
Write-Host '[Environment]::SetEnvironmentVariable("AZURE_TENANT_ID", "your-tenant-id", "User")' -ForegroundColor White
Write-Host '[Environment]::SetEnvironmentVariable("AZURE_SUBSCRIPTION_ID", "your-subscription-id", "User")' -ForegroundColor White

Write-Host "`n⚠️  After setting variables, restart VS Code for MCP servers to pick them up." -ForegroundColor Yellow

# Test PowerShell shell integration
Write-Host "`n🔌 Testing PowerShell Shell Integration:" -ForegroundColor Cyan
try {
    $psVersion = $PSVersionTable.PSVersion
    Write-Host "✅ PowerShell Version: $psVersion" -ForegroundColor Green

    # Test if we can run basic commands
    $test = & pwsh -Command "Write-Output 'Shell integration test'"
    if ($test -eq 'Shell integration test') {
        Write-Host "✅ Shell Integration: Working" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  Shell Integration: Limited" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "❌ Shell Integration: Failed - $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n🎯 Next Steps:" -ForegroundColor Cyan
Write-Host "1. Set the required environment variables using the commands above" -ForegroundColor White
Write-Host "2. Restart VS Code completely" -ForegroundColor White
Write-Host "3. Check MCP server status in VS Code settings" -ForegroundColor White
Write-Host "4. Verify servers are running in the MCP output panel" -ForegroundColor White
