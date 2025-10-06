using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WileyWidget.Models;
using WileyWidget.Services.Excel;

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
    /// DbSet for Department entities (municipal departments)
    /// </summary>
    public DbSet<Department> Departments { get; set; }

    /// <summary>
    /// DbSet for BudgetPeriod entities (multi-year budget tracking)
    /// </summary>
    public DbSet<BudgetPeriod> BudgetPeriods { get; set; }

    /// <summary>
    /// DbSet for BudgetEntry entities (individual budget line items)
    /// </summary>
    public DbSet<BudgetEntry> BudgetEntries { get; set; }

    /// <summary>
    /// DbSet for UtilityCustomer entities (municipal utility customers)
    /// </summary>
    public DbSet<UtilityCustomer> UtilityCustomers { get; set; }

    /// <summary>
    /// DbSet for Widget entities
    /// </summary>
    public DbSet<Widget> Widgets { get; set; }

    private readonly ILogger<AppDbContext>? _logger;
    private readonly AccountTypeValidator? _accountTypeValidator;

    /// <summary>
    /// Constructor for dependency injection
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, ILogger<AppDbContext>? logger = null)
        : base(options)
    {
        _logger = logger;
        if (_logger != null)
        {
            _accountTypeValidator = new AccountTypeValidator(_logger);
        }
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

        // Configure value converter for AccountNumber value object
        modelBuilder.Entity<MunicipalAccount>()
            .Property(ma => ma.AccountNumber)
            .HasConversion(
                accountNumber => accountNumber.Value,
                value => new AccountNumber(value)
            )
            .IsRequired()
            .HasMaxLength(20);

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
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

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
            entity.Property(ma => ma.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(ma => ma.Type)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(ma => ma.Fund)
                .IsRequired()
                .HasConversion<string>();

            entity.Property(ma => ma.FundClass)
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
            entity.HasIndex("AccountNumber").IsUnique();
            entity.HasIndex(ma => ma.Type);
            entity.HasIndex(ma => ma.Fund);
            entity.HasIndex(ma => ma.IsActive);
            entity.HasIndex(ma => ma.QuickBooksId);
        });

        // Configure Department entity
        modelBuilder.Entity<Department>(entity =>
        {
            // Table name
            entity.ToTable("Departments");

            // Primary key
            entity.HasKey(d => d.Id);

            // Property configurations
            entity.Property(d => d.Code)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.Fund)
                .IsRequired()
                .HasConversion<string>();

            // Self-referencing relationship for parent-child hierarchy
            entity.HasOne(d => d.ParentDepartment)
                .WithMany(d => d.ChildDepartments)
                .HasForeignKey(d => d.ParentDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            entity.HasIndex(d => d.Code).IsUnique();
            entity.HasIndex(d => d.Name);
            entity.HasIndex(d => d.Fund);
            entity.HasIndex(d => d.ParentDepartmentId);
        });

        // Configure BudgetPeriod entity
        modelBuilder.Entity<BudgetPeriod>(entity =>
        {
            // Table name
            entity.ToTable("BudgetPeriods");

            // Primary key
            entity.HasKey(bp => bp.Id);

            // Property configurations
            entity.Property(bp => bp.Year)
                .IsRequired();

            entity.Property(bp => bp.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(bp => bp.CreatedDate)
                .IsRequired();

            entity.Property(bp => bp.Status)
                .IsRequired()
                .HasConversion<string>();

            // Indexes for performance
            entity.HasIndex(bp => bp.Year);
            entity.HasIndex(bp => bp.Status);
            entity.HasIndex(bp => new { bp.Year, bp.Status });
        });

        // Configure relationships for MunicipalAccount
        modelBuilder.Entity<MunicipalAccount>()
            .HasOne(ma => ma.Department)
            .WithMany(d => d.Accounts)
            .HasForeignKey(ma => ma.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MunicipalAccount>()
            .HasOne(ma => ma.ParentAccount)
            .WithMany(ma => ma.ChildAccounts)
            .HasForeignKey(ma => ma.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MunicipalAccount>()
            .HasOne(ma => ma.BudgetPeriod)
            .WithMany(bp => bp.Accounts)
            .HasForeignKey(ma => ma.BudgetPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

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
            entity.Property(uc => uc.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
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
            entity.Property(w => w.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            // Indexes for performance
            entity.HasIndex(w => w.Name);
            entity.HasIndex(w => w.Category);
            entity.HasIndex(w => w.CreatedDate);
            entity.HasIndex(w => w.IsActive);
            entity.HasIndex(w => w.SKU)
                .IsUnique()
                .HasFilter("[SKU] IS NOT NULL");
        });

        // Seed data
        modelBuilder.Entity<Enterprise>().HasData(
            new Enterprise
            {
                Id = 1,
                Name = "Water Utility",
                Type = "Utility",
                CurrentRate = 25.00M,
                MonthlyExpenses = 15000.00M,
                CitizenCount = 2500,
                TotalBudget = 180000.00M,
                Notes = "Municipal water service",
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }
            },
            new Enterprise
            {
                Id = 2,
                Name = "Sewer Service",
                Type = "Utility",
                CurrentRate = 35.00M,
                MonthlyExpenses = 22000.00M,
                CitizenCount = 2500,
                TotalBudget = 264000.00M,
                Notes = "Wastewater treatment and sewer service",
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 }
            },
            new Enterprise
            {
                Id = 3,
                Name = "Trash Collection",
                Type = "Service",
                CurrentRate = 15.00M,
                MonthlyExpenses = 8000.00M,
                CitizenCount = 2500,
                TotalBudget = 96000.00M,
                Notes = "Residential and commercial waste collection",
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 3 }
            },
            new Enterprise
            {
                Id = 4,
                Name = "Municipal Apartments",
                Type = "Housing",
                CurrentRate = 450.00M,
                MonthlyExpenses = 12000.00M,
                CitizenCount = 150,
                TotalBudget = 144000.00M,
                Notes = "Low-income housing units",
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 4 }
            }
        );

        modelBuilder.Entity<Widget>().HasData(
            new Widget
            {
                Id = 1,
                Name = "Rate Calculator",
                Description = "Calculate utility rates based on usage and budget",
                Category = "Calculator",
                SKU = "WW-RATE-CALC",
                Price = 0.00M,
                IsActive = true,
                CreatedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 5 }
            },
            new Widget
            {
                Id = 2,
                Name = "Budget Analyzer",
                Description = "Analyze municipal budget allocations and expenses",
                Category = "Analyzer",
                SKU = "WW-BUDGET-ANALYZER",
                Price = 0.00M,
                IsActive = true,
                CreatedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 6 }
            },
            new Widget
            {
                Id = 3,
                Name = "Configuration Manager",
                Description = "Manage application settings and configurations",
                Category = "Management",
                SKU = "WW-CONFIG-MGR",
                Price = 0.00M,
                IsActive = true,
                CreatedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                ModifiedDate = new DateTime(2025, 9, 27, 0, 0, 0, DateTimeKind.Utc),
                RowVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 7 }
            }
        );
    }

    /// <summary>
    /// Configures database context options
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Enable sensitive data logging in development only
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        // Add detailed EF Core query logging
        optionsBuilder.LogTo(message => Console.WriteLine($"EF Core: {message}"),
            new[] { Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted },
            LogLevel.Information);

        // Configure query tracking and other behaviors
        optionsBuilder.ConfigureWarnings(warnings =>
        {
            // Suppress common warnings that are expected
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning);
        });
    }

    /// <summary>
    /// Validates GASB compliance for MunicipalAccount changes before saving.
    /// </summary>
    private async Task ValidateGASBComplianceAsync()
    {
        if (_accountTypeValidator == null)
            return;

        // Get all MunicipalAccount entries that are being added or modified
        var municipalAccountEntries = ChangeTracker.Entries<MunicipalAccount>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        if (!municipalAccountEntries.Any())
            return;

        // Get all existing accounts for context (to validate fund balances)
        var allAccounts = await MunicipalAccounts.ToListAsync();
        var accountsToValidate = allAccounts.Concat(municipalAccountEntries).ToList();

        // Validate account type compliance
        var validationResult = _accountTypeValidator.ValidateAccountTypeCompliance(accountsToValidate);

        if (!validationResult.IsValid)
        {
            var errorMessage = $"GASB compliance validation failed: {string.Join("; ", validationResult.Errors)}";
            _logger?.LogError("GASB validation failed: {Errors}", string.Join("; ", validationResult.Errors));
            throw new InvalidOperationException(errorMessage);
        }

        if (validationResult.Warnings.Any())
        {
            _logger?.LogWarning("GASB validation warnings: {Warnings}", string.Join("; ", validationResult.Warnings));
        }
    }

    /// <summary>
    /// Validates GASB compliance for MunicipalAccount changes before saving (sync version).
    /// </summary>
    private void ValidateGASBCompliance()
    {
        if (_accountTypeValidator == null)
            return;

        // Get all MunicipalAccount entries that are being added or modified
        var municipalAccountEntries = ChangeTracker.Entries<MunicipalAccount>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        if (!municipalAccountEntries.Any())
            return;

        // Get all existing accounts for context (to validate fund balances)
        var allAccounts = MunicipalAccounts.ToList();
        var accountsToValidate = allAccounts.Concat(municipalAccountEntries).ToList();

        // Validate account type compliance
        var validationResult = _accountTypeValidator.ValidateAccountTypeCompliance(accountsToValidate);

        if (!validationResult.IsValid)
        {
            var errorMessage = $"GASB compliance validation failed: {string.Join("; ", validationResult.Errors)}";
            _logger?.LogError("GASB validation failed: {Errors}", string.Join("; ", validationResult.Errors));
            throw new InvalidOperationException(errorMessage);
        }

        if (validationResult.Warnings.Any())
        {
            _logger?.LogWarning("GASB validation warnings: {Warnings}", string.Join("; ", validationResult.Warnings));
        }
    }

    /// <summary>
    /// Saves changes to the database asynchronously
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await ValidateGASBComplianceAsync();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Saves changes to the database
    /// </summary>
    public override int SaveChanges()
    {
        ValidateGASBCompliance();
        return base.SaveChanges();
    }
}
