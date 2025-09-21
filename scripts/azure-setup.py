#!/usr/bin/env python3
"""
Azure Setup and Configuration Script for WileyWidget (Python)
Run this script to set up your Azure development environment
"""

import os
import sys
import subprocess
import argparse
from pathlib import Path

# Azure CLI path
AZ_CLI_PATH = r"C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd"

def load_env_file():
    """Load environment variables from .env file"""
    env_file = Path('.env')

    if not env_file.exists():
        print("❌ .env file not found!")
        print("Please copy .env.example to .env and fill in your Azure values")
        return False

    print("📖 Loading environment configuration...")

    try:
        with open(env_file, 'r', encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#') and '=' in line:
                    key, value = line.split('=', 1)
                    key = key.strip()
                    value = value.strip()

                    # Remove quotes
                    if (value.startswith('"') and value.endswith('"')) or \
                       (value.startswith("'") and value.endswith("'")):
                        value = value[1:-1]

                    os.environ[key] = value
                    print(f"  ✓ {key}")

        return True
    except Exception as e:
        print(f"❌ Error loading .env file: {e}")
        return False

def check_azure_cli():
    """Check Azure CLI authentication"""
    print("🔐 Checking Azure CLI authentication...")

    try:
        result = subprocess.run([AZ_CLI_PATH, 'account', 'show'],
                              capture_output=True, text=True, check=True)
        import json
        account = json.loads(result.stdout)

        print(f"  ✓ Signed in as: {account.get('user', {}).get('name', 'Unknown')}")
        print(f"  ✓ Subscription: {account.get('name', 'Unknown')}")
        return True
    except subprocess.CalledProcessError:
        print("  ❌ Not signed in to Azure CLI")
        print("  Run: az login")
        return False
    except Exception as e:
        print(f"  ❌ Error checking Azure CLI: {e}")
        return False

def test_sql_connection():
    """Test Azure SQL Database connection"""
    print("🧪 Testing Azure SQL Connection...")

    required_vars = ['AZURE_SQL_SERVER', 'AZURE_SQL_DATABASE', 'AZURE_SQL_USER', 'AZURE_SQL_PASSWORD']
    missing_vars = [var for var in required_vars if not os.environ.get(var)]

    if missing_vars:
        print(f"❌ Missing environment variables: {', '.join(missing_vars)}")
        return False

    server = os.environ['AZURE_SQL_SERVER']
    database = os.environ['AZURE_SQL_DATABASE']
    user = os.environ['AZURE_SQL_USER']
    password = os.environ['AZURE_SQL_PASSWORD']

    connection_string = f"Server=tcp:{server},1433;Database={database};User ID={user};Password={password};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

    # Test with sqlcmd if available
    try:
        result = subprocess.run([
            'sqlcmd', '-S', f'{server},1433',
            '-d', database, '-U', user, '-P', password,
            '-Q', 'SELECT 1 as TestConnection'
        ], capture_output=True, text=True, timeout=30)

        if result.returncode == 0:
            print("  ✓ Azure SQL connection successful")
            return True
        else:
            print(f"  ❌ SQL connection failed: {result.stderr}")
            return False

    except FileNotFoundError:
        print("  ⚠️  sqlcmd not found, skipping SQL connection test")
        print("  Install SQL Server tools or test manually")
        return True
    except subprocess.TimeoutExpired:
        print("  ❌ SQL connection test timed out")
        return False
    except Exception as e:
        print(f"  ❌ Error testing SQL connection: {e}")
        return False

def create_resources():
    """Create Azure resources using Bicep/ARM templates"""
    print("🏗️  Creating Azure resources...")

    # This would typically use Azure CLI or Azure PowerShell
    print("  ⚠️  Resource creation not implemented in this script")
    print("  Use Azure CLI or Azure Portal to create resources")
    return True

def deploy_database():
    """Deploy database schema"""
    print("🗄️  Deploying database...")

    # This would run EF migrations or SQL scripts
    try:
        result = subprocess.run(['dotnet', 'ef', 'database', 'update'],
                              capture_output=True, text=True, cwd=os.getcwd())

        if result.returncode == 0:
            print("  ✓ Database deployed successfully")
            return True
        else:
            print(f"  ❌ Database deployment failed: {result.stderr}")
            return False

    except Exception as e:
        print(f"  ❌ Error deploying database: {e}")
        return False

def main():
    parser = argparse.ArgumentParser(description='Azure Setup for WileyWidget')
    parser.add_argument('--test-connection', action='store_true',
                       help='Test Azure connections')
    parser.add_argument('--create-resources', action='store_true',
                       help='Create Azure resources')
    parser.add_argument('--deploy-database', action='store_true',
                       help='Deploy database')

    args = parser.parse_args()

    print("🔧 WileyWidget Azure Setup Script (Python)")
    print("==========================================")

    # Load environment variables
    if not load_env_file():
        return 1

    # Check Azure CLI
    if not check_azure_cli():
        return 1

    success = True

    if args.test_connection:
        success &= test_sql_connection()

    if args.create_resources:
        success &= create_resources()

    if args.deploy_database:
        success &= deploy_database()

    if success:
        print("✅ Azure setup completed successfully!")
        return 0
    else:
        print("❌ Azure setup failed!")
        return 1

if __name__ == '__main__':
    sys.exit(main())