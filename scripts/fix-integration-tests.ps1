# Fix Integration Tests - Replace model references and add using statements

Write-Information "Fixing integration test files..." -InformationAction Continue

# Delete and recreate TestDataBuilder.cs with correct models
$testDataBuilderContent = @'
using WileyWidget.Models;

namespace WileyWidget.IntegrationTests.Infrastructure;

/// <summary>
/// Provides builder methods for creating test data entities.
/// </summary>
public static class TestDataBuilder
{
    /// <summary>
    /// Creates a sample MunicipalAccount with default values.
    /// </summary>
    public static MunicipalAccount CreateMunicipalAccount(
        string accountNumber = "405.1",
        string accountName = "Test Municipal Account",
        decimal budgetAmount = 10000.00m)
    {
        return new MunicipalAccount(accountNumber, accountName)
        {
            BudgetAmount = budgetAmount,
            ActualAmount = 0m,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
    }

    /// <summary>
    /// Creates a sample Department.
    /// </summary>
    public static Department CreateDepartment(
        string name = "Test Department",
        string code = "TEST")
    {
        return new Department
        {
            Name = name,
            DepartmentCode = code,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
    }

    /// <summary>
    /// Creates a sample Enterprise.
    /// </summary>
    public static Enterprise CreateEnterprise(
        string name = "Test Enterprise")
    {
        return new Enterprise
        {
            Name = name,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
    }

    /// <summary>
    /// Creates a sample BudgetEntry linked to an account.
    /// </summary>
    public static BudgetEntry CreateBudgetEntry(
        int municipalAccountId,
        decimal amount = 1000.00m,
        int fiscalYear = 2025)
    {
        return new BudgetEntry
        {
            MunicipalAccountId = municipalAccountId,
            Amount = amount,
            FiscalYear = fiscalYear,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow,
            RowVersion = new byte[8]
        };
    }
}
'@

$testDataBuilderPath = "WileyWidget.IntegrationTests\Infrastructure\TestDataBuilder.cs"
Remove-Item $testDataBuilderPath -Force -ErrorAction SilentlyContinue
$testDataBuilderContent | Out-File -FilePath $testDataBuilderPath -Encoding UTF8 -Force

Write-Information "Created TestDataBuilder.cs" -InformationAction Continue

# Add using statements to test files
$testFiles = @(
    "WileyWidget.IntegrationTests\Concurrency\ConcurrencyConflictTests.cs",
    "WileyWidget.IntegrationTests\Relationships\ForeignKeyIntegrityTests.cs",
    "WileyWidget.IntegrationTests\Performance\DatabasePerformanceBenchmarks.cs",
    "WileyWidget.IntegrationTests\Performance\DatabasePerformanceTests.cs"
)

foreach ($file in $testFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Check if using WileyWidget.Models is already there
        if ($content -notmatch 'using WileyWidget\.Models;') {
            # Add after the last using statement
            $content = $content -replace '(using [^;]+;)(\r?\n\r?\nnamespace)', "`$1`nusing WileyWidget.Models;`$2"
            $content | Out-File -FilePath $file -Encoding UTF8 -Force
            Write-Information "Updated $file" -InformationAction Continue
        }
    }
}

Write-Information "Integration test files fixed!" -InformationAction Continue
Write-Information "Run: dotnet build WileyWidget.IntegrationTests/WileyWidget.IntegrationTests.csproj" -InformationAction Continue
