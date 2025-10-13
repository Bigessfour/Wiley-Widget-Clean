#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

namespace WileyWidget.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=WileyWidgetDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=true";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);
        optionsBuilder.EnableDetailedErrors();
#pragma warning disable CA2000 // UseLoggerFactory takes ownership of the ILoggerFactory and disposes it when the DbContext is disposed
        optionsBuilder.UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()));
#pragma warning restore CA2000

        // Add seeding for initial data
        optionsBuilder.UseSeeding((context, _) =>
        {
            var dbContext = (AppDbContext)context;
            
            // Seed Funds
            if (!dbContext.Funds.Any())
            {
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Funds] ON");
                dbContext.Funds.AddRange(
                    new Fund { Id = 1, FundCode = "100", Name = "General Fund", Type = FundType.GeneralFund },
                    new Fund { Id = 2, FundCode = "200", Name = "Utility Fund", Type = FundType.EnterpriseFund }
                );
                dbContext.SaveChanges();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Funds] OFF");
            }
            
            // Seed Departments
            if (!dbContext.Departments.Any())
            {
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Departments] ON");
                dbContext.Departments.AddRange(
                    new Department { Id = 1, Name = "Public Works", DepartmentCode = "DPW" },
                    new Department { Id = 2, Name = "Sanitation", DepartmentCode = "SAN", ParentId = 1 }
                );
                dbContext.SaveChanges();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Departments] OFF");
            }
            
            // Seed BudgetEntries
            if (!dbContext.BudgetEntries.Any())
            {
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [BudgetEntries] ON");
                dbContext.BudgetEntries.AddRange(
                    new BudgetEntry { Id = 1, AccountNumber = "405", Description = "Road Maintenance", BudgetedAmount = 50000, FiscalYear = 2026, DepartmentId = 1, FundId = 1, ActivityCode = "GOV", IsGASBCompliant = true, CreatedAt = DateTime.UtcNow },
                    new BudgetEntry { Id = 2, AccountNumber = "405.1", Description = "Paving", BudgetedAmount = 20000, FiscalYear = 2026, ParentId = 1, DepartmentId = 1, FundId = 1, ActivityCode = "GOV", IsGASBCompliant = true, CreatedAt = DateTime.UtcNow }
                );
                dbContext.SaveChanges();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [BudgetEntries] OFF");
            }
            
            // Seed Transactions
            if (!dbContext.Transactions.Any())
            {
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Transactions] ON");
                dbContext.Transactions.AddRange(
                    new Transaction { Id = 1, BudgetEntryId = 1, Amount = 10000, Type = "Payment", TransactionDate = DateTime.UtcNow, Description = "Initial payment for road work", CreatedAt = DateTime.UtcNow }
                );
                dbContext.SaveChanges();
                dbContext.Database.ExecuteSqlRaw("SET IDENTITY_INSERT [Transactions] OFF");
            }
        });

        return new AppDbContext(optionsBuilder.Options);
    }
}
