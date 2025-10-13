#nullable enable

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using WileyWidget.Models;
using WileyWidget.Models.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Only configure if not already configured
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        }
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BudgetEntry (updated)
        modelBuilder.Entity<BudgetEntry>(entity =>
        {
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Cascade);
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
                  .OnDelete(DeleteBehavior.SetNull);
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

        // Seed sample data (from TOW/WSD Excel structure) - MOVED TO UseSeeding
        // modelBuilder.Entity<Fund>().HasData(
        //     new Fund { Id = 1, FundCode = "100", Name = "General Fund", Type = FundType.GeneralFund },
        //     new Fund { Id = 2, FundCode = "200", Name = "Utility Fund", Type = FundType.EnterpriseFund }
        // );
        // modelBuilder.Entity<Department>().HasData(
        //     new Department { Id = 1, Name = "Public Works", DepartmentCode = "DPW" },
        //     new Department { Id = 2, Name = "Sanitation", DepartmentCode = "SAN", ParentId = 1 }
        // );
        // modelBuilder.Entity<BudgetEntry>().HasData(
        //     new BudgetEntry { Id = 1, AccountNumber = "405", Description = "Road Maintenance", BudgetedAmount = 50000, FiscalYear = 2026, DepartmentId = 1, FundId = 1, ActivityCode = "GOV" },
        //     new BudgetEntry { Id = 2, AccountNumber = "405.1", Description = "Paving", BudgetedAmount = 20000, FiscalYear = 2026, ParentId = 1, DepartmentId = 1, FundId = 1, ActivityCode = "GOV" }
        // );
        // modelBuilder.Entity<Transaction>().HasData(
        //     new Transaction { Id = 1, BudgetEntryId = 1, Amount = 10000, Type = "Payment", TransactionDate = new DateTime(2025, 10, 13, 12, 0, 0, DateTimeKind.Utc), Description = "Initial payment for road work" }
        // );
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
