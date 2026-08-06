using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

internal sealed class TestDbContextFactory<TContext>(Func<TContext> factory)
    : IDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext() => factory();

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(factory());
    }
}
