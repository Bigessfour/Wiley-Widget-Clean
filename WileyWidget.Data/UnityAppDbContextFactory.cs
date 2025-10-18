#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WileyWidget.Data
{
    /// <summary>
    /// Unity-friendly factory for AppDbContext. Unity can construct this when a
    /// DbContextOptions<AppDbContext> instance is registered in the container.
    /// </summary>
    public sealed class UnityAppDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public UnityAppDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options ?? throw new System.ArgumentNullException(nameof(options));
        }

        public AppDbContext CreateDbContext()
        {
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new AppDbContext(_options);
#pragma warning restore CA2000
        }

        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            // Construction here is synchronous; wrap in ValueTask for compatibility
#pragma warning disable CA2000 // Factory method - caller is responsible for disposal
            return new ValueTask<AppDbContext>(new AppDbContext(_options));
#pragma warning restore CA2000
        }
    }
}
