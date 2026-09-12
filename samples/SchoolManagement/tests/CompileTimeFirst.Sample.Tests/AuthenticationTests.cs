using System.Security.Claims;
using CompileTimeFirst.Sample.Web.Services;
using Microsoft.AspNetCore.Http;

namespace CompileTimeFirst.Sample.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public void Authenticated_tenant_claim_is_captured()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(SchoolClaimTypes.TenantId, tenantId.ToString("D"))],
                authenticationType: "Test"));
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var currentUser = new ClaimsCurrentUser(accessor);

        Assert.Equal(tenantId, currentUser.TenantId);
    }

    [Fact]
    public void Accessor_can_be_created_before_authentication_completes()
    {
        var tenantId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = context };
        var currentUser = new ClaimsCurrentUser(accessor);

        Assert.Null(currentUser.TenantId);

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(SchoolClaimTypes.TenantId, tenantId.ToString("D"))],
                authenticationType: "Test"));

        Assert.Equal(tenantId, currentUser.TenantId);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("not-a-guid", true)]
    [InlineData("11111111-1111-1111-1111-111111111111", false)]
    public void Missing_invalid_or_unauthenticated_claim_resolves_to_no_tenant(
        string? claimValue,
        bool authenticated)
    {
        var claims = claimValue is null
            ? Array.Empty<Claim>()
            : [new Claim(SchoolClaimTypes.TenantId, claimValue)];
        var identity = new ClaimsIdentity(claims, authenticated ? "Test" : null);
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var currentUser = new ClaimsCurrentUser(accessor);

        Assert.Null(currentUser.TenantId);
    }
}
