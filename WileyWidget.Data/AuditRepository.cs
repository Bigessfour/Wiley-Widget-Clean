#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for audit trail data operations
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public AuditRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets audit trail entries within a date range
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AuditEntries
            .Where(a => a.Timestamp >= startDate && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets audit trail entries for a specific entity type
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AuditEntries
            .Where(a => a.EntityType == entityType && a.Timestamp >= startDate && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Gets audit trail entries for a specific entity
    /// </summary>
    public async Task<IEnumerable<AuditEntry>> GetAuditTrailForEntityAsync(string entityType, int entityId, DateTime startDate, DateTime endDate)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.AuditEntries
            .Where(a => a.EntityType == entityType && a.EntityId == entityId && a.Timestamp >= startDate && a.Timestamp <= endDate)
            .OrderByDescending(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

    /// <summary>
    /// Adds a new audit entry
    /// </summary>
    public async Task AddAuditEntryAsync(AuditEntry auditEntry)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.AuditEntries.Add(auditEntry);
        await context.SaveChangesAsync();
    }
}