#nullable enable

using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;
// Clean Architecture: Interfaces defined in Business layer, implemented in Data layer
using WileyWidget.Business.Interfaces;

namespace WileyWidget.Data;

/// <summary>
/// Repository implementation for Department data operations
/// </summary>
public class DepartmentRepository : IDepartmentRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    /// <summary>
    /// Constructor with dependency injection
    /// </summary>
    public DepartmentRepository(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// Gets all departments
    /// </summary>
    public async Task<IEnumerable<Department>> GetAllAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a department by ID
    /// </summary>
    public async Task<Department?> GetByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>
    /// Gets a department by name
    /// </summary>
    public async Task<Department?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Department name cannot be null or empty", nameof(code));

        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name == code);
    }

    /// <summary>
    /// Adds a new department
    /// </summary>
    public async Task AddAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Departments.Add(department);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing department
    /// </summary>
    public async Task UpdateAsync(Department department)
    {
        if (department == null)
            throw new ArgumentNullException(nameof(department));

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Departments.Update(department);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a department by ID
    /// </summary>
    public async Task DeleteAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var department = await context.Departments.FindAsync(id);
        if (department != null)
        {
            context.Departments.Remove(department);
            await context.SaveChangesAsync();
        }
    }
}