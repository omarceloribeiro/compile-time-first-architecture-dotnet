using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Web.Services;

/// <summary>
/// The tenant selected for the current Blazor circuit.
///
/// This stands in for authentication so the sample can demonstrate isolation without shipping an
/// identity provider. **It is not the production shape.** In a real application the tenant is
/// resolved on the server from a claim and is never chosen by the client: letting the browser name
/// its own tenant would make isolation a request parameter.
///
/// It never throws when nothing is selected. A null tenant makes every filtered query return
/// nothing, which is the safe direction; throwing instead would break seeding, the dependency
/// injection build gate and any code path that legitimately runs without a user.
/// </summary>
public sealed class CurrentTenantSelection : ICurrentUser
{
    public Guid? TenantId { get; private set; }

    public void Select(Guid? tenantId) => TenantId = tenantId;
}
