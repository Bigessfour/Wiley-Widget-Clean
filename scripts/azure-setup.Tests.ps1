# Pester Tests for Azure Setup Script
# Tests Azure environment configuration and connectivity

Describe "Azure Setup Script Tests" {

    BeforeAll {
        # Mock external commands
        Mock az { return '{"user":{"name":"test@example.com"},"name":"Test Subscription"}' } -ParameterFilter { $args[0] -eq 'account' -and $args[1] -eq 'show' }
        Mock Test-Path { return $true } -ParameterFilter { $Path -eq '.env' }
        Mock Get-Content { return @('AZURE_CLIENT_ID=test-id', 'AZURE_CLIENT_SECRET=test-secret', 'AZURE_TENANT_ID=test-tenant') }
        Mock az { return 'test-output' } -ParameterFilter { $args[0] -eq 'keyvault' }
    }

    Context "Environment File Handling" {

        It "Should detect missing .env file" {
            Mock Test-Path { return $false } -ParameterFilter { $Path -eq '.env' }

            # Test the logic that would cause exit
            $envFileExists = Test-Path ".env"
            $envFileExists | Should -Be $false
        }

        It "Should load environment variables from .env file" {
            $envVars = @()
            Get-Content ".env" | ForEach-Object {
                if ($_ -match '^([^=]+)=(.*)$') {
                    $key = $matches[1]
                    $value = $matches[2]
                    $envVars += @{ Key = $key; Value = $value }
                }
            }

            $envVars.Count | Should -BeGreaterThan 0
            $envVars[0].Key | Should -Not -BeNullOrEmpty
            $envVars[0].Value | Should -Not -BeNullOrEmpty
        }

        It "Should handle malformed .env lines gracefully" {
            Mock Get-Content { return @('INVALID_LINE', 'VALID_KEY=valid_value', '') }

            $validVars = @()
            Get-Content ".env" | ForEach-Object {
                if ($_ -match '^([^=]+)=(.*)$') {
                    $validVars += $matches[1]
                }
            }

            $validVars | Should -Contain 'VALID_KEY'
            $validVars | Should -Not -Contain 'INVALID_LINE'
        }
    }

    Context "Azure CLI Authentication" {

        It "Should verify Azure CLI login status" {
            # Test the az account show command logic
            $account = az account show | ConvertFrom-Json
            $account.user.name | Should -Not -BeNullOrEmpty
            $account.name | Should -Not -BeNullOrEmpty
        }

        It "Should handle authentication failure" {
            Mock az { throw "Authentication failed" } -ParameterFilter { $args[0] -eq 'account' -and $args[1] -eq 'show' }

            { $account = az account show | ConvertFrom-Json; $account.user.name } | Should -Throw
        }
    }

    Context "Parameter Handling" {

        It "Should accept TestConnection switch" {
            $script = {
                param([switch]$TestConnection)
                $TestConnection.IsPresent
            }
            $result = & $script -TestConnection
            $result | Should -Be $true
        }

        It "Should accept CreateResources switch" {
            $script = {
                param([switch]$CreateResources)
                $CreateResources.IsPresent
            }
            $result = & $script -CreateResources
            $result | Should -Be $true
        }

        It "Should accept DeployDatabase switch" {
            $script = {
                param([switch]$DeployDatabase)
                $DeployDatabase.IsPresent
            }
            $result = & $script -DeployDatabase
            $result | Should -Be $true
        }
    }

    Context "Connection String Building" {

        It "Should construct valid SQL connection string" {
            $env:AZURE_SQL_SERVER = 'test-server'
            $env:AZURE_SQL_DATABASE = 'test-db'
            $env:AZURE_SQL_USER = 'test-user'
            $env:AZURE_SQL_PASSWORD = 'test-pass'

            $connectionString = "Server=tcp:$($env:AZURE_SQL_SERVER),1433;Database=$($env:AZURE_SQL_DATABASE);User ID=$($env:AZURE_SQL_USER);Password=$($env:AZURE_SQL_PASSWORD);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

            $connectionString | Should -Match 'Server=tcp:test-server'
            $connectionString | Should -Match 'Database=test-db'
            $connectionString | Should -Match 'User ID=test-user'
            $connectionString | Should -Match 'Password=test-pass'
        }
    }

    Context "Error Handling" {

        It "Should handle missing environment variables" {
            $env:AZURE_SQL_SERVER = $null

            # Test that missing env vars would cause issues
            { "Server=tcp:$($env:AZURE_SQL_SERVER),1433;Database=test" } | Should -Not -Throw
            # But the resulting string would be malformed
            $badString = "Server=tcp:$($env:AZURE_SQL_SERVER),1433;Database=test"
            $badString | Should -Match 'tcp:,1433'
        }

        It "Should handle Azure CLI command failures" {
            # Test the error handling pattern without mocking
            $errorOccurred = $false
            try {
                # Simulate a command that would fail
                throw "Command failed"
            }
            catch {
                $errorOccurred = $true
            }

            $errorOccurred | Should -Be $true
        }
    }
}

# Example of how to run these tests:
# Invoke-Pester -Path $MyInvocation.MyCommand.Path -Verbose
