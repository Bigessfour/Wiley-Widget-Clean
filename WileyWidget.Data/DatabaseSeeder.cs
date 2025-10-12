using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WileyWidget.Data;
using WileyWidget.Models;
using System.Globalization;

namespace WileyWidget.Data;

/// <summary>
/// Enhanced database seeder with realistic municipal utility customer data
/// </summary>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public DatabaseSeeder(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Seeds the database with sample enterprise and customer data
    /// </summary>
    public async Task SeedAsync()
    {
        // Only seed if no enterprises exist
        if (await _context.Enterprises.AnyAsync())
        {
            Console.WriteLine("Database already seeded. Skipping...");
            return;
        }

        Console.WriteLine("Seeding database with sample enterprise and customer data...");

        // Create sample enterprises
        var enterprises = await CreateEnterprisesAsync();

        // Create sample customers
        var customers = await CreateCustomersAsync();

        // Create sample municipal accounts
        var municipalAccounts = await CreateMunicipalAccountsAsync();

        // Create budget interactions
        var interactions = await CreateBudgetInteractionsAsync(enterprises);

        // Create overall budget
        var overallBudget = await CreateOverallBudgetAsync(enterprises);

        Console.WriteLine("Database seeded successfully!");
        Console.WriteLine($"Created {enterprises.Count} enterprises");
        Console.WriteLine($"Created {customers.Count} customers");
        Console.WriteLine($"Created {municipalAccounts.Count} municipal accounts");
        Console.WriteLine($"Created {interactions.Count} budget interactions");
        Console.WriteLine($"Total Monthly Revenue: ${overallBudget.TotalMonthlyRevenue:F2}");
        Console.WriteLine($"Total Monthly Expenses: ${overallBudget.TotalMonthlyExpenses:F2}");
        Console.WriteLine($"Monthly Balance: ${overallBudget.TotalMonthlyBalance:F2}");
    }

    private async Task<List<Enterprise>> CreateEnterprisesAsync()
    {
        var enterprises = new List<Enterprise>
        {
            new Enterprise
            {
                Name = "Water",
                CurrentRate = 5.00m,
                MonthlyExpenses = 7500.00m,
                CitizenCount = 500,
                Notes = "Municipal water utility serving 500 citizens"
            },
            new Enterprise
            {
                Name = "Sewer",
                CurrentRate = 3.50m,
                MonthlyExpenses = 4200.00m,
                CitizenCount = 500,
                Notes = "Wastewater treatment and sewer services"
            },
            new Enterprise
            {
                Name = "Trash",
                CurrentRate = 15.00m,
                MonthlyExpenses = 2800.00m,
                CitizenCount = 500,
                Notes = "Solid waste collection and disposal"
            },
            new Enterprise
            {
                Name = "Apartments",
                CurrentRate = 0.00m, // Legacy apartments may not charge rates
                MonthlyExpenses = 1200.00m,
                CitizenCount = 50, // Fewer citizens in apartments
                Notes = "Legacy apartment complex maintenance"
            }
        };

        await _context.Enterprises.AddRangeAsync(enterprises);
        await _context.SaveChangesAsync();

        return enterprises;
    }

    private async Task<List<UtilityCustomer>> CreateCustomersAsync()
    {
        // Create realistic municipal customer data
        var customers = new List<UtilityCustomer>
        {
            new UtilityCustomer
            {
                FirstName = "John",
                LastName = "Smith",
                AccountNumber = "ACCT-00001",
                ServiceAddress = "123 Main Street",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62701",
                EmailAddress = "john.smith@email.com",
                PhoneNumber = "(555) 123-4567",
                CustomerType = CustomerType.Residential,
                MeterNumber = "METER-0000001"
            },
            new UtilityCustomer
            {
                FirstName = "Mary",
                LastName = "Johnson",
                AccountNumber = "ACCT-00002",
                ServiceAddress = "456 Oak Avenue",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62702",
                EmailAddress = "mary.johnson@email.com",
                PhoneNumber = "(555) 234-5678",
                CustomerType = CustomerType.Residential,
                MeterNumber = "METER-0000002"
            },
            new UtilityCustomer
            {
                FirstName = "Robert",
                LastName = "Williams",
                CompanyName = "Williams Construction LLC",
                AccountNumber = "ACCT-00003",
                ServiceAddress = "789 Industrial Blvd",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62703",
                EmailAddress = "info@williamsconstruction.com",
                PhoneNumber = "(555) 345-6789",
                CustomerType = CustomerType.Commercial,
                MeterNumber = "METER-0000003"
            },
            new UtilityCustomer
            {
                FirstName = "Jennifer",
                LastName = "Brown",
                AccountNumber = "ACCT-00004",
                ServiceAddress = "321 Pine Street",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62704",
                EmailAddress = "jennifer.brown@email.com",
                PhoneNumber = "(555) 456-7890",
                CustomerType = CustomerType.Residential,
                MeterNumber = "METER-0000004"
            },
            new UtilityCustomer
            {
                FirstName = "Michael",
                LastName = "Davis",
                CompanyName = "Davis Plumbing Services",
                AccountNumber = "ACCT-00005",
                ServiceAddress = "654 Elm Drive",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62705",
                EmailAddress = "michael@davisplumbing.com",
                PhoneNumber = "(555) 567-8901",
                CustomerType = CustomerType.Commercial,
                MeterNumber = "METER-0000005"
            },
            new UtilityCustomer
            {
                FirstName = "Lisa",
                LastName = "Garcia",
                AccountNumber = "ACCT-00006",
                ServiceAddress = "987 Maple Lane",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62706",
                EmailAddress = "lisa.garcia@email.com",
                PhoneNumber = "(555) 678-9012",
                CustomerType = CustomerType.MultiFamily,
                MeterNumber = "METER-0000006"
            },
            new UtilityCustomer
            {
                FirstName = "David",
                LastName = "Miller",
                CompanyName = "Miller Manufacturing Inc",
                AccountNumber = "ACCT-00007",
                ServiceAddress = "147 Industrial Park",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62707",
                EmailAddress = "david@miller-mfg.com",
                PhoneNumber = "(555) 789-0123",
                CustomerType = CustomerType.Industrial,
                MeterNumber = "METER-0000007"
            },
            new UtilityCustomer
            {
                FirstName = "Sarah",
                LastName = "Wilson",
                AccountNumber = "ACCT-00008",
                ServiceAddress = "258 Cedar Court",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62708",
                EmailAddress = "sarah.wilson@email.com",
                PhoneNumber = "(555) 890-1234",
                CustomerType = CustomerType.Residential,
                MeterNumber = "METER-0000008"
            },
            new UtilityCustomer
            {
                FirstName = "James",
                LastName = "Moore",
                CompanyName = "Springfield School District",
                AccountNumber = "ACCT-00009",
                ServiceAddress = "369 Education Way",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62709",
                EmailAddress = "facilities@springfieldschools.edu",
                PhoneNumber = "(555) 901-2345",
                CustomerType = CustomerType.Institutional,
                MeterNumber = "METER-0000009"
            },
            new UtilityCustomer
            {
                FirstName = "Emily",
                LastName = "Taylor",
                AccountNumber = "ACCT-00010",
                ServiceAddress = "741 Birch Street",
                ServiceCity = "Springfield",
                ServiceState = "IL",
                ServiceZipCode = "62710",
                EmailAddress = "emily.taylor@email.com",
                PhoneNumber = "(555) 012-3456",
                CustomerType = CustomerType.Residential,
                MeterNumber = "METER-0000010"
            }
        };

        await _context.UtilityCustomers.AddRangeAsync(customers);
        await _context.SaveChangesAsync();

        return customers;
    }

    private async Task<List<MunicipalAccount>> CreateMunicipalAccountsAsync()
    {
        var accounts = new List<MunicipalAccount>
        {
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1010"),
                Name = "Water Utility Revenue",
                Type = AccountType.Sales,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = 2500.00m,
                BudgetAmount = 2500.00m,
                RowVersion = Array.Empty<byte>()
            },
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1020"),
                Name = "Sewer Utility Revenue",
                Type = AccountType.Sales,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = 1750.00m,
                BudgetAmount = 1750.00m,
                RowVersion = Array.Empty<byte>()
            },
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("1030"),
                Name = "Trash Collection Revenue",
                Type = AccountType.Sales,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = 7500.00m,
                BudgetAmount = 7500.00m,
                RowVersion = Array.Empty<byte>()
            },
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("2010"),
                Name = "Water System Maintenance",
                Type = AccountType.Supplies,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = -7500.00m,
                BudgetAmount = -7500.00m,
                RowVersion = Array.Empty<byte>()
            },
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("2020"),
                Name = "Sewer Treatment Operations",
                Type = AccountType.Services,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = -4200.00m,
                BudgetAmount = -4200.00m,
                RowVersion = Array.Empty<byte>()
            },
            new MunicipalAccount
            {
                AccountNumber = new AccountNumber("2030"),
                Name = "Waste Collection Services",
                Type = AccountType.Services,
                Fund = FundType.Enterprise,
                FundClass = FundClass.Proprietary,
                Balance = -2800.00m,
                BudgetAmount = -2800.00m,
                RowVersion = Array.Empty<byte>()
            }
        };

        await _context.MunicipalAccounts.AddRangeAsync(accounts);
        await _context.SaveChangesAsync();

        return accounts;
    }

    private async Task<List<BudgetInteraction>> CreateBudgetInteractionsAsync(List<Enterprise> enterprises)
    {
        var interactions = new List<BudgetInteraction>
        {
            new BudgetInteraction
            {
                PrimaryEnterpriseId = enterprises.First(e => e.Name == "Water").Id,
                SecondaryEnterpriseId = enterprises.First(e => e.Name == "Sewer").Id,
                InteractionType = "SharedCost",
                Description = "Shared infrastructure maintenance costs",
                MonthlyAmount = 800.00m,
                IsCost = true,
                Notes = "Joint maintenance of shared water/sewer pipes"
            },
            new BudgetInteraction
            {
                PrimaryEnterpriseId = enterprises.First(e => e.Name == "Trash").Id,
                InteractionType = "Transfer",
                Description = "Surplus funds transfer to general fund",
                MonthlyAmount = 1200.00m,
                IsCost = false,
                Notes = "Monthly surplus transferred to municipal general fund"
            }
        };

        await _context.BudgetInteractions.AddRangeAsync(interactions);
        await _context.SaveChangesAsync();

        return interactions;
    }

    private async Task<OverallBudget> CreateOverallBudgetAsync(List<Enterprise> enterprises)
    {
        var overallBudget = new OverallBudget
        {
            TotalMonthlyRevenue = enterprises.Sum(e => e.MonthlyRevenue),
            TotalMonthlyExpenses = enterprises.Sum(e => e.MonthlyExpenses),
            TotalCitizensServed = enterprises.Sum(e => e.CitizenCount),
            IsCurrent = true,
            Notes = "Initial budget snapshot with enhanced customer data"
        };

        overallBudget.TotalMonthlyBalance = overallBudget.TotalMonthlyRevenue - overallBudget.TotalMonthlyExpenses;
        overallBudget.AverageRatePerCitizen = overallBudget.TotalCitizensServed > 0 ?
            overallBudget.TotalMonthlyRevenue / overallBudget.TotalCitizensServed : 0;

        await _context.OverallBudgets.AddAsync(overallBudget);
        await _context.SaveChangesAsync();

        return overallBudget;
    }
}
