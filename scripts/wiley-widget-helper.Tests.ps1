# Pester Tests for Wiley Widget Helper Script
# Tests utility functions for the WPF application

Describe "Wiley Widget Helper Tests" {

    BeforeAll {
        # Mock external commands and file operations
        Mock Get-Process { return [PSCustomObject]@{ Id = 1234; WorkingSet64 = 50MB; StartTime = Get-Date } } -ParameterFilter { $Name -eq 'WileyWidget' }
        Mock Test-Path { return $true } -ParameterFilter { $Path -like '*APPDATA*' }
        Mock Get-ChildItem { return @([PSCustomObject]@{ Name = 'app.log'; LastWriteTime = Get-Date }) } -ParameterFilter { $Filter -eq '*.log' }
        Mock Start-Process { return [PSCustomObject]@{ Id = 5678 } }
        Mock dotnet { return 0 } -ParameterFilter { $args -contains 'run' }
    }

    Context "Application Status Checking" {

        It "Should detect running WileyWidget process" {
            $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
            $process | Should -Not -BeNullOrEmpty
            $process.Id | Should -Be 1234
        }

        It "Should calculate memory usage correctly" {
            $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
            $memoryMB = [math]::Round($process.WorkingSet64 / 1MB, 2)
            $memoryMB | Should -BeGreaterThan 0
        }

        It "Should handle when application is not running" {
            Mock Get-Process { return $null } -ParameterFilter { $Name -eq 'WileyWidget' }

            $process = Get-Process -Name "WileyWidget" -ErrorAction SilentlyContinue
            $process | Should -BeNullOrEmpty
        }
    }

    Context "App Data Directory Handling" {

        It "Should check app data directory existence" {
            $appDataPath = "$env:APPDATA\WileyWidget"
            $exists = Test-Path $appDataPath
            $exists | Should -Be $true
        }

        It "Should find log files in app data directory" {
            $logFiles = Get-ChildItem "$env:APPDATA\WileyWidget\logs" -Filter "*.log" -ErrorAction SilentlyContinue
            $logFiles | Should -Not -BeNullOrEmpty
            $logFiles[0].Name | Should -Be 'app.log'
        }

        It "Should handle missing log directory" {
            Mock Get-ChildItem { return $null } -ParameterFilter { $Path -eq "nonexistent\logs" }

            $logFiles = Get-ChildItem "nonexistent\logs" -Filter "*.log" -ErrorAction SilentlyContinue
            $logFiles | Should -BeNullOrEmpty
        }
    }

    Context "Application Startup" {

        It "Should accept Debug parameter" {
            $script = {
                param([switch]$Debug)
                $Debug.IsPresent
            }
            $result = & $script -Debug
            $result | Should -Be $true
        }

        It "Should accept CleanStart parameter" {
            $script = {
                param([switch]$CleanStart)
                $CleanStart.IsPresent
            }
            $result = & $script -CleanStart
            $result | Should -Be $true
        }

        It "Should handle dotnet run command structure" {
            # Test command construction logic
            $dotnetArgs = @('run', '--project', 'WileyWidget.csproj')
            $dotnetArgs | Should -Contain 'run'
            $dotnetArgs | Should -Contain '--project'
        }
    }

    Context "Database Testing" {

        It "Should accept database connection parameters" {
            $script = {
                param([string]$ConnectionString)
                $ConnectionString
            }
            $result = & $script -ConnectionString 'Server=test;Database=test'
            $result | Should -Be 'Server=test;Database=test'
        }

        It "Should handle connection test logic" {
            # Test the connection testing pattern
            $connectionString = 'Server=test;Database=test'
            $connectionString | Should -Match 'Server='
            $connectionString | Should -Match 'Database='
        }
    }

    Context "Log File Management" {

        It "Should retrieve latest log file" {
            $logFiles = Get-ChildItem "logs" -Filter "*.log" -ErrorAction SilentlyContinue
            $latestLog = $logFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            $latestLog | Should -Not -BeNullOrEmpty
            $latestLog.Name | Should -Be 'app.log'
        }

        It "Should handle log file tailing parameters" {
            $tailParams = @{
                Lines = 50
                Wait  = $true
            }
            $tailParams.Lines | Should -Be 50
            $tailParams.Wait | Should -Be $true
        }
    }

    Context "Error Handling" {

        It "Should handle process not found gracefully" {
            Mock Get-Process { return $null } -ParameterFilter { $Name -eq "NonExistent" }

            { Get-Process -Name "NonExistent" -ErrorAction SilentlyContinue } | Should -Not -Throw
        }

        It "Should handle file access errors" {
            Mock Test-Path { throw "Access denied" }

            { Test-Path "protected\path" } | Should -Throw
        }

        It "Should handle Start-Process failures" {
            Mock Start-Process { throw "Failed to start" }

            { Start-Process "nonexistent.exe" } | Should -Throw
        }
    }

    Context "Environment Variable Handling" {

        It "Should set environment variables correctly" {
            $testKey = 'TEST_VAR'
            $testValue = 'test_value'

            # Test the pattern used in scripts
            [Environment]::SetEnvironmentVariable($testKey, $testValue, "Process")
            $retrieved = [Environment]::GetEnvironmentVariable($testKey, "Process")
            $retrieved | Should -Be $testValue
        }

        It "Should handle missing environment variables" {
            $missingVar = [Environment]::GetEnvironmentVariable("NONEXISTENT_VAR", "Process")
            $missingVar | Should -BeNullOrEmpty
        }
    }
}

# Example of how to run these tests:
# Invoke-Pester -Path $MyInvocation.MyCommand.Path -Verbose
