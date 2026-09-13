using System.Security.Claims;
using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Data;

public static class SchoolClaimTypes
{
    public const string TenantId = "tenant_id";

    public static Guid? ResolveTenantId(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var tenantClaims = principal.FindAll(TenantId).ToArray();
        return tenantClaims.Length == 1 && Guid.TryParse(tenantClaims[0].Value, out var tenantId)
            ? tenantId
            : null;
    }
}

/// <summary>
/// Stores the tenant established by the authenticated principal for this request or Blazor circuit.
/// Missing, malformed and unauthenticated principals deliberately resolve to no tenant.
/// </summary>
public sealed class ClaimsCurrentUser : ICurrentUser
{
    public Guid? TenantId { get; private set; }

    public void SetPrincipal(ClaimsPrincipal? principal) =>
        TenantId = SchoolClaimTypes.ResolveTenantId(principal);
}
