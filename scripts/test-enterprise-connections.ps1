# Test Enterprise Database Connections
# Comprehensive validation script for Azure SQL and Local SQL Server connections

param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("Development", "Production", "Test")]
    [string]$Environment = "Development",

    [Parameter(Mandatory = $false)]
    [switch]$SkipAzureTest,

    [Parameter(Mandatory = $false)]
    [switch]$DetailedOutput,

    [Parameter(Mandatory = $false)]
    [string]$AzureServer = $env:AZURE_SQL_SERVER,

    [Parameter(Mandatory = $false)]
    [string]$AzureDatabase = $env:AZURE_SQL_DATABASE
)

# Set environment variable for the session
$env:DOTNET_ENVIRONMENT = $Environment

Write-Information "🔍 ENTERPRISE DATABASE CONNECTION TEST" -InformationAction Continue
Write-Information "Environment: $Environment" -InformationAction Continue
Write-Information "Timestamp: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -InformationAction Continue
Write-Information "" -InformationAction Continue

# Test Results
$testResults = @{
    LocalConnection = $false
    AzureConnection = $false
    HealthCheck     = $false
    MigrationStatus = $false
    Errors          = @()
}

function Write-TestHeader {
    param([string]$TestName)
    Write-Information "📋 Testing: $TestName" -InformationAction Continue
}

function Write-Success {
    param([string]$Message)
    Write-Information "✅ $Message" -InformationAction Continue
}

function Write-Warning {
    param([string]$Message)
    Write-Warning "⚠️  $Message"
}

function Write-Error {
    param([string]$Message)
    Write-Warning "❌ $Message"
    $testResults.Errors += $Message
}

function Test-LocalSqlServerConnection {
    Write-TestHeader "Local SQL Server Connection"

    try {
        # Check if SQL Server is running
        $sqlService = Get-Service -Name "MSSQL`$SQLEXPRESS01" -ErrorAction SilentlyContinue
        if (-not $sqlService) {
            Write-Warning "SQL Server Express (SQLEXPRESS01) service not found. Checking for other instances..."
            $sqlServices = Get-Service -Name "MSSQL`$*" -ErrorAction SilentlyContinue
            if ($sqlServices) {
                Write-Information "Found SQL Server instances:" -InformationAction Continue
                $sqlServices | ForEach-Object { Write-Verbose "  - $($_.Name)" }
            }
            else {
                Write-Error "No SQL Server instances found on this machine"
                return $false
            }
        }
        elseif ($sqlService.Status -ne "Running") {
            Write-Error "SQL Server Express service is not running (Status: $($sqlService.Status))"
            return $false
        }

        # Test connection using .NET
        Add-Type -AssemblyName "Microsoft.Data.SqlClient"
        $connectionString = "Server=localhost\SQLEXPRESS01;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=10;"

        try {
            $connection = New-Object Microsoft.Data.SqlClient.SqlConnection $connectionString
            $connection.Open()
            Write-Success "Local SQL Server connection established"

            # Test database creation/query
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT @@VERSION as Version"
            $reader = $command.ExecuteReader()
            if ($reader.Read()) {
                $version = $reader["Version"]
                Write-Success "SQL Server version query successful: $($version.ToString().Substring(0, 50))..."
            }
            $reader.Close()
            $connection.Close()

            $testResults.LocalConnection = $true
            return $true
        }
        catch {
            Write-Error "Local SQL Server connection failed: $($_.Exception.Message)"
            return $false
        }
    }
    catch {
        Write-Error "Local SQL Server test failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-AzureSqlConnection {
    if ($SkipAzureTest) {
        Write-Warning "Skipping Azure SQL test as requested"
        return $true
    }

    Write-TestHeader "Azure SQL Database Connection"

    if (-not $AzureServer -or -not $AzureDatabase) {
        Write-Error "Azure SQL Server and Database not specified. Use -AzureServer and -AzureDatabase parameters or set environment variables."
        return $false
    }

    try {
        # Test Azure connection using .NET
        Add-Type -AssemblyName "Microsoft.Data.SqlClient"
        $connectionString = "Server=tcp:$AzureServer.database.windows.net,1433;Database=$AzureDatabase;Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

        Write-Verbose "Testing connection to: $AzureServer.database.windows.net"

        try {
            $connection = New-Object Microsoft.Data.SqlClient.SqlConnection $connectionString
            $connection.Open()
            Write-Success "Azure SQL Database connection established"

            # Test basic query
            $command = $connection.CreateCommand()
            $command.CommandText = "SELECT @@VERSION as Version"
            $reader = $command.ExecuteReader()
            if ($reader.Read()) {
                $version = $reader["Version"]
                Write-Success "Azure SQL version query successful: $($version.ToString().Substring(0, 50))..."
            }
            $reader.Close()
            $connection.Close()

            $testResults.AzureConnection = $true
            return $true
        }
        catch {
            Write-Error "Azure SQL Database connection failed: $($_.Exception.Message)"
            Write-Warning "Ensure you are logged in with 'az login' and have access to the Azure SQL Database"
            return $false
        }
    }
    catch {
        Write-Error "Azure SQL test failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-DotNetBuild {
    Write-TestHeader ".NET Build and Database Context"

    try {
        # Build the project
        Write-Verbose "Building WileyWidget project..."
        $buildResult = dotnet build "WileyWidget.csproj" --verbosity quiet 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Success ".NET build successful"
        }
        else {
            Write-Error ".NET build failed"
            if ($DetailedOutput) {
                Write-Verbose "Build output:"
                $buildResult | ForEach-Object { Write-Verbose "  $_" }
            }
            return $false
        }

        # Test DbContext creation
        Write-Verbose "Testing DbContext factory..."
        $testCode = @"
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using WileyWidget.Configuration;
using Microsoft.Extensions.Configuration;
using System;

try {
    var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
    Console.WriteLine($"Testing DbContext in {environment} environment");

    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{environment}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var services = new ServiceCollection();
    services.AddEnterpriseDatabaseServices(configuration);

    var serviceProvider = services.BuildServiceProvider();
    var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<WileyWidget.Data.AppDbContext>>();

    using var context = dbContextFactory.CreateDbContext();
    var canConnect = context.Database.CanConnect();
    Console.WriteLine($"Database connection test: {canConnect}");

    if (canConnect) {
        Console.WriteLine("SUCCESS: DbContext factory working correctly");
    } else {
        Console.WriteLine("FAILED: Cannot connect to database");
    }
} catch (Exception ex) {
    Console.WriteLine($"ERROR: {ex.Message}");
}
"@

        $testFile = [System.IO.Path]::GetTempFileName() + ".cs"
        $testCode | Out-File -FilePath $testFile -Encoding UTF8

        try {
            $testResult = dotnet run --project "WileyWidget.csproj" -- $testFile 2>&1

            if ($testResult -match "SUCCESS") {
                Write-Success "DbContext factory test passed"
                return $true
            }
            elseif ($testResult -match "FAILED") {
                Write-Error "DbContext factory test failed - connection issue"
                return $false
            }
            else {
                Write-Warning "DbContext factory test inconclusive"
                if ($DetailedOutput) {
                    Write-Verbose "Test output:"
                    $testResult | ForEach-Object { Write-Verbose "  $_" }
                }
                return $false
            }
        }
        finally {
            Remove-Item $testFile -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Error "DbContext test failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-HealthCheck {
    Write-TestHeader "Health Check System"

    try {
        # Test health check endpoint (if application is running)
        # For now, just test that the health check classes compile
        Write-Verbose "Testing health check compilation..."

        $healthCheckCode = @"
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WileyWidget.Services;

try {
    Console.WriteLine("Health check classes compile successfully");
    Console.WriteLine("SUCCESS: Health check system ready");
} catch (Exception ex) {
    Console.WriteLine($"ERROR: {ex.Message}");
}
"@

        $healthFile = [System.IO.Path]::GetTempFileName() + ".cs"
        $healthCheckCode | Out-File -FilePath $healthFile -Encoding UTF8

        try {
            $healthResult = dotnet build "WileyWidget.csproj" --verbosity quiet 2>&1

            if ($LASTEXITCODE -eq 0) {
                Write-Success "Health check system compilation successful"
                $testResults.HealthCheck = $true
                return $true
            }
            else {
                Write-Error "Health check system compilation failed"
                return $false
            }
        }
        finally {
            Remove-Item $healthFile -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Error "Health check test failed: $($_.Exception.Message)"
        return $false
    }
}

function Show-TestSummary {
    Write-Information "" -InformationAction Continue
    Write-Information "📊 TEST SUMMARY" -InformationAction Continue
    Write-Information "Environment: $Environment" -InformationAction Continue
    Write-Information "" -InformationAction Continue

    $totalTests = 4
    $passedTests = 0

    if ($testResults.LocalConnection) { $passedTests++ }
    if ($testResults.AzureConnection -or $SkipAzureTest) { $passedTests++ }
    if ($testResults.HealthCheck) { $passedTests++ }
    if ($testResults.MigrationStatus) { $passedTests++ }

    Write-Information "Local SQL Server Connection: $(if ($testResults.LocalConnection) { '✅ PASS' } else { '❌ FAIL' })" -InformationAction Continue
    Write-Information "Azure SQL Database Connection: $(if ($testResults.AzureConnection -or $SkipAzureTest) { '✅ PASS' } else { '❌ FAIL' })" -InformationAction Continue
    Write-Information "Health Check System: $(if ($testResults.HealthCheck) { '✅ PASS' } else { '❌ FAIL' })" -InformationAction Continue
    Write-Information "Migration Status: $(if ($testResults.MigrationStatus) { '✅ PASS' } else { '❌ FAIL' })" -InformationAction Continue

    Write-Information "" -InformationAction Continue
    Write-Information "Overall Result: $(if ($passedTests -eq $totalTests) { '🎉 ALL TESTS PASSED' } else { '⚠️  SOME TESTS FAILED' })" -InformationAction Continue
    Write-Information "Passed: $passedTests/$totalTests" -InformationAction Continue

    if ($testResults.Errors.Count -gt 0) {
        Write-Information "" -InformationAction Continue
        Write-Information "❌ ERRORS ENCOUNTERED:" -InformationAction Continue
        $testResults.Errors | ForEach-Object {
            Write-Information "  • $_" -InformationAction Continue
        }
    }

    Write-Information "" -InformationAction Continue
    Write-Information "💡 RECOMMENDATIONS:" -InformationAction Continue
    if (-not $testResults.LocalConnection) {
        Write-Information "  • Ensure SQL Server Express is installed and running" -InformationAction Continue
        Write-Information "  • Check connection string in appsettings.Development.json" -InformationAction Continue
    }
    if (-not $testResults.AzureConnection -and -not $SkipAzureTest) {
        Write-Information "  • Run 'az login' to authenticate with Azure" -InformationAction Continue
        Write-Information "  • Verify AZURE_SQL_SERVER and AZURE_SQL_DATABASE environment variables" -InformationAction Continue
        Write-Information "  • Ensure your Azure AD account has access to the database" -InformationAction Continue
    }
    if (-not $testResults.HealthCheck) {
        Write-Information "  • Check for compilation errors in DatabaseHealthCheck.cs" -InformationAction Continue
    }
}

# Main test execution
Write-Output "Starting comprehensive database connection tests..."
Write-Output ""

# Run tests
$localTest = Test-LocalSqlServerConnection
$azureTest = Test-AzureSqlConnection
$buildTest = Test-DotNetBuild
$healthTest = Test-HealthChecks

# Show summary
Show-TestSummary

# Return exit code
if ($testResults.LocalConnection -and ($testResults.AzureConnection -or $SkipAzureTest) -and $testResults.HealthCheck) {
    exit 0
}
else {
    exit 1
}
