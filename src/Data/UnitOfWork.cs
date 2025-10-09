#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

namespace WileyWidget.Data;

/// <summary>
/// Implementation of Unit of Work pattern using EF Core DbContext
/// Coordinates multiple repositories and provides transaction management
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    // Lazy-initialized repositories
    private IEnterpriseRepository? _enterprises;
    private IMunicipalAccountRepository? _municipalAccounts;
    private IUtilityCustomerRepository? _utilityCustomers;

    /// <summary>
    /// Constructor with DbContext injection
    /// </summary>
    public UnitOfWork(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Enterprise repository (lazy-loaded)
    /// </summary>
    public IEnterpriseRepository Enterprises
    {
        get
        {
            if (_enterprises == null)
            {
                // Create factory that returns our existing context
                var factory = new SingleContextFactory(_context);
                _enterprises = new EnterpriseRepository(factory);
            }
            return _enterprises;
        }
    }

    /// <summary>
    /// Municipal account repository (lazy-loaded)
    /// </summary>
    public IMunicipalAccountRepository MunicipalAccounts
    {
        get
        {
            if (_municipalAccounts == null)
            {
                var factory = new SingleContextFactory(_context);
                _municipalAccounts = new MunicipalAccountRepository(factory);
            }
            return _municipalAccounts;
        }
    }

    /// <summary>
    /// Utility customer repository (lazy-loaded)
    /// </summary>
    public IUtilityCustomerRepository UtilityCustomers
    {
        get
        {
            if (_utilityCustomers == null)
            {
                var factory = new SingleContextFactory(_context);
                _utilityCustomers = new UtilityCustomerRepository(factory);
            }
            return _utilityCustomers;
        }
    }

    /// <summary>
    /// Saves all pending changes to the database
    /// </summary>
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Log.Error(ex, "Concurrency conflict in UnitOfWork.SaveChangesAsync");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving changes in UnitOfWork");
            throw;
        }
    }

    /// <summary>
    /// Begins a new database transaction
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        Log.Debug("Transaction started: {TransactionId}", _currentTransaction.TransactionId);
        return _currentTransaction;
    }

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction in progress");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
            Log.Debug("Transaction committed: {TransactionId}", _currentTransaction.TransactionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error committing transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return; // Nothing to rollback
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            Log.Debug("Transaction rolled back: {TransactionId}", _currentTransaction.TransactionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error rolling back transaction");
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    /// <summary>
    /// Executes an operation within a transaction, automatically handling commit/rollback
    /// </summary>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Executes a void operation within a transaction
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            await operation();
            await CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Dispose pattern implementation
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Async dispose pattern implementation
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
                // Context is managed by DI, don't dispose it here
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }
        // Context is managed by DI, don't dispose it here
    }

    /// <summary>
    /// Helper factory that returns a single context instance (for UnitOfWork repo coordination)
    /// </summary>
    private class SingleContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly AppDbContext _context;

        public SingleContextFactory(AppDbContext context)
        {
            _context = context;
        }

        public AppDbContext CreateDbContext()
        {
            return _context;
        }
    }
}
