using Microsoft.EntityFrameworkCore;
using WileyWidget.Data;
using WileyWidget.Models;
using WileyWidget.IntegrationTests.Infrastructure;
using Xunit;

namespace WileyWidget.IntegrationTests.LayeredArchitecture;

/// <summary>
/// Basic integration test to verify the layered architecture works.
/// Tests that Models, Data, and Business layers can be used together.
/// </summary>
public class LayeredArchitectureSmokeTest : SqlServerTestBase
{
    [Fact]
    public async Task CanCreateAndRetrieveEnterprise()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Water Utility",
            Type = "Water",
            CreatedDate = DateTime.UtcNow
        };

        // Act
        await using var context = CreateDbContext();
        context.Enterprises.Add(enterprise);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.Enterprises.FirstOrDefaultAsync(e => e.Name == "Test Water Utility");
        Assert.NotNull(retrieved);
        Assert.Equal("Water", retrieved.Type);
        Assert.Equal("Test Water Utility", retrieved.Name);
    }

    [Fact]
    public async Task CanCreateAndRetrieveMunicipalAccount()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000-000"),
            Name = "General Fund"
        };

        // Act
        await using var context = CreateDbContext();
        context.MunicipalAccounts.Add(account);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.MunicipalAccounts.FirstOrDefaultAsync(a => a.AccountNumber == new AccountNumber("101-1000-000"));
        Assert.NotNull(retrieved);
        Assert.Equal("General Fund", retrieved.Name);
    }

    [Fact]
    public async Task CanCreateAndRetrieveDepartment()
    {
        // Arrange
        var department = new Department
        {
            Name = "Public Works",
            Code = "PW"
        };

        // Act
        await using var context = CreateDbContext();
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        // Assert
        var retrieved = await context.Departments.FirstOrDefaultAsync(d => d.Code == "PW");
        Assert.NotNull(retrieved);
        Assert.Equal("Public Works", retrieved.Name);
    }
}