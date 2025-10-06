using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WileyWidget.Data;
using WileyWidget.Models;

namespace WileyWidget.LifecycleTests;

internal sealed class SqliteTestAppDbContext : AppDbContext
{
    public SqliteTestAppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!Database.IsSqlite())
        {
            return;
        }

        ConfigureRowVersion(modelBuilder.Entity<Enterprise>());
        ConfigureRowVersion(modelBuilder.Entity<UtilityCustomer>());
        ConfigureRowVersion(modelBuilder.Entity<Widget>());
    }

    public override int SaveChanges()
    {
        if (Database.IsSqlite())
        {
            ApplySqliteRowVersionBehavior();
        }

        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Database.IsSqlite())
        {
            ApplySqliteRowVersionBehavior();
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureRowVersion<T>(EntityTypeBuilder<T> entity) where T : class
    {
        const string propertyName = nameof(Enterprise.RowVersion);
        var propertyBuilder = entity.Property<byte[]>(propertyName)
            .IsConcurrencyToken()
            .HasDefaultValueSql("randomblob(8)")
            .ValueGeneratedNever();

        propertyBuilder.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
        propertyBuilder.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Save);
    }

    private void ApplySqliteRowVersionBehavior()
    {
        UpdateRowVersion(ChangeTracker.Entries<Enterprise>());
        UpdateRowVersion(ChangeTracker.Entries<UtilityCustomer>());
        UpdateRowVersion(ChangeTracker.Entries<Widget>());
    }

    private static void UpdateRowVersion<TEntity>(IEnumerable<EntityEntry<TEntity>> entries) where TEntity : class
    {
        foreach (var entry in entries)
        {
            var property = entry.Property(nameof(Enterprise.RowVersion));

            if (entry.State == EntityState.Added)
            {
                if (property.CurrentValue is not byte[] bytes || bytes.Length == 0)
                {
                    property.CurrentValue = CreateRowVersion();
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                property.CurrentValue = CreateRowVersion();
            }
        }
    }

    private static byte[] CreateRowVersion()
    {
        var buffer = new byte[8];
        RandomNumberGenerator.Fill(buffer);
        return buffer;
    }
}
