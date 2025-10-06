using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WileyWidget.Data;
using WileyWidget.Services;
using WileyWidget.Services.Threading;
using Xunit;

namespace WileyWidget.LifecycleTests;

public abstract class LifecycleTestBase : IAsyncLifetime
{
    private SqliteAppDbContextFactory? _factory;

    protected IDbContextFactory<AppDbContext> DbContextFactory => _factory ?? throw new InvalidOperationException("Factory not initialized");

    protected IDispatcherHelper CreateDispatcherHelper() => new TestDispatcherHelper();

    protected static NullLogger<T> CreateLogger<T>() where T : class => NullLogger<T>.Instance;

    public async Task InitializeAsync()
    {
        _factory = await SqliteAppDbContextFactory.CreateAsync().ConfigureAwait(false);
        await SeedBaselineDataAsync().ConfigureAwait(false);

        ErrorReportingService.Instance.SuppressUserDialogs = true;
    }

    public async Task DisposeAsync()
    {
        ErrorReportingService.Instance.SuppressUserDialogs = false;

        if (_factory != null)
        {
            await _factory.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected virtual Task SeedBaselineDataAsync()
    {
        return Task.CompletedTask;
    }

    protected async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        await action(context).ConfigureAwait(false);
    }

    protected async Task<T> WithDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var context = await DbContextFactory.CreateDbContextAsync().ConfigureAwait(false);
        return await action(context).ConfigureAwait(false);
    }

    protected Task RunOnDispatcherAsync(Func<Task> action) => StaTestHarness.RunAsync(action);
}
