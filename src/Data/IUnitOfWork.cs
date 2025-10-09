#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace WileyWidget.Data;

/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations in a single transaction
/// Ensures ACID compliance for complex operations (e.g., update budget + create audit log)
/// </summary>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Enterprise repository access
    /// </summary>
    IEnterpriseRepository Enterprises { get; }

    /// <summary>
    /// Municipal account repository access
    /// </summary>
    IMunicipalAccountRepository MunicipalAccounts { get; }

    /// <summary>
    /// Utility customer repository access
    /// </summary>
    IUtilityCustomerRepository UtilityCustomers { get; }

    /// <summary>
    /// Saves all pending changes to the database
    /// </summary>
    /// <returns>Number of affected rows</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new transaction
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation within a transaction, automatically committing or rolling back
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a void operation within a transaction
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);
}
