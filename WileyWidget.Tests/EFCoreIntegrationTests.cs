using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WileyWidget.Data;
using WileyWidget.Models;
using Xunit;
using Xunit.Abstractions;

namespace WileyWidget.Tests;

/// <summary>
/// Basic Entity Framework Core integration tests
/// Tests basic CRUD operations and database connectivity
/// </summary>
public sealed class EFCoreIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ITestOutputHelper _output;

    public EFCoreIntegrationTests(ITestOutputHelper output)
    {
        _output = output;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .LogTo(message => _output.WriteLine(message), LogLevel.Information)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Database_CanConnect_Successfully()
    {
        // Arrange & Act
        var canConnect = await _context.Database.CanConnectAsync();

        // Assert
        Assert.True(canConnect);
    }

    [Fact]
    public async Task MunicipalAccounts_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var account = new MunicipalAccount
        {
            AccountNumber = new AccountNumber("101-1000"),
            Name = "Test Cash Account",
            Type = AccountType.Cash,
            Fund = MunicipalFundType.General,
            FundClass = FundClass.Governmental,
            Balance = 1000.00m,
            IsActive = true
        };

        // Act
        _context.MunicipalAccounts.Add(account);
        await _context.SaveChangesAsync();

        var retrieved = await _context.MunicipalAccounts
            .FirstOrDefaultAsync(a => a.AccountNumber!.Value == "101-1000");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Test Cash Account", retrieved.Name);
        Assert.Equal(1000.00m, retrieved.Balance);
    }

    [Fact]
    public async Task Departments_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var department = new Department
        {
            Name = "Test Department",
            DepartmentCode = "TEST"
        };

        // Act
        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        var retrieved = await _context.Departments
            .FirstOrDefaultAsync(d => d.DepartmentCode == "TEST");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Test Department", retrieved.Name);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
