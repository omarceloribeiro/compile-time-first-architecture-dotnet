using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

/// <summary>
/// The original entity model for business reads. SaveChanges is disabled; this is not a
/// database permission boundary. The read-use-case analyzer also rejects direct bulk/SQL writes.
/// </summary>
public sealed class QuerySchoolDbContext(DbContextOptions<QuerySchoolDbContext> options)
    : SchoolDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    public override int SaveChanges() => throw PersistenceDisabled();
    public override int SaveChanges(bool acceptAllChangesOnSuccess) => throw PersistenceDisabled();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => throw PersistenceDisabled();
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        => throw PersistenceDisabled();

    private static InvalidOperationException PersistenceDisabled() =>
        new("Business read contexts do not allow SaveChanges. Use a write use case to persist changes.");
}
