using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

/// <summary>
/// Creates contexts already scoped to the current tenant.
///
/// The tenant cannot be injected into the context itself: <c>AddDbContextFactory</c> registers the
/// factory as a singleton by default, so a scoped accessor could not be captured there without
/// violating scope validation. Registering these factories with a scoped lifetime moves the
/// accessor to the only place that can legally hold it, and leaves every use case and read still
/// asking for the public <c>IDbContextFactory&lt;T&gt;</c>.
/// </summary>
public sealed class TenantSchoolDbContextFactory(
    DbContextOptions<SchoolDbContext> options,
    ICurrentUser currentUser)
    : IDbContextFactory<SchoolDbContext>
{
    public SchoolDbContext CreateDbContext() =>
        new(options) { TenantId = currentUser.TenantId };
}

public sealed class TenantReadOnlySchoolDbContextFactory(
    DbContextOptions<ReadOnlySchoolDbContext> options,
    ICurrentUser currentUser)
    : IDbContextFactory<ReadOnlySchoolDbContext>
{
    public ReadOnlySchoolDbContext CreateDbContext() =>
        new(options) { TenantId = currentUser.TenantId };
}
