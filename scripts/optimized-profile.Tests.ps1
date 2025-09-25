# Pester Tests for Optimized PowerShell Profile
# This demonstrates that Pester can test profile functions

Describe "Optimized PowerShell Profile Tests" {

    BeforeAll {
        # Import the profile functions for testing
        . "$PSScriptRoot\optimized-profile.ps1"
    }

    Context "Profile Phase Tracking" {

        It "Write-ProfilePhase should not throw" {
            { Write-ProfilePhase -PhaseName "TestPhase" -Details "Testing" } | Should -Not -Throw
        }

        It "Write-ProfilePhase should accept valid parameters" {
            { Write-ProfilePhase -PhaseName "Test" } | Should -Not -Throw
        }
    }

    Context "Cache Validity Testing" {

        It "Test-CacheValidity should return false for non-existent file" {
            $result = Test-CacheValidity -CacheFile "C:\NonExistent\file.json" -ExpiryHours 24
            $result | Should -Be $false
        }

        It "Test-CacheValidity should handle invalid JSON gracefully" {
            # Create a temporary invalid JSON file
            $tempFile = [System.IO.Path]::GetTempFileName() + ".json"
            try {
                '{"invalid": json}' | Set-Content $tempFile
                $result = Test-CacheValidity -CacheFile $tempFile -ExpiryHours 24
                $result | Should -Be $false
            }
            finally {
                if (Test-Path $tempFile) { Remove-Item $tempFile }
            }
        }
    }

    Context "Environment Variable Management" {

        It "Set-EnvironmentVariables should handle empty hashtable" {
            { Set-EnvironmentVariables -Variables @{} } | Should -Not -Throw
        }

        It "Set-EnvironmentVariables should handle null values" {
            { Set-EnvironmentVariables -Variables @{ "TEST_VAR" = $null } } | Should -Not -Throw
        }
    }

    Context "Syncfusion License Registration" {

        It "Register-SyncfusionLicense should return appropriate values" {
            $result = Register-SyncfusionLicense
            # Should return $true, $false, or $null (deferred)
            ($result -eq $true -or $result -eq $false -or $result -eq $null) | Should -Be $true
        }
    }

    Context "Profile Metrics" {

        It "Get-ProfileLoadTime should not throw" {
            { Get-ProfileLoadTime } | Should -Not -Throw
        }

        It "Get-ProfileMetrics should not throw" {
            { Get-ProfileMetrics } | Should -Not -Throw
        }
    }

    Context "MCP Cache Management" {

        It "Get-MCPCacheStatus should not throw" {
            { Get-MCPCacheStatus } | Should -Not -Throw
        }

        It "Clear-MCPCache should handle WhatIf" {
            { Clear-MCPCache -WhatIf } | Should -Not -Throw
        }
    }
}

# Example of how to run these tests:
# Invoke-Pester -Path $MyInvocation.MyCommand.Path -Verbose
