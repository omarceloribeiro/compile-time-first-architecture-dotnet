namespace CompileTimeFirst.Sample.Domain;

/// <summary>
/// The tenant the current operation runs under.
///
/// Deliberately tolerant: it returns null instead of throwing when no tenant is resolved. Code runs
/// outside a user context in more places than it first appears - database seeding, the dependency
/// injection build gate, background work and tests - and an accessor that throws there turns a
/// missing tenant into a crash at startup rather than a query that returns nothing.
/// </summary>
public interface ICurrentUser
{
    Guid? TenantId { get; }
}
