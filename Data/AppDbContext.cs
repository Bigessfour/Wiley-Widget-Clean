using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WileyWidget.Models;

namespace WileyWidget.Data;

/// <summary>
/// Application database context for Entity Framework Core
/// Configures database schema and provides access to entity sets
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// DbSet for Enterprise entities (Water, Sewer, Trash, Apartments)
    /// </summary>
    public DbSet<Enterprise> Enterprises { get; set; }

    /// <summary>
    /// DbSet for BudgetInteraction entities (shared costs between enterprises)
    /// </summary>
    public DbSet<BudgetInteraction> BudgetInteractions { get; set; }

    /// <summary>
    /// DbSet for OverallBudget entities (municipal budget snapshots)
    /// </summary>
    public DbSet<OverallBudget> OverallBudgets { get; set; }

    /// <summary>
    /// DbSet for MunicipalAccount entities (governmental accounting accounts)
    /// </summary>
    public DbSet<MunicipalAccount> MunicipalAccounts { get; set; }

    /// <summary>
    /// DbSet for UtilityCustomer entities (municipal utility customers)
    /// </summary>
    public DbSet<UtilityCustomer> UtilityCustomers { get; set; }

    /// <summary>
    /// DbSet for Widget entities
    /// </summary>
    public DbSet<Widget> Widgets { get; set; }

    /// <summary>
    /// Constructor for dependency injection
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Parameterless constructor for testing/mocking
    /// </summary>
    protected AppDbContext()
    {
    }

    /// <summary>
    /// Configures the model and relationships
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Enterprise entity
        modelBuilder.Entity<Enterprise>(entity =>
        {
            // Table name
            entity.ToTable("Enterprises");

            // Primary key
            entity.HasKey(e => e.Id);

            // Property configurations
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CurrentRate)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.MonthlyExpenses)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.CitizenCount)
                .IsRequired();

            entity.Property(e => e.Notes)
                .HasMaxLength(500);

            // Concurrency token
            if (Database.IsSqlite())
            {
                entity.Property(e => e.RowVersion)
                    .HasDefaultValueSql("randomblob(8)");
            }
            else
            {
                entity.Property(e => e.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            }

            // Indexes for performance
            entity.HasIndex(e => e.Name).IsUnique();
        });

        // Configure relationships to avoid convention ambiguity
        modelBuilder.Entity<Enterprise>()
            .HasMany(e => e.BudgetInteractions)
            .WithOne(bi => bi.PrimaryEnterprise)
            .HasForeignKey(bi => bi.PrimaryEnterpriseId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure BudgetInteraction entity
        modelBuilder.Entity<BudgetInteraction>(entity =>
        {
            // Table name
            entity.ToTable("BudgetInteractions");

            // Primary key
            entity.HasKey(bi => bi.Id);

            // Secondary enterprise relationship (no inverse navigation)
            entity.HasOne(bi => bi.SecondaryEnterprise)
                .WithMany()  // Explicitly no inverse relationship
                .HasForeignKey(bi => bi.SecondaryEnterpriseId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Property configurations
            entity.Property(bi => bi.InteractionType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(bi => bi.Description)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(bi => bi.MonthlyAmount)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(bi => bi.IsCost)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(bi => bi.Notes)
                .HasMaxLength(300);

            // Indexes for performance
            entity.HasIndex(bi => bi.PrimaryEnterpriseId);
            entity.HasIndex(bi => bi.SecondaryEnterpriseId);
            entity.HasIndex(bi => bi.InteractionType);
        });

        // Configure OverallBudget entity
        modelBuilder.Entity<OverallBudget>(entity =>
        {
            // Table name
            entity.ToTable("OverallBudgets");

            // Primary key
            entity.HasKey(ob => ob.Id);

            // Property configurations
            entity.Property(ob => ob.SnapshotDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(ob => ob.TotalMonthlyRevenue)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(ob => ob.TotalMonthlyExpenses)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(ob => ob.TotalMonthlyBalance)
                .HasColumnType("decimal(18,2)");

            entity.Property(ob => ob.TotalCitizensServed)
                .IsRequired();

            entity.Property(ob => ob.AverageRatePerCitizen)
                .HasColumnType("decimal(18,2)");

            entity.Property(ob => ob.Notes)
                .HasMaxLength(500);

            entity.Property(ob => ob.IsCurrent)
                .IsRequired()
                .HasDefaultValue(false);

            // Ensure only one current budget snapshot
            entity.HasIndex(ob => ob.IsCurrent)
                .IsUnique()
                .HasFilter("IsCurrent = 1");

            // Indexes for performance
            entity.HasIndex(ob => ob.SnapshotDate);
        });

        // Configure MunicipalAccount entity
        modelBuilder.Entity<MunicipalAccount>(entity =>
        {
            // Table name
            entity.ToTable("MunicipalAccounts");

            // Primary key
            entity.HasKey(ma => ma.Id);

            // Property configurations
            entity.Property(ma => ma.AccountNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(ma => ma.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(ma => ma.Type)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(ma => ma.Fund)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(ma => ma.Balance)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(ma => ma.BudgetAmount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(ma => ma.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(ma => ma.QuickBooksId)
                .HasMaxLength(50);

            entity.Property(ma => ma.Notes)
                .HasMaxLength(200);

            // Indexes for performance
            entity.HasIndex(ma => ma.AccountNumber).IsUnique();
            entity.HasIndex(ma => ma.Type);
            entity.HasIndex(ma => ma.Fund);
            entity.HasIndex(ma => ma.IsActive);
            entity.HasIndex(ma => ma.QuickBooksId);
        });

        // Configure UtilityCustomer entity
        modelBuilder.Entity<UtilityCustomer>(entity =>
        {
            entity.ToTable("UtilityCustomers");

            entity.Property(uc => uc.AccountNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(uc => uc.FirstName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(uc => uc.LastName)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(uc => uc.CompanyName)
                .HasMaxLength(100);

            entity.Property(uc => uc.ServiceAddress)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(uc => uc.ServiceCity)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(uc => uc.ServiceState)
                .IsRequired()
                .HasMaxLength(2);

            entity.Property(uc => uc.ServiceZipCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(uc => uc.MailingAddress)
                .HasMaxLength(200);

            entity.Property(uc => uc.MailingCity)
                .HasMaxLength(50);

            entity.Property(uc => uc.MailingState)
                .HasMaxLength(2);

            entity.Property(uc => uc.MailingZipCode)
                .HasMaxLength(10);

            entity.Property(uc => uc.PhoneNumber)
                .HasMaxLength(15);

            entity.Property(uc => uc.EmailAddress)
                .HasMaxLength(100);

            entity.Property(uc => uc.MeterNumber)
                .HasMaxLength(20);

            entity.Property(uc => uc.CustomerType)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(uc => uc.ServiceLocation)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(uc => uc.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasDefaultValue(CustomerStatus.Active);

            entity.Property(uc => uc.AccountOpenDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(uc => uc.CurrentBalance)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(uc => uc.TaxId)
                .HasMaxLength(20);

            entity.Property(uc => uc.BusinessLicenseNumber)
                .HasMaxLength(20);

            entity.Property(uc => uc.Notes)
                .HasMaxLength(500);

            entity.Property(uc => uc.CreatedDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(uc => uc.LastModifiedDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes for performance
            entity.HasIndex(uc => uc.AccountNumber).IsUnique();
            entity.HasIndex(uc => uc.CustomerType);
            entity.HasIndex(uc => uc.ServiceLocation);
            entity.HasIndex(uc => uc.Status);
            entity.HasIndex(uc => uc.MeterNumber);
            entity.HasIndex(uc => uc.EmailAddress);

            // Concurrency token
            if (Database.IsSqlite())
            {
                // SQLite does not support SQL Server ROWVERSION; mark as concurrency token
                // and use a default 8-byte blob. Mark as ValueGeneratedNever so EF will
                // include values we set in UPDATE statements rather than treating the
                // value as store-generated.
                entity.Property(uc => uc.RowVersion)
                    .IsConcurrencyToken()
                    .ValueGeneratedNever()
                    .HasDefaultValueSql("randomblob(8)");
            }
            else
            {
                entity.Property(uc => uc.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            }
        });

        // Configure Widget entity
        modelBuilder.Entity<Widget>(entity =>
        {
            // Table name
            entity.ToTable("Widgets");

            // Primary key
            entity.HasKey(w => w.Id);

            // Property configurations
            entity.Property(w => w.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(w => w.Description)
                .HasMaxLength(500);

            entity.Property(w => w.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(w => w.Quantity)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(w => w.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(w => w.CreatedDate)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(w => w.ModifiedDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(w => w.Category)
                .HasMaxLength(50);

            entity.Property(w => w.SKU)
                .HasMaxLength(20);

            // Concurrency token
            if (Database.IsSqlite())
            {
                entity.Property(w => w.RowVersion)
                    .HasDefaultValueSql("randomblob(8)");
            }
            else
            {
                entity.Property(w => w.RowVersion)
                    .IsRowVersion()
                    .IsConcurrencyToken();
            }

            // Indexes for performance
            entity.HasIndex(w => w.Name);
            entity.HasIndex(w => w.Category);
            entity.HasIndex(w => w.CreatedDate);
            entity.HasIndex(w => w.IsActive);
            entity.HasIndex(w => w.SKU)
                .IsUnique()
                .HasFilter("[SKU] IS NOT NULL");
        });
    }

    /// <summary>
    /// Configures database context options
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development only
        if (Debugger.IsAttached)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        // Configure query tracking and other behaviors
        optionsBuilder.ConfigureWarnings(warnings =>
        {
            // Suppress common warnings that are expected
            warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
        });
    }

    /// <summary>
    /// Saves changes to the database
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // For SQLite provider, manually manage RowVersion for concurrency tokens
        if (Database.IsSqlite())
        {
            ApplySqliteRowVersionBehavior();
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves changes to the database
    /// </summary>
    public override int SaveChanges()
    {
        // For SQLite provider, manually manage RowVersion for concurrency tokens
        if (Database.IsSqlite())
        {
            ApplySqliteRowVersionBehavior();
        }
        return base.SaveChanges();
    }

    // (async override above handles SaveChangesAsync)

    private static byte[] GenerateRowVersion()
    {
        var bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    private void ApplySqliteRowVersionBehavior()
    {
        // Ensure RowVersion is never null on insert, and changes on update for entities with concurrency tokens
        foreach (var entry in ChangeTracker.Entries<UtilityCustomer>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
                {
                    entry.Entity.RowVersion = GenerateRowVersion();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Always generate a new RowVersion on updates for SQLite so the concurrency token changes
                entry.Entity.RowVersion = GenerateRowVersion();
            }
        }

        foreach (var entry in ChangeTracker.Entries<Enterprise>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
                {
                    entry.Entity.RowVersion = GenerateRowVersion();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Always generate a new RowVersion on updates for SQLite so the concurrency token changes
                entry.Entity.RowVersion = GenerateRowVersion();
            }
        }

        foreach (var entry in ChangeTracker.Entries<Widget>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.RowVersion == null || entry.Entity.RowVersion.Length == 0)
                {
                    entry.Entity.RowVersion = GenerateRowVersion();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Always generate a new RowVersion on updates for SQLite so the concurrency token changes
                entry.Entity.RowVersion = GenerateRowVersion();
            }
        }
    }
}
