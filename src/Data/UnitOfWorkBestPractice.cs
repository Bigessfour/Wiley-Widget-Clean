#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;

namespace WileyWidget.Data;

/// <summary>
/// BEST PRACTICE Unit of Work implementation following Clean Architecture principles
/// Key improvements over original:
/// 1. Repositories injected via constructor (DI-friendly)
/// 2. Repositories do NOT call SaveChanges - only UnitOfWork does
/// 3. Transaction nesting supported (idempotent)
/// 4. Change tracker exposure for advanced scenarios
/// </summary>
public class UnitOfWorkBestPractice : IUnitOfWorkBestPractice
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;
    private bool _transactionOwner; // Track if WE started the transaction

    // BEST PRACTICE: Inject repositories via constructor (registered in DI)
    private readonly IEnterpriseRepository _enterprises;
    private readonly IMunicipalAccountRepository _municipalAccounts;
    private readonly IUtilityCustomerRepository _utilityCustomers;

    /// <summary>
    /// Constructor with DbContext and Repository injection
    /// BEST PRACTICE: All dependencies injected, not created internally
    /// </summary>
    public UnitOfWorkBestPractice(
        AppDbContext context,
        IEnterpriseRepository enterprises,
        IMunicipalAccountRepository municipalAccounts,
        IUtilityCustomerRepository utilityCustomers)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _enterprises = enterprises ?? throw new ArgumentNullException(nameof(enterprises));
        _municipalAccounts = municipalAccounts ?? throw new ArgumentNullException(nameof(municipalAccounts));
        _utilityCustomers = utilityCustomers ?? throw new ArgumentNullException(nameof(utilityCustomers));
    }

    /// <summary>
    /// BEST PRACTICE: Direct property access (no lazy loading - already injected)
    /// </summary>
    public IEnterpriseRepository Enterprises => _enterprises;

    /// <summary>
    /// Municipal account repository
    /// </summary>
    public IMunicipalAccountRepository MunicipalAccounts => _municipalAccounts;

    /// <summary>
    /// Utility customer repository
    /// </summary>
    public IUtilityCustomerRepository UtilityCustomers => _utilityCustomers;

    /// <summary>
    /// BEST PRACTICE: ONLY place SaveChanges is called
    /// Repositories modify tracked entities but don't persist
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
    /// BEST PRACTICE: Idempotent transaction - returns existing or creates new
    /// </summary>
    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // If transaction already exists, return it (support nesting)
        if (_currentTransaction != null)
        {
            Log.Debug("Transaction already in progress, returning existing transaction");
            return _currentTransaction;
        }

        // Check if EF Core already has an ambient transaction
        if (_context.Database.CurrentTransaction != null)
        {
            _currentTransaction = _context.Database.CurrentTransaction;
            _transactionOwner = false; // We didn't create it
            Log.Debug("Using existing EF Core ambient transaction: {TransactionId}", _currentTransaction.TransactionId);
            return _currentTransaction;
        }

        // Create new transaction
        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _transactionOwner = true; // We created it, so we're responsible for committing/rolling back
        Log.Debug("Transaction started: {TransactionId}", _currentTransaction.TransactionId);
        return _currentTransaction;
    }

    /// <summary>
    /// BEST PRACTICE: Only commit if we started the transaction (respect nesting)
    /// </summary>
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            Log.Warning("CommitTransactionAsync called but no transaction in progress");
            return; // Gracefully handle - maybe SaveChanges was called directly
        }

        // Only commit if we're the transaction owner
        if (!_transactionOwner)
        {
            Log.Debug("Not committing transaction - we're not the owner");
            return;
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
            if (_transactionOwner && _currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
                _transactionOwner = false;
            }
        }
    }

    /// <summary>
    /// BEST PRACTICE: Rollback with ownership check
    /// </summary>
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            return; // Nothing to rollback
        }

        // Only rollback if we're the transaction owner
        if (!_transactionOwner)
        {
            Log.Debug("Not rolling back transaction - we're not the owner");
            return;
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
            if (_transactionOwner && _currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
                _transactionOwner = false;
            }
        }
    }

    /// <summary>
    /// BEST PRACTICE: Supports nested transactions gracefully
    /// </summary>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
        {
            throw new ArgumentNullException(nameof(operation));
        }

        // Check if transaction already exists (nested call)
        var existingTransaction = _currentTransaction != null;
        
        if (!existingTransaction)
        {
            // Start new transaction only if none exists
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await operation();
            
            // Only commit if we started the transaction
            if (!existingTransaction && _transactionOwner)
            {
                await CommitTransactionAsync(cancellationToken);
            }
            
            return result;
        }
        catch
        {
            // Only rollback if we started the transaction
            if (!existingTransaction && _transactionOwner)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
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
        await ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return 0; // Dummy return for generic method
        }, cancellationToken);
    }

    /// <summary>
    /// BEST PRACTICE: Expose change tracker state
    /// Allows checking if there are unsaved changes before committing
    /// </summary>
    public bool HasChanges()
    {
        return _context.ChangeTracker.HasChanges();
    }

    /// <summary>
    /// BEST PRACTICE: Clear change tracker (useful for long-lived contexts)
    /// Detaches all tracked entities to prevent memory leaks
    /// </summary>
    public void DetachAll()
    {
        foreach (var entry in _context.ChangeTracker.Entries().ToList())
        {
            entry.State = EntityState.Detached;
        }
        Log.Debug("All entities detached from change tracker");
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
                // Rollback any uncommitted transaction
                if (_currentTransaction != null && _transactionOwner)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                    Log.Warning("UnitOfWork disposed with uncommitted transaction - rolled back");
                }
                // Context is managed by DI, don't dispose it here
            }
            _disposed = true;
        }
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_currentTransaction != null && _transactionOwner)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            Log.Warning("UnitOfWork disposed with uncommitted transaction - rolled back");
        }
        // Context is managed by DI, don't dispose it here
    }
}
