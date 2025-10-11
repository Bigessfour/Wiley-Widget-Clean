using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;

namespace WileyWidget.Tests.TestHelpers;

/// <summary>
/// Test implementation of IAsyncEnumerable for EF Core async mocking
/// Enables async enumeration of query results in unit tests
/// </summary>
public class TestAsyncEnumerable<T> : IAsyncEnumerable<T>, IQueryable<T>, IOrderedQueryable<T>
{
    private readonly IQueryable<T> _inner;

    public TestAsyncEnumerable(IQueryable<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new TestAsyncEnumerator<T>(_inner.GetEnumerator());
    }

    public Type ElementType => _inner.ElementType;
    public Expression Expression => _inner.Expression;
    public IQueryProvider Provider => _inner.Provider;

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return _inner.GetEnumerator();
    }

    System.Collections.Generic.IEnumerator<T> System.Collections.Generic.IEnumerable<T>.GetEnumerator()
    {
        return _inner.GetEnumerator();
    }
}