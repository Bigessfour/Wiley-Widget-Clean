#nullable enable
using Azure;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using System;
using System.Threading.Tasks;
using Serilog;

namespace WileyWidget.Data.Resilience;

/// <summary>
/// Provides Polly-based resilience policies for database operations
/// Handles transient failures from Azure SQL, authentication timeouts, and network issues
/// </summary>
public static class DatabaseResiliencePolicy
{
    /// <summary>
    /// Retry policy for Azure authentication failures
    /// Retries 3 times with exponential backoff (500ms, 1s, 2s)
    /// </summary>
    public static AsyncRetryPolicy AzureAuthRetryPolicy { get; } = Policy
        .Handle<AuthenticationFailedException>()
        .Or<RequestFailedException>(ex => ex.Status == 401 || ex.Status == 403)
        .Or<SqlException>(ex => IsTransientError(ex))
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                Log.Warning(exception,
                    "Azure authentication/transient error on attempt {RetryCount}. Retrying in {RetryDelayMs}ms",
                    retryCount, timeSpan.TotalMilliseconds);
            });

    /// <summary>
    /// Retry policy for database operation timeouts
    /// Retries 2 times with linear backoff (1s, 2s)
    /// </summary>
    public static AsyncRetryPolicy DatabaseTimeoutRetryPolicy { get; } = Policy
        .Handle<TimeoutException>()
        .Or<SqlException>(ex => ex.Number == -2) // Timeout error number
        .WaitAndRetryAsync(
            retryCount: 2,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryAttempt),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                Log.Warning(exception,
                    "Database timeout on attempt {RetryCount}. Retrying in {RetryDelaySeconds}s",
                    retryCount, timeSpan.TotalSeconds);
            });

    /// <summary>
    /// Retry policy for EF Core concurrency conflicts
    /// Retries once immediately
    /// </summary>
    public static AsyncRetryPolicy ConcurrencyRetryPolicy { get; } = Policy
        .Handle<DbUpdateConcurrencyException>()
        .RetryAsync(
            retryCount: 1,
            onRetry: (exception, retryCount) =>
            {
                Log.Warning(exception, "Concurrency conflict detected. Retrying once.");
            });

    /// <summary>
    /// Combined policy for all database operations
    /// Wraps auth, timeout, and concurrency policies
    /// </summary>
    public static AsyncRetryPolicy CombinedDatabasePolicy { get; } = Policy
        .Handle<AuthenticationFailedException>()
        .Or<RequestFailedException>(ex => ex.Status == 401 || ex.Status == 403)
        .Or<TimeoutException>()
        .Or<SqlException>(ex => IsTransientError(ex))
        .Or<DbUpdateConcurrencyException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
            onRetry: (exception, timeSpan, retryCount, context) =>
            {
                var exceptionType = exception.GetType().Name;
                Log.Warning(exception,
                    "Database operation failed with {ExceptionType} on attempt {RetryCount}. Retrying in {RetryDelayMs}ms",
                    exceptionType, retryCount, timeSpan.TotalMilliseconds);
            });

    /// <summary>
    /// Determines if a SQL exception is transient (retryable)
    /// </summary>
    private static bool IsTransientError(SqlException ex)
    {
        // Common transient error numbers for Azure SQL
        int[] transientErrorNumbers = {
            -2,     // Timeout
            -1,     // Connection broken
            2,      // Network error
            53,     // Connection failed
            64,     // Network-level error
            233,    // Connection initialization error
            10053,  // Transport-level error
            10054,  // Connection reset by peer
            10060,  // Network timeout
            40197,  // Service error processing request
            40501,  // Service busy
            40613,  // Database unavailable
            49918,  // Cannot process request (insufficient resources)
            49919,  // Cannot process create/update request (too many operations)
            49920   // Cannot process request (too many operations)
        };

        return Array.Exists(transientErrorNumbers, num => num == ex.Number);
    }

    /// <summary>
    /// Executes a database operation with combined resilience policy
    /// </summary>
    public static Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
    {
        return CombinedDatabasePolicy.ExecuteAsync(operation);
    }

    /// <summary>
    /// Executes a void database operation with combined resilience policy
    /// </summary>
    public static Task ExecuteAsync(Func<Task> operation)
    {
        return CombinedDatabasePolicy.ExecuteAsync(operation);
    }
}
