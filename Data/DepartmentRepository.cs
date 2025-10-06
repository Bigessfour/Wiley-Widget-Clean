using Microsoft.EntityFrameworkCore;
using WileyWidget.Models;

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
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a department by ID
    /// </summary>
    public async Task<Department?> GetByIdAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>
    /// Gets a department by code
    /// </summary>
    public async Task<Department?> GetByCodeAsync(string code)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Code == code);
    }

    /// <summary>
    /// Gets departments by fund type
    /// </summary>
    public async Task<IEnumerable<Department>> GetByFundAsync(FundType fund)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Where(d => d.Fund == fund)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>
    /// Gets root departments (no parent)
    /// </summary>
    public async Task<IEnumerable<Department>> GetRootDepartmentsAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Where(d => d.ParentDepartmentId == null)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>
    /// Gets child departments for a parent department
    /// </summary>
    public async Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentDepartmentId)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Where(d => d.ParentDepartmentId == parentDepartmentId)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a department with all its child departments (hierarchical)
    /// </summary>
    public async Task<Department?> GetWithChildrenAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Include(d => d.ChildDepartments)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>
    /// Gets a department with all its accounts
    /// </summary>
    public async Task<Department?> GetWithAccountsAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .Include(d => d.Accounts)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <summary>
    /// Adds a new department
    /// </summary>
    public async Task<Department> AddAsync(Department department)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        context.Departments.Add(department);
        await context.SaveChangesAsync();
        return department;
    }

    /// <summary>
    /// Updates an existing department
    /// </summary>
    public async Task<Department> UpdateAsync(Department department)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        
        // Detach any existing tracked entity with the same key
        var existingEntry = context.ChangeTracker.Entries<Department>()
            .FirstOrDefault(e => e.Entity.Id == department.Id);
        if (existingEntry != null)
        {
            existingEntry.State = EntityState.Detached;
        }
        
        context.Departments.Update(department);
        await context.SaveChangesAsync();
        return department;
    }

    /// <summary>
    /// Deletes a department by ID
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var department = await context.Departments.FindAsync(id);
        if (department == null)
            return false;

        // Check if department has child departments
        var hasChildren = await context.Departments
            .AnyAsync(d => d.ParentDepartmentId == id);
        if (hasChildren)
        {
            throw new InvalidOperationException("Cannot delete department with child departments. Delete child departments first.");
        }

        // Check if department has accounts
        var hasAccounts = await context.MunicipalAccounts
            .AnyAsync(a => a.DepartmentId == id);
        if (hasAccounts)
        {
            throw new InvalidOperationException("Cannot delete department with associated accounts. Reassign or delete accounts first.");
        }

        context.Departments.Remove(department);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Checks if a department exists by code
    /// </summary>
    public async Task<bool> ExistsByCodeAsync(string code, int? excludeId = null)
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.Departments.Where(d => d.Code == code);
        
        if (excludeId.HasValue)
        {
            query = query.Where(d => d.Id != excludeId.Value);
        }
        
        return await query.AnyAsync();
    }

    /// <summary>
    /// Gets the total number of departments
    /// </summary>
    public async Task<int> GetCountAsync()
    {
        using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Departments
            .AsNoTracking()
            .CountAsync();
    }
}
