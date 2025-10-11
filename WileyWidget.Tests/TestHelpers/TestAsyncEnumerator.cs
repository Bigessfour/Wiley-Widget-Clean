using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WileyWidget.Tests.TestHelpers;

/// <summary>
/// Test implementation of IAsyncEnumerator for EF Core async mocking
/// Provides async enumeration capabilities for unit tests
/// </summary>
public class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;

    public TestAsyncEnumerator(IEnumerator<T> inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public T Current => _inner.Current;

    public ValueTask<bool> MoveNextAsync()
    {
        return new ValueTask<bool>(_inner.MoveNext());
    }

    public ValueTask DisposeAsync()
    {
        _inner.Dispose();
        GC.SuppressFinalize(this);
        return new ValueTask();
    }
}