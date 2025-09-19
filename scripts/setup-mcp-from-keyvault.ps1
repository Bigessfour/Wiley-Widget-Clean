# MCP Environment Setup from Azure Key Vault
# This script retrieves MCP environment variables from Azure Key Vault and sets them locally

Write-Host "🔑 MCP Environment Setup from Azure Key Vault" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$vaultName = "wiley-widget-secrets"

# Mapping of Key Vault secret names to environment variable names
$secretMapping = @{
    "GITHUB-PAT" = "GITHUB_TOKEN"
    "XAI-API-KEY" = "XAI_API_KEY"
    # Note: Azure service principal credentials may need to be created separately
    # "AZURE-CLIENT-ID" = "AZURE_CLIENT_ID"
    # "AZURE-CLIENT-SECRET" = "AZURE_CLIENT_SECRET"
    # "AZURE-TENANT-ID" = "AZURE_TENANT_ID"
    # "AZURE-SUBSCRIPTION-ID" = "AZURE_SUBSCRIPTION_ID"
}

Write-Host "`n🔍 Retrieving secrets from Key Vault: $vaultName" -ForegroundColor Yellow

foreach ($kvSecret in $secretMapping.Keys) {
    $envVar = $secretMapping[$kvSecret]

    Write-Host "`n📋 Retrieving: $kvSecret → $envVar" -ForegroundColor White

    try {
        # Retrieve secret from Key Vault
        $secretValue = az keyvault secret show --vault-name $vaultName --name $kvSecret --query value -o tsv

        if ($secretValue -and $secretValue -ne "") {
            # Set as environment variable
            [Environment]::SetEnvironmentVariable($envVar, $secretValue, "User")
            Write-Host "  ✅ Successfully set $envVar" -ForegroundColor Green
        } else {
            Write-Host "  ❌ Failed to retrieve $kvSecret from Key Vault" -ForegroundColor Red
        }
    } catch {
        Write-Host "  ❌ Error retrieving $kvSecret`: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n🔍 Verifying environment variables:" -ForegroundColor Yellow

foreach ($envVar in $secretMapping.Values) {
    $value = [Environment]::GetEnvironmentVariable($envVar, "User")
    if ($value) {
        $maskedValue = $value.Substring(0, [Math]::Min(10, $value.Length)) + "..."
        Write-Host "  ✅ $envVar`: $maskedValue" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $envVar`: NOT SET" -ForegroundColor Red
    }
}

Write-Host "`n⚠️  IMPORTANT: Restart VS Code completely for MCP servers to pick up the new environment variables." -ForegroundColor Yellow
Write-Host "💡 You can verify the setup by running: .\scripts\check-mcp-status.ps1" -ForegroundColor Cyan