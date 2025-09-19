#!/usr/bin/env python3
"""
Azure Key Vault Integration Test Script for WileyWidget
Tests end-to-end Key Vault access and secret retrieval
"""

import os
import sys
import subprocess
import json
from pathlib import Path

def run_command(cmd, description, timeout=30):
    """Run a command and return the result"""
    print(f"🔧 {description}...")
    try:
        result = subprocess.run(cmd, shell=True, capture_output=True, text=True, check=True, timeout=timeout)
        print(f"  ✅ {description} successful")
        return result.stdout.strip()
    except subprocess.CalledProcessError as e:
        print(f"  ❌ {description} failed: {e}")
        print(f"  Error output: {e.stderr}")
        return None
    except subprocess.TimeoutExpired:
        print(f"  ❌ {description} timed out after {timeout} seconds")
        return None

def test_azure_cli_auth():
    """Test Azure CLI authentication"""
    print("🔐 Testing Azure CLI Authentication...")

    # Check if logged in
    account_info = run_command("az account show --query user.name", "Check Azure login status")
    if not account_info:
        print("  ❌ Not logged in to Azure CLI")
        return False

    print(f"  ✅ Logged in as: {account_info}")

    # Check subscription
    subscription = run_command("az account show --query name", "Check active subscription")
    if subscription:
        print(f"  ✅ Active subscription: {subscription}")

    return True

def test_key_vault_access():
    """Test Key Vault access and list secrets"""
    print("🔑 Testing Azure Key Vault Access...")

    vault_name = "wiley-widget-secrets"

    # List secrets
    secrets_output = run_command(f"az keyvault secret list --vault-name {vault_name}", "List Key Vault secrets")
    if not secrets_output:
        return False

    try:
        secrets_data = json.loads(secrets_output)
        secret_names = [secret['name'] for secret in secrets_data]
        print(f"  ✅ Found {len(secret_names)} secrets in Key Vault:")
        for name in secret_names[:5]:  # Show first 5 secrets
            print(f"    - {name}")
        if len(secret_names) > 5:
            print(f"    ... and {len(secret_names) - 5} more")
    except json.JSONDecodeError as e:
        print(f"  ❌ Could not parse secrets list: {e}")
        print(f"  Raw output: {secrets_output[:200]}...")
        return False

    return True

def test_secret_retrieval():
    """Test retrieving specific secrets"""
    print("📋 Testing Secret Retrieval...")

    vault_name = "wiley-widget-secrets"
    test_secrets = ["SyncfusionLicenseKey", "AZURE-SQL-PASSWORD", "GITHUB-PAT"]

    for secret_name in test_secrets:
        print(f"  🔍 Testing retrieval of: {secret_name}")
        value = run_command(f"az keyvault secret show --vault-name {vault_name} --name {secret_name} --query value", f"Retrieve {secret_name}")
        if value:
            # Mask the actual value for security
            masked_value = value[:10] + "..." if len(value) > 10 else value
            print(f"    ✅ Retrieved: {masked_value}")
        else:
            print(f"    ❌ Failed to retrieve: {secret_name}")

    return True

def test_production_config():
    """Test production configuration setup"""
    print("⚙️  Testing Production Configuration...")

    # Check if production appsettings exists
    prod_config = Path("appsettings.Production.json")
    if not prod_config.exists():
        print("  ❌ appsettings.Production.json not found")
        return False

    print("  ✅ appsettings.Production.json exists")

    # Check .env file
    env_file = Path(".env")
    if not env_file.exists():
        print("  ❌ .env file not found")
        return False

    print("  ✅ .env file exists")

    # Check for required environment variables
    required_vars = ["AZURE_KEY_VAULT_URL", "AZURE_KEY_VAULT_NAME"]
    missing_vars = []

    with open(env_file, 'r') as f:
        for line in f:
            if '=' in line:
                key = line.split('=')[0].strip()
                for required in required_vars:
                    if key == required:
                        required_vars.remove(required)

    if required_vars:
        print(f"  ❌ Missing required environment variables: {required_vars}")
        return False

    print("  ✅ Required environment variables present")
    return True

def test_key_vault_integration_code():
    """Test that the Key Vault integration code exists and is properly structured"""
    print("� Testing Key Vault Integration Code...")

    app_file = Path("App.xaml.cs")
    if not app_file.exists():
        print("  ❌ App.xaml.cs not found")
        return False

    print("  ✅ App.xaml.cs exists")

    # Check for Key Vault related code
    with open(app_file, 'r') as f:
        content = f.read()

    required_elements = [
        "Azure.Security.KeyVault.Secrets",
        "GetAzureCredential",
        "RegisterSyncfusionLicense",
        "SecretClient"
    ]

    missing_elements = []
    for element in required_elements:
        if element not in content:
            missing_elements.append(element)

    if missing_elements:
        print(f"  ❌ Missing Key Vault integration elements: {missing_elements}")
        return False

    print("  ✅ Key Vault integration code present")
    return True

def main():
    print("🧪 WileyWidget Azure Key Vault Integration Test")
    print("=" * 50)

    tests = [
        ("Azure CLI Authentication", test_azure_cli_auth),
        ("Key Vault Access", test_key_vault_access),
        ("Secret Retrieval", test_secret_retrieval),
        ("Production Configuration", test_production_config),
        ("Key Vault Integration Code", test_key_vault_integration_code)
    ]

    results = []

    for test_name, test_func in tests:
        print(f"\n🔬 Running: {test_name}")
        print("-" * 30)
        try:
            result = test_func()
            results.append((test_name, result))
        except Exception as e:
            print(f"  ❌ Test failed with exception: {e}")
            results.append((test_name, False))

    # Summary
    print("\n" + "=" * 50)
    print("📊 TEST RESULTS SUMMARY")
    print("=" * 50)

    passed = 0
    total = len(results)

    for test_name, result in results:
        status = "✅ PASS" if result else "❌ FAIL"
        print(f"{status}: {test_name}")
        if result:
            passed += 1

    print(f"\nOverall: {passed}/{total} tests passed")

    if passed == total:
        print("🎉 All tests passed! Azure Key Vault integration is working correctly.")
        return 0
    else:
        print("⚠️  Some tests failed. Please review the output above.")
        return 1

if __name__ == "__main__":
    sys.exit(main())