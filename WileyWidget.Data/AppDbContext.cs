#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
    {
        // Initialize DbSets to satisfy nullable reference types
        MunicipalAccounts = Set<MunicipalAccount>();
        Departments = Set<Department>();
        BudgetEntries = Set<BudgetEntry>();
        Funds = Set<Fund>();
        Transactions = Set<Transaction>();
        Enterprises = Set<Enterprise>();
        AppSettings = Set<AppSettings>();
        FiscalYearSettings = Set<FiscalYearSettings>();
        UtilityCustomers = Set<UtilityCustomer>();
        BudgetPeriods = Set<BudgetPeriod>();
        AuditEntries = Set<AuditEntry>();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Only configure if not already configured
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        // Add data seeding using UseSeeding for dynamic data
        optionsBuilder.UseSeeding((context, _) =>
        {
            SeedData(context);
        });

        base.OnConfiguring(optionsBuilder);
    }

    public DbSet<MunicipalAccount> MunicipalAccounts { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<BudgetEntry> BudgetEntries { get; set; }
    public DbSet<Fund> Funds { get; set; }
    public DbSet<Transaction> Transactions { get; set; } // New
    public DbSet<Enterprise> Enterprises { get; set; } // New
    public DbSet<AppSettings> AppSettings { get; set; } // New
    public DbSet<FiscalYearSettings> FiscalYearSettings { get; set; } // New
    public DbSet<UtilityCustomer> UtilityCustomers { get; set; } // New
    public DbSet<BudgetPeriod> BudgetPeriods { get; set; } // New
    public DbSet<Invoice> Invoices { get; set; } // New
    public DbSet<AuditEntry> AuditEntries { get; set; } // New

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BudgetEntry (updated)
        modelBuilder.Entity<BudgetEntry>(entity =>
        {
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => new { e.AccountNumber, e.FiscalYear }).IsUnique();
            entity.HasIndex(e => e.SourceRowNumber); // New: Excel import queries
            entity.HasIndex(e => e.ActivityCode); // New: GASB reporting
            entity.Property(e => e.BudgetedAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
            entity.Property(e => e.ActualAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.EncumbranceAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SourceFilePath).HasMaxLength(500);
            entity.Property(e => e.ActivityCode).HasMaxLength(10);
            entity.ToTable(t => t.HasCheckConstraint("CK_Budget_Positive", "[BudgetedAmount] > 0"));
        });

        // Department hierarchy
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.DepartmentCode).IsUnique(); // New: Unique code
        });

        // BudgetPeriod configuration
        modelBuilder.Entity<BudgetPeriod>(entity =>
        {
            entity.HasIndex(e => e.Year);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => new { e.Year, e.Status });
        });

        // MunicipalAccount configuration
        modelBuilder.Entity<MunicipalAccount>(entity =>
        {
            entity.OwnsOne(e => e.AccountNumber, owned =>
            {
                owned.Property(a => a.Value).HasColumnName("AccountNumber").HasMaxLength(20).IsRequired();
            });
            entity.HasOne(e => e.Department)
                  .WithMany()
                  .HasForeignKey(e => e.DepartmentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BudgetPeriod)
                  .WithMany(bp => bp.Accounts)
                  .HasForeignKey(e => e.BudgetPeriodId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentAccount)
                  .WithMany(e => e.ChildAccounts)
                  .HasForeignKey(e => e.ParentAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.BudgetPeriodId);
            entity.HasIndex(e => e.ParentAccountId);
            entity.HasIndex(e => new { e.Fund, e.Type });
            entity.Property(e => e.Balance).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BudgetAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RowVersion).IsConcurrencyToken();
        });

        // Fund relations
        modelBuilder.Entity<Fund>(entity =>
        {
            entity.HasMany(f => f.BudgetEntries)
                  .WithOne(be => be.Fund)
                  .HasForeignKey(be => be.FundId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // New: Transaction
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.BudgetEntry)
                  .WithMany(be => be.Transactions)
                  .HasForeignKey(t => t.BudgetEntryId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(t => t.TransactionDate);
            entity.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            entity.Property(t => t.Type).HasMaxLength(50);
            entity.Property(t => t.Description).HasMaxLength(200);
            entity.ToTable(t => t.HasCheckConstraint("CK_Transaction_NonZero", "[Amount] != 0"));
        });

        // New: Invoice
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasOne(i => i.Vendor)
                  .WithMany(v => v.Invoices)
                  .HasForeignKey(i => i.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.MunicipalAccount)
                  .WithMany(ma => ma.Invoices)
                  .HasForeignKey(i => i.MunicipalAccountId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(i => i.VendorId);
            entity.HasIndex(i => i.MunicipalAccountId);
            entity.HasIndex(i => i.InvoiceDate);
            entity.Property(i => i.Amount).HasColumnType("decimal(18,2)");
            entity.Property(i => i.InvoiceNumber).HasMaxLength(50);
        });

        // New: BudgetInteraction relationships
        modelBuilder.Entity<BudgetInteraction>(entity =>
        {
            entity.HasOne(bi => bi.PrimaryEnterprise)
                  .WithMany() // No inverse navigation
                  .HasForeignKey(bi => bi.PrimaryEnterpriseId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(bi => bi.SecondaryEnterprise)
                  .WithMany() // No inverse navigation
                  .HasForeignKey(bi => bi.SecondaryEnterpriseId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(bi => bi.PrimaryEnterpriseId);
            entity.HasIndex(bi => bi.SecondaryEnterpriseId);
            entity.Property(bi => bi.MonthlyAmount).HasColumnType("decimal(18,2)");
            entity.Property(bi => bi.InteractionType).HasMaxLength(50);
            entity.Property(bi => bi.Description).HasMaxLength(200);
            entity.Property(bi => bi.Notes).HasMaxLength(300);
        });

        // Auditing
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(WileyWidget.Models.Entities.IAuditable).IsAssignableFrom(e.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).Property("CreatedAt").HasDefaultValueSql("GETUTCDATE()");
            modelBuilder.Entity(entityType.ClrType).Property("UpdatedAt").ValueGeneratedOnAddOrUpdate();
        }

        // Set all foreign keys to Restrict to avoid SQL Server cascade path issues
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // Override restrict for cascade deletes on MunicipalAccount relationships
        modelBuilder.Entity<MunicipalAccount>()
            .HasOne(ma => ma.ParentAccount)
            .WithMany(pa => pa.ChildAccounts)
            .HasForeignKey(ma => ma.ParentAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Invoice>()
            .HasOne(i => i.MunicipalAccount)
            .WithMany(ma => ma.Invoices)
            .HasForeignKey(i => i.MunicipalAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    // Data seeding method for UseSeeding
    private void SeedData(DbContext context)
    {
        var dbContext = (AppDbContext)context;

        // Seed Departments
        if (!dbContext.Departments.Any())
        {
            dbContext.Departments.AddRange(
                new Department { Name = "General Government", DepartmentCode = "GEN" },
                new Department { Name = "Public Works", DepartmentCode = "PW" }
            );
            dbContext.SaveChanges();
        }

        // Get department IDs
        var generalGovDept = dbContext.Departments.FirstOrDefault(d => d.DepartmentCode == "GEN");
        var publicWorksDept = dbContext.Departments.FirstOrDefault(d => d.DepartmentCode == "PW");

        // Seed Budget Periods
        if (!dbContext.BudgetPeriods.Any())
        {
            dbContext.BudgetPeriods.Add(
                new BudgetPeriod 
                { 
                    Year = 2026,
                    Name = "FY 2026 Budget",
                    Status = BudgetStatus.Adopted,
                    StartDate = DateTime.Parse("2026-01-01"),
                    EndDate = DateTime.Parse("2026-12-31"),
                    IsActive = true
                }
            );
            dbContext.SaveChanges();
        }

        // Get budget period ID
        var budgetPeriod = dbContext.BudgetPeriods.FirstOrDefault(bp => bp.Year == 2026);

        // Seed Enterprises
        if (!dbContext.Enterprises.Any())
        {
            dbContext.Enterprises.AddRange(
                new Enterprise 
                { 
                    Name = "Town of Wiley", 
                    Description = "Municipal government for Wiley, CO (pop ~300)",
                    CitizenCount = 300,
                    CurrentRate = 8.5m,
                    TotalBudget = 2500000m,
                    Type = "General",
                    Status = EnterpriseStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                },
                new Enterprise 
                { 
                    Name = "Wiley Sanitation District", 
                    Description = "Sanitation services for Wiley area",
                    CitizenCount = 250,
                    CurrentRate = 38.0m,
                    TotalBudget = 1500000m,
                    Type = "Sanitation",
                    Status = EnterpriseStatus.Active,
                    CreatedDate = DateTime.UtcNow,
                    ModifiedDate = DateTime.UtcNow
                }
            );
            dbContext.SaveChanges();
        }

        // Seed Utility Customers
        if (!dbContext.UtilityCustomers.Any())
        {
            dbContext.UtilityCustomers.AddRange(
                new UtilityCustomer 
                { 
                    AccountNumber = "CUST001",
                    FirstName = "John",
                    LastName = "Doe",
                    ServiceAddress = "123 Main St",
                    ServiceCity = "Wiley",
                    ServiceState = "CO",
                    ServiceZipCode = "81092",
                    CustomerType = CustomerType.Residential,
                    CurrentBalance = 45.67m,
                    LastPaymentDate = DateTime.Parse("2025-10-01"),
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                },
                new UtilityCustomer 
                { 
                    AccountNumber = "CUST002",
                    CompanyName = "Jane Smith Business",
                    ServiceAddress = "456 Oak Ave",
                    ServiceCity = "Wiley",
                    ServiceState = "CO",
                    ServiceZipCode = "81092",
                    CustomerType = CustomerType.Commercial,
                    CurrentBalance = 150.25m,
                    LastPaymentDate = DateTime.Parse("2025-09-15"),
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                }
            );
            dbContext.SaveChanges();
        }

        // Seed Municipal Accounts
        if (!dbContext.MunicipalAccounts.Any() && generalGovDept != null && publicWorksDept != null && budgetPeriod != null)
        {
            dbContext.MunicipalAccounts.AddRange(
                new MunicipalAccount 
                { 
                    AccountNumber = new AccountNumber("101.100"),
                    Name = "General Fund Checking",
                    Type = AccountType.Asset,
                    Fund = MunicipalFundType.General,
                    DepartmentId = generalGovDept.Id,
                    BudgetPeriodId = budgetPeriod.Id,
                    Balance = 500000m,
                    BudgetAmount = 500000m,
                    IsActive = true
                },
                new MunicipalAccount 
                { 
                    AccountNumber = new AccountNumber("201.100"),
                    Name = "Enterprise Fund Checking",
                    Type = AccountType.Asset,
                    Fund = MunicipalFundType.Enterprise,
                    DepartmentId = publicWorksDept.Id,
                    BudgetPeriodId = budgetPeriod.Id,
                    Balance = 200000m,
                    BudgetAmount = 200000m,
                    IsActive = true
                }
            );
            dbContext.SaveChanges();
        }

        // Seed Budget Entries
        if (!dbContext.BudgetEntries.Any() && generalGovDept != null && publicWorksDept != null)
        {
            dbContext.BudgetEntries.AddRange(
                new BudgetEntry 
                { 
                    AccountNumber = "110",
                    Description = "CASH IN BANK",
                    BudgetedAmount = 100000m,
                    ActualAmount = 95000m,
                    FiscalYear = 2026,
                    StartPeriod = DateOnly.Parse("2026-01-01"),
                    EndPeriod = DateOnly.Parse("2026-12-31"),
                    FundType = FundType.GeneralFund,
                    DepartmentId = generalGovDept.Id
                },
                new BudgetEntry 
                { 
                    AccountNumber = "310",
                    Description = "STATE APPORTIONMENT",
                    BudgetedAmount = 50000m,
                    ActualAmount = 45000m,
                    FiscalYear = 2026,
                    StartPeriod = DateOnly.Parse("2026-01-01"),
                    EndPeriod = DateOnly.Parse("2026-12-31"),
                    FundType = FundType.GeneralFund,
                    DepartmentId = generalGovDept.Id
                },
                new BudgetEntry 
                { 
                    AccountNumber = "410",
                    Description = "CAPITAL IMP - BALL COMPLEX",
                    BudgetedAmount = 150000m,
                    ActualAmount = 120000m,
                    FiscalYear = 2026,
                    StartPeriod = DateOnly.Parse("2026-01-01"),
                    EndPeriod = DateOnly.Parse("2026-12-31"),
                    FundType = FundType.GeneralFund,
                    DepartmentId = generalGovDept.Id
                },
                new BudgetEntry 
                { 
                    AccountNumber = "101",
                    Description = "CHECKING ACCOUNT-Enterprise",
                    BudgetedAmount = 200000m,
                    ActualAmount = 180000m,
                    FiscalYear = 2026,
                    StartPeriod = DateOnly.Parse("2026-01-01"),
                    EndPeriod = DateOnly.Parse("2026-12-31"),
                    FundType = FundType.EnterpriseFund,
                    DepartmentId = publicWorksDept.Id
                },
                new BudgetEntry 
                { 
                    AccountNumber = "301",
                    Description = "SEWER SALES",
                    BudgetedAmount = 300000m,
                    ActualAmount = 275000m,
                    FiscalYear = 2026,
                    StartPeriod = DateOnly.Parse("2026-01-01"),
                    EndPeriod = DateOnly.Parse("2026-12-31"),
                    FundType = FundType.EnterpriseFund,
                    DepartmentId = publicWorksDept.Id
                }
            );
            dbContext.SaveChanges();
        }

        dbContext.SaveChanges();
    }

    // Hierarchy query for UI (e.g., BudgetView SfTreeGrid)
    public IQueryable<BudgetEntry> GetBudgetHierarchy(int fiscalYear)
    {
        return BudgetEntries
            .Include(be => be.Parent)
            .Include(be => be.Children)
            .Include(be => be.Department)
            .Include(be => be.Fund)
            .Where(be => be.FiscalYear == fiscalYear)
            .AsNoTracking();
    }

    // New: Transaction query for UI (e.g., MunicipalAccountView)
    public IQueryable<Transaction> GetTransactionsForBudget(int budgetEntryId)
    {
        return Transactions
            .Include(t => t.BudgetEntry)
            .Where(t => t.BudgetEntryId == budgetEntryId)
            .OrderByDescending(t => t.TransactionDate)
            .AsNoTracking();
    }

    // New: Excel import validation query
    public IQueryable<BudgetEntry> GetBudgetEntriesBySource(string sourceFilePath, int? rowNumber = null)
    {
        return BudgetEntries
            .Where(be => be.SourceFilePath == sourceFilePath && (rowNumber == null || be.SourceRowNumber == rowNumber))
            .AsNoTracking();
    }
}
