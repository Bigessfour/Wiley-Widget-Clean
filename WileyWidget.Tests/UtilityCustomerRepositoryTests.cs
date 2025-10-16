using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Unit tests for UtilityCustomerRepository
/// Tests all repository methods using in-memory database
/// </summary>
public class UtilityCustomerRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory;
    private readonly AppDbContext _context;
    private readonly UtilityCustomerRepository _repository;

    public UtilityCustomerRepositoryTests()
    {
        // Use SQLite in-memory database for testing (Microsoft recommended approach)
        // Provides better SQL compatibility than EF Core In-Memory provider
        var databaseName = $"UtilityCustomerTest_{Guid.NewGuid()}";
        _contextFactory = TestDbContextFactory.CreateSqliteInMemory(databaseName);
        _context = _contextFactory.CreateDbContext();
        _repository = new UtilityCustomerRepository(_contextFactory);

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UtilityCustomerRepository(null!));
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyCollection()
    {
        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_WithCustomers_ReturnsOrderedByName()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        var customer3 = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Smith",
            AccountNumber = "1003",
            CustomerType = CustomerType.Commercial,
            RowVersion = new byte[8]
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2, customer3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(3, result.Count());
        var customers = result.ToList();
        Assert.Equal("Doe", customers[0].LastName);
        Assert.Equal("Smith", customers[1].LastName);
        Assert.Equal("Smith", customers[2].LastName);
        Assert.Equal("Bob", customers[1].FirstName); // Bob Smith comes second in Smith group alphabetically
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCustomer()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(customer.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(customer.Id, result.Id);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Smith", result.LastName);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(999);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_ExistingAccountNumber_ReturnsCustomer()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "ACC-1001",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByAccountNumberAsync("ACC-1001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ACC-1001", result.AccountNumber);
        Assert.Equal("John", result.FirstName);
    }

    [Fact]
    public async Task GetByAccountNumberAsync_NonExistingAccountNumber_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByAccountNumberAsync("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCustomerTypeAsync_ExistingType_ReturnsFilteredCustomers()
    {
        // Arrange
        var residentialCustomer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        var residentialCustomer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        var commercialCustomer = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Johnson",
            AccountNumber = "2001",
            CustomerType = CustomerType.Commercial,
            RowVersion = new byte[8]
        };

        await _context.UtilityCustomers.AddRangeAsync(residentialCustomer1, residentialCustomer2, commercialCustomer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCustomerTypeAsync(CustomerType.Residential);

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, c => Assert.Equal(CustomerType.Residential, c.CustomerType));
        Assert.Contains(result, c => c.FirstName == "John");
        Assert.Contains(result, c => c.FirstName == "Jane");
    }

    [Fact]
    public async Task GetByCustomerTypeAsync_NoMatchingType_ReturnsEmptyCollection()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential,
            RowVersion = new byte[8]
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByCustomerTypeAsync(CustomerType.Commercial);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByServiceLocationAsync_ExistingLocation_ReturnsFilteredCustomers()
    {
        // Arrange
        var insideCustomer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            ServiceLocation = ServiceLocation.InsideCityLimits
        };
        var outsideCustomer = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            ServiceLocation = ServiceLocation.OutsideCityLimits
        };

        await _context.UtilityCustomers.AddRangeAsync(insideCustomer, outsideCustomer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByServiceLocationAsync(ServiceLocation.OutsideCityLimits);

        // Assert
        Assert.Single(result);
        Assert.Equal("Jane", result.First().FirstName);
        Assert.Equal(ServiceLocation.OutsideCityLimits, result.First().ServiceLocation);
    }

    [Fact]
    public async Task GetActiveCustomersAsync_ReturnsOnlyActiveCustomers()
    {
        // Arrange
        var activeCustomer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            Status = CustomerStatus.Active,
            RowVersion = new byte[8]
        };
        var activeCustomer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            Status = CustomerStatus.Active,
            RowVersion = new byte[8]
        };
        var inactiveCustomer = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Johnson",
            AccountNumber = "1003",
            Status = CustomerStatus.Inactive,
            RowVersion = new byte[8]
        };

        await _context.UtilityCustomers.AddRangeAsync(activeCustomer1, activeCustomer2, inactiveCustomer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveCustomersAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, c => Assert.Equal(CustomerStatus.Active, c.Status));
    }

    [Fact]
    public async Task GetCustomersWithBalanceAsync_ReturnsCustomersWithOutstandingBalance()
    {
        // Arrange
        var customerWithBalance1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CurrentBalance = 150.00m
        };
        var customerWithBalance2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            CurrentBalance = 75.50m
        };
        var customerWithZeroBalance = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Johnson",
            AccountNumber = "1003",
            CurrentBalance = 0.00m
        };

        await _context.UtilityCustomers.AddRangeAsync(customerWithBalance1, customerWithBalance2, customerWithZeroBalance);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCustomersWithBalanceAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, c => Assert.True(c.CurrentBalance > 0));
        var customers = result.ToList();
        Assert.Equal(150.00m, customers[0].CurrentBalance);
        Assert.Equal(75.50m, customers[1].CurrentBalance);
    }

    [Fact]
    public async Task SearchAsync_WithFirstName_ReturnsMatchingCustomers()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Johnny",
            LastName = "Doe",
            AccountNumber = "1002"
        };
        var customer3 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Johnson",
            AccountNumber = "1003"
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2, customer3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchAsync("John");

        // Assert
        Assert.Equal(3, result.Count()); // John, Johnny, and Jane (Johnson contains "John")
        Assert.Contains(result, c => c.FirstName == "John");
        Assert.Contains(result, c => c.FirstName == "Johnny");
        Assert.Contains(result, c => c.LastName == "Johnson");
    }

    [Fact]
    public async Task SearchAsync_WithLastName_ReturnsMatchingCustomers()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Smith",
            AccountNumber = "1002"
        };
        var customer3 = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Johnson",
            AccountNumber = "1003"
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2, customer3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchAsync("Smith");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, c => Assert.Equal("Smith", c.LastName));
    }

    [Fact]
    public async Task SearchAsync_WithAccountNumber_ReturnsMatchingCustomers()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "ACC-1001"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "ACC-1002"
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchAsync("ACC-100");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, c => c.AccountNumber == "ACC-1001");
        Assert.Contains(result, c => c.AccountNumber == "ACC-1002");
    }

    [Fact]
    public async Task SearchAsync_WithCompanyName_ReturnsMatchingCustomers()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            CompanyName = "ABC Corporation",
            AccountNumber = "1001"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            CompanyName = "XYZ Corporation",
            AccountNumber = "1002"
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.SearchAsync("Corporation");

        // Assert
        Assert.Equal(2, result.Count());
        Assert.Contains(result, c => c.CompanyName?.Contains("Corporation") == true);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyOrNullTerm_ReturnsAllCustomers()
    {
        // Arrange
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002"
        };

        await _context.UtilityCustomers.AddRangeAsync(customer1, customer2);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _repository.SearchAsync("");
        var result2 = await _repository.SearchAsync(null!);

        // Assert
        Assert.Equal(2, result1.Count());
        Assert.Equal(2, result2.Count());
    }

    [Fact]
    public async Task AddAsync_ValidCustomer_AddsAndSetsTimestamps()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential,
            CurrentBalance = 100.00m
        };

        var beforeAdd = DateTime.Now;

        // Act
        var result = await _repository.AddAsync(customer);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Smith", result.LastName);
        Assert.Equal("1001", result.AccountNumber);
        Assert.True(result.CreatedDate >= beforeAdd);
        Assert.True(result.LastModifiedDate >= beforeAdd);

        // Verify it was added to database
        var savedCustomer = await _context.UtilityCustomers.FindAsync(result.Id);
        Assert.NotNull(savedCustomer);
        Assert.Equal("John", savedCustomer.FirstName);
    }

    [Fact]
    public async Task UpdateAsync_ExistingCustomer_UpdatesAndSetsModifiedDate()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            CustomerType = CustomerType.Residential
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        var originalCreatedDate = customer.CreatedDate;
        var beforeUpdate = DateTime.Now;

        customer.FirstName = "Johnny";
        customer.CurrentBalance = 200.00m;

        // Act
        var result = await _repository.UpdateAsync(customer);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Johnny", result.FirstName);
        Assert.Equal(200.00m, result.CurrentBalance);
        Assert.Equal(originalCreatedDate, result.CreatedDate); // Created date should not change
        Assert.True(result.LastModifiedDate >= beforeUpdate);

        // Verify it was updated in database
        var updatedCustomer = await _context.UtilityCustomers.FindAsync(customer.Id);
        Assert.NotNull(updatedCustomer);
        Assert.Equal("Johnny", updatedCustomer.FirstName);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesAndReturnsTrue()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001"
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.DeleteAsync(customer.Id);

        // Assert
        Assert.True(result);

        // Verify it was deleted from database
        var deletedCustomer = await _context.UtilityCustomers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customer.Id);
        Assert.Null(deletedCustomer);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingId_ReturnsFalse()
    {
        // Act
        var result = await _repository.DeleteAsync(999);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByAccountNumberAsync_ExistingAccountNumber_ReturnsTrue()
    {
        // Arrange
        var customer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "ACC-1001"
        };
        _context.UtilityCustomers.Add(customer);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.ExistsByAccountNumberAsync("ACC-1001");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsByAccountNumberAsync_WithExcludeId_ExcludesSpecifiedCustomer()
    {
        // Arrange
        var uniqueId1 = Guid.NewGuid().ToString("N").Substring(0, 8);
        var uniqueId2 = Guid.NewGuid().ToString("N").Substring(0, 8);
        var customer1 = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = $"ACC-{uniqueId1}"
        };
        var customer2 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = $"ACC-{uniqueId2}"
        };
        _context.UtilityCustomers.AddRange(customer1, customer2);
        await _context.SaveChangesAsync();

        // Act - Should return false because customer1 is excluded and customer2 has different account number
        var result = await _repository.ExistsByAccountNumberAsync($"acc-{uniqueId1}", customer1.Id);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsByAccountNumberAsync_NonExistingAccountNumber_ReturnsFalse()
    {
        // Act
        var result = await _repository.ExistsByAccountNumberAsync("NONEXISTENT");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCountAsync_EmptyDatabase_ReturnsZero()
    {
        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetCountAsync_WithCustomers_ReturnsCorrectCount()
    {
        // Arrange
        var customers = new List<UtilityCustomer>
        {
            new UtilityCustomer { FirstName = "John", LastName = "Smith", AccountNumber = "1001", RowVersion = new byte[8] },
            new UtilityCustomer { FirstName = "Jane", LastName = "Doe", AccountNumber = "1002", RowVersion = new byte[8] },
            new UtilityCustomer { FirstName = "Bob", LastName = "Johnson", AccountNumber = "1003", RowVersion = new byte[8] }
        };

        _context.UtilityCustomers.AddRange(customers);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCountAsync();

        // Assert
        Assert.Equal(3, result);
    }

    [Fact]
    public async Task GetCustomersOutsideCityLimitsAsync_ReturnsOnlyOutsideCustomers()
    {
        // Arrange
        var insideCustomer = new UtilityCustomer
        {
            FirstName = "John",
            LastName = "Smith",
            AccountNumber = "1001",
            ServiceLocation = ServiceLocation.InsideCityLimits
        };
        var outsideCustomer1 = new UtilityCustomer
        {
            FirstName = "Jane",
            LastName = "Doe",
            AccountNumber = "1002",
            ServiceLocation = ServiceLocation.OutsideCityLimits
        };
        var outsideCustomer2 = new UtilityCustomer
        {
            FirstName = "Bob",
            LastName = "Johnson",
            AccountNumber = "1003",
            ServiceLocation = ServiceLocation.OutsideCityLimits
        };

        await _context.UtilityCustomers.AddRangeAsync(insideCustomer, outsideCustomer1, outsideCustomer2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetCustomersOutsideCityLimitsAsync();

        // Assert
        Assert.Equal(2, result.Count());
        Assert.All(result, c => Assert.Equal(ServiceLocation.OutsideCityLimits, c.ServiceLocation));
    }

    [Fact(Skip = "SQLite doesn't support row versioning for concurrency")]
    public async Task Concurrency_Update_ShouldThrowOnStaleRowVersion()
    {
        // Use file-based SQLite for proper concurrency testing
        var dbFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        try
        {
            var connectionString = $"Data Source={dbFile}";
            var optBuilder = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString);

            // Create schema
            using (var schemaCtx = new AppDbContext(optBuilder.Options))
            {
                await schemaCtx.Database.EnsureCreatedAsync();
            }

            // Seed
            using (var seed = new AppDbContext(optBuilder.Options))
            {
                seed.UtilityCustomers.Add(new UtilityCustomer
                {
                    FirstName = "John",
                    LastName = "Smith",
                    AccountNumber = "1001",
                    CustomerType = CustomerType.Residential
                });
                await seed.SaveChangesAsync();
            }

            AppDbContext? ctx1 = null;
            AppDbContext? ctx2 = null;
            try
            {
                // First, capture the original RowVersion before any updates
                byte[] originalRowVersion;
                using (var tempCtx = new AppDbContext(optBuilder.Options))
                {
                    originalRowVersion = (byte[])tempCtx.UtilityCustomers.First().RowVersion.Clone();
                }

                ctx1 = new AppDbContext(optBuilder.Options);
                ctx2 = new AppDbContext(optBuilder.Options);

                var c1 = await ctx1.UtilityCustomers.AsTracking().FirstAsync();
                var c2 = await ctx2.UtilityCustomers.AsTracking().FirstAsync();

                // initial rowversion values captured for debugging during development

                // First update succeeds via ctx1 (ensures RowVersion changes)
                c1.FirstName = "Johnny";
                ctx1.UtilityCustomers.Update(c1);
                await ctx1.SaveChangesAsync();

                // verify update occurred (no logging in final test)
                var updatedEntity = await ctx1.UtilityCustomers.AsNoTracking().FirstAsync();

                // Reload c2 to get the updated RowVersion from database
                await ctx2.Entry(c2).ReloadAsync();

                var currentDbRowVersion2 = await ctx2.UtilityCustomers.AsNoTracking().Select(x => x.RowVersion).FirstAsync();

                // Now set the ORIGINAL value used by EF to the old RowVersion to simulate stale data
                // EF uses the OriginalValue in the WHERE clause when updating; setting the property
                // value (CurrentValue) is not sufficient because OriginalValue was refreshed by ReloadAsync.
                ctx2.Entry(c2).Property(nameof(WileyWidget.Models.UtilityCustomer.RowVersion)).OriginalValue = originalRowVersion;

                // OriginalValue is set below to simulate stale client copy

                var currentDbRowVersion = await ctx2.UtilityCustomers.AsNoTracking().Select(x => x.RowVersion).FirstAsync();

                // Second update should fail due to stale RowVersion
                c2.LastName = "Smyth";
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
                {
                    ctx2.UtilityCustomers.Update(c2);
                    await ctx2.SaveChangesAsync();
                });
            }
            finally
            {
                ctx1?.Dispose();
                ctx2?.Dispose();
            }
        }
        finally
        {
            // Clean up
            try
            {
                if (File.Exists(dbFile))
                {
                    File.Delete(dbFile);
                }
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
            _contextFactory.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}