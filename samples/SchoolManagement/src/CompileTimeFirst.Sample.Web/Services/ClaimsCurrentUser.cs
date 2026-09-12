using System.Security.Claims;
using CompileTimeFirst.Sample.Domain;

namespace CompileTimeFirst.Sample.Web.Services;

public static class SchoolClaimTypes
{
    public const string TenantId = "tenant_id";
}

/// <summary>
/// Captures the tenant established by the authenticated cookie for this request or Blazor circuit.
/// Missing, malformed and unauthenticated principals deliberately resolve to no tenant.
/// </summary>
public sealed class ClaimsCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private Guid? _tenantId;

    public ClaimsCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            if (_tenantId.HasValue)
            {
                return _tenantId;
            }

            var principal = _httpContextAccessor.HttpContext?.User;
            var value = principal?.Identity?.IsAuthenticated == true
                ? principal.FindFirst(SchoolClaimTypes.TenantId)?.Value
                : null;

            if (Guid.TryParse(value, out var tenantId))
            {
                _tenantId = tenantId;
            }

            return _tenantId;
        }
    }
}
