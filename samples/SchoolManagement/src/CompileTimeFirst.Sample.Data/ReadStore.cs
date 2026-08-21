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

    public async Task<PageResult<T>> ToPageAsync<T>(
        IQueryable<T> query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new PageResult<T>(items, totalCount);
    }

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.FirstOrDefaultAsync(cancellationToken);

    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.CountAsync(cancellationToken);

    public Task<bool> AnyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.AnyAsync(cancellationToken);
}
