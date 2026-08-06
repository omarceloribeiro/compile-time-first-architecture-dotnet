using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.ConsoleApp;

public sealed class SimpleDbContextFactory<TContext>(Func<TContext> factory)
    : IDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext() => factory();

    public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(factory());
}
