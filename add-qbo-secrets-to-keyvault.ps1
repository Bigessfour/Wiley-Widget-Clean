# Add QuickBooks Online Secrets to Azure Key Vault
# This script securely stores QBO credentials in Azure Key Vault

param(
    [Parameter(Mandatory=$true)]
    [string]$ClientId,

    [Parameter(Mandatory=$true)]
    [string]$ClientSecret,

    [Parameter(Mandatory=$false)]
    [string]$RealmId = "9341455168020461",

    [Parameter(Mandatory=$false)]
    [string]$Environment = "sandbox",

    [Parameter(Mandatory=$false)]
    [switch]$Force
)

Write-Output "=== Adding QuickBooks Online Secrets to Azure Key Vault ==="
Write-Output ""

# Check if Azure CLI is available
try {
    $azVersion = az version --query '"azure-cli"' -o tsv 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Output "✅ Azure CLI available (version: $azVersion)"
    } else {
        throw "Azure CLI not found"
    }
} catch {
    Write-Error "Azure CLI is required but not found. Please install Azure CLI first."
    Write-Output "Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Check if logged in to Azure
try {
    $account = az account show --query '{name:name, id:id, tenantId:tenantId}' -o json | ConvertFrom-Json
    Write-Output "✅ Logged in to Azure: $($account.name) ($($account.id))"
} catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get Key Vault name from environment or ask user
$keyVaultName = $env:AZURE_KEY_VAULT_NAME
if ([string]::IsNullOrEmpty($keyVaultName)) {
    $keyVaultName = Read-Host "Enter Azure Key Vault name"
}

# Verify Key Vault exists
try {
    $kvInfo = az keyvault show --name $keyVaultName --query '{name:name, location:location, resourceGroup:resourceGroup}' -o json | ConvertFrom-Json
    Write-Output "✅ Found Key Vault: $($kvInfo.name) in $($kvInfo.resourceGroup) ($($kvInfo.location))"
} catch {
    Write-Error "Key Vault '$keyVaultName' not found or not accessible."
    Write-Output "Please ensure the Key Vault exists and you have access to it."
    exit 1
}

Write-Output ""
Write-Output "QBO Configuration:"
Write-Output "  Client ID: $ClientId"
Write-Output "  Client Secret: $($ClientSecret.Substring(0, [Math]::Min(8, $ClientSecret.Length)))..."
Write-Output "  Realm ID: $RealmId"
Write-Output "  Environment: $Environment"
Write-Output ""

if (-not $Force) {
    $confirm = Read-Host "Continue adding these secrets to Key Vault '$keyVaultName'? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Output "Operation cancelled."
        exit 0
    }
}

# Add secrets to Key Vault
$secrets = @(
    @{Name = "QBO-CLIENT-ID"; Value = $ClientId; Description = "QuickBooks Online OAuth2 Client ID"},
    @{Name = "QBO-CLIENT-SECRET"; Value = $ClientSecret; Description = "QuickBooks Online OAuth2 Client Secret"},
    @{Name = "QBO-REALM-ID"; Value = $RealmId; Description = "QuickBooks Online Company Realm ID"},
    @{Name = "QBO-ENVIRONMENT"; Value = $Environment; Description = "QuickBooks Online Environment (sandbox/production)"}
)

foreach ($secret in $secrets) {
    Write-Output "Adding secret: $($secret.Name)..."

    try {
        az keyvault secret set `
            --vault-name $keyVaultName `
            --name $secret.Name `
            --value $secret.Value `
            --description $secret.Description `
            --output none

        if ($LASTEXITCODE -eq 0) {
            Write-Output "✅ Successfully added $($secret.Name)"
        } else {
            Write-Error "❌ Failed to add $($secret.Name)"
        }
    } catch {
        Write-Error "❌ Error adding $($secret.Name): $_"
    }
}

Write-Output ""
Write-Output "=== Verification ==="

# List the secrets we just added
Write-Output "Verifying secrets in Key Vault:"
try {
    $existingSecrets = az keyvault secret list --vault-name $keyVaultName --query "[].{name:name, enabled:attributes.enabled}" -o json | ConvertFrom-Json

    foreach ($secret in $secrets) {
        $found = $existingSecrets | Where-Object { $_.name -eq $secret.Name -and $_.enabled }
        if ($found) {
            Write-Output "✅ $($secret.Name): Found and enabled"
        } else {
            Write-Output "❌ $($secret.Name): Not found or disabled"
        }
    }
} catch {
    Write-Error "Failed to verify secrets: $_"
}

Write-Output ""
Write-Output "=== Next Steps ==="
Write-Output "1. Update your appsettings.json or environment variables to reference these secrets"
Write-Output "2. The application will automatically load QBO secrets from Azure Key Vault"
Write-Output "3. Test the QuickBooks connection using the validation scripts"
Write-Output ""
Write-Output "Example appsettings.json addition:"
Write-Output '{
  "Azure": {
    "KeyVault": {
      "Url": "https://' + $keyVaultName + '.vault.azure.net/"
    }
  }
}'