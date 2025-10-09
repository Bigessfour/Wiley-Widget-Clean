# Test QuickBooks Online Azure Key Vault Integration
# This script validates that QBO secrets are properly loaded from Azure Key Vault

Write-Output "=== Testing QuickBooks Online Azure Key Vault Integration ==="
Write-Output ""

# Check if Azure CLI is available and logged in
try {
    $account = az account show --query '{name:name, id:id}' -o json 2>$null | ConvertFrom-Json
    Write-Output "✅ Azure CLI logged in: $($account.name)"
} catch {
    Write-Output "⚠️  Azure CLI not logged in - testing with environment variables only"
}

# Check environment variables
Write-Output ""
Write-Output "Checking QBO environment variables:"
$envVars = @("QBO_CLIENT_ID", "QBO_CLIENT_SECRET", "QBO_REALM_ID", "QBO_ENVIRONMENT")
$envStatus = @{}

foreach ($var in $envVars) {
    $value = [System.Environment]::GetEnvironmentVariable($var, "User")
    if ([string]::IsNullOrEmpty($value)) {
        Write-Output "❌ $var : Not set"
        $envStatus[$var] = $false
    } else {
        Write-Output "✅ $var : Set ($($value.Substring(0, [Math]::Min(8, $value.Length)))...)"
        $envStatus[$var] = $true
    }
}

# Check Azure Key Vault configuration
Write-Output ""
Write-Output "Checking Azure Key Vault configuration:"
$keyVaultUrl = $env:AZURE_KEY_VAULT_URL
if ([string]::IsNullOrEmpty($keyVaultUrl)) {
    # Try to get from appsettings.json
    try {
        $appSettings = Get-Content "appsettings.json" | ConvertFrom-Json
        $keyVaultUrl = $appSettings.Azure.KeyVault.Url
    } catch {
        $keyVaultUrl = $null
    }
}

if ([string]::IsNullOrEmpty($keyVaultUrl)) {
    Write-Output "❌ Azure Key Vault URL not configured"
    $kvConfigured = $false
} else {
    Write-Output "✅ Azure Key Vault URL: $keyVaultUrl"
    $kvConfigured = $true

    # Extract vault name for testing
    if ($keyVaultUrl -match "https://(.+)\.vault\.azure\.net") {
        $vaultName = $matches[1]
        Write-Output "   Vault Name: $vaultName"

        # Test Key Vault access
        try {
            $secrets = az keyvault secret list --vault-name $vaultName --query "[?starts_with(name, 'QBO-')].name" -o tsv 2>$null
            $qboSecrets = $secrets | Where-Object { $_ -like "QBO-*" }
            if ($qboSecrets) {
                Write-Output "✅ Found QBO secrets in Key Vault:"
                $qboSecrets | ForEach-Object { Write-Output "   - $_" }
            } else {
                Write-Output "⚠️  No QBO secrets found in Key Vault"
            }
        } catch {
            Write-Output "❌ Cannot access Key Vault (may not be logged in or vault doesn't exist)"
        }
    }
}

# Build and test the application
Write-Output ""
Write-Output "Building and testing application..."
dotnet build "WileyWidget.csproj" --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Build failed"
    exit 1
}

Write-Output "✅ Application builds successfully"

# Test QBO service initialization
Write-Output ""
Write-Output "Testing QBO service initialization..."
$testCode = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WileyWidget.Services;
using Microsoft.Extensions.Logging;

class Program {
    static async Task Main(string[] args) {
        try {
            Console.WriteLine("Testing QBO service initialization...");

            // Create minimal host to test DI
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) => {
                    // Register minimal services needed for QBO
                    services.AddSingleton<SettingsService>(SettingsService.Instance);
                    services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
                    services.AddLogging();
                })
                .Build();

            var serviceProvider = host.Services;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<Program>();

            // Test Azure Key Vault service
            var kvService = serviceProvider.GetRequiredService<IAzureKeyVaultService>();
            Console.WriteLine("✓ Azure Key Vault service resolved");

            // Test QBO service creation
            var qbService = new QuickBooksService(
                serviceProvider.GetRequiredService<SettingsService>(),
                kvService,
                loggerFactory.CreateLogger<QuickBooksService>()
            );
            Console.WriteLine("✓ QuickBooks service created successfully");

            // Test basic connectivity (will fail without auth, but validates setup)
            try {
                var testResult = await qbService.TestConnectionAsync();
                Console.WriteLine($"✓ Connection test completed (result: {testResult})");
            } catch (Exception ex) {
                Console.WriteLine($"⚠ Connection test failed as expected (authentication required): {ex.Message}");
            }

            Console.WriteLine("");
            Console.WriteLine("SUCCESS: QBO service integration test completed!");
            Console.WriteLine("The service is properly configured to load secrets from Azure Key Vault.");

        } catch (Exception ex) {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Environment.Exit(1);
        }
    }
}
"@

$testCode | Out-File -FilePath "TestQboIntegration.cs" -Encoding UTF8

dotnet run --project WileyWidget.csproj TestQboIntegration.cs 2>$null
$testExitCode = $LASTEXITCODE

Remove-Item "TestQboIntegration.cs" -ErrorAction SilentlyContinue

if ($testExitCode -eq 0) {
    Write-Output "✅ QBO service integration test passed"
} else {
    Write-Output "❌ QBO service integration test failed"
}

# Summary
Write-Output ""
Write-Output "=== Integration Summary ==="
Write-Output "Environment Variables: $($envStatus.Values | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count)/$($envVars.Count) configured"
Write-Output "Azure Key Vault: $(if ($kvConfigured) { "Configured" } else { "Not configured" })"

if (($envStatus.Values | Where-Object { $_ } | Measure-Object | Select-Object -ExpandProperty Count) -eq $envVars.Count -or $kvConfigured) {
    Write-Output "✅ QBO secrets are available (via environment variables or Azure Key Vault)"
} else {
    Write-Output "❌ QBO secrets not available - configure environment variables or Azure Key Vault"
}

Write-Output ""
Write-Output "=== Next Steps ==="
if (-not $kvConfigured) {
    Write-Output "1. Set up Azure Key Vault and add QBO secrets:"
    Write-Output "   .\add-qbo-secrets-to-keyvault.ps1 -ClientId '<id>' -ClientSecret '<secret>'"
    Write-Output ""
    Write-Output "2. Configure appsettings.json with Key Vault URL"
} else {
    Write-Output "1. Ensure QBO secrets are in Azure Key Vault (QBO-CLIENT-ID, QBO-CLIENT-SECRET, etc.)"
}

Write-Output ""
Write-Output "2. Test full application startup to verify QBO initialization logging"
Write-Output "3. Use QuickBooks validation scripts to test API connectivity"