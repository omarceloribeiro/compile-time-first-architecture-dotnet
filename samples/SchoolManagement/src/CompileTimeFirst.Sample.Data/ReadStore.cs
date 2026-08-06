using CompileTimeFirst.Sample.ReadModel;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

public sealed class ReadSchoolDbFactory(
    IDbContextFactory<ReadOnlySchoolDbContext> factory)
    : IReadSchoolDbFactory, IReadProviderInfo
{
    public string Name => "EF Core (Interactive Server)";

    public async Task<IReadSchoolDbScope> CreateAsync(
        CancellationToken cancellationToken = default)
        => await factory.CreateDbContextAsync(cancellationToken);
}

public sealed class EfReadQueryExecutor : IReadQueryExecutor
{
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.ToListAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.FirstOrDefaultAsync(cancellationToken);

    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.CountAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.AnyAsync(cancellationToken);
}
