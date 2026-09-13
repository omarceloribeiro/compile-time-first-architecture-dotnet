using System.Security.Claims;
using CompileTimeFirst.Sample.BlazorServer.Services;
using CompileTimeFirst.Sample.Data;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompileTimeFirst.Sample.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public void Authenticated_tenant_claim_is_captured()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new ClaimsCurrentUser();

        currentUser.SetPrincipal(Principal(tenantId.ToString("D")));

        Assert.Equal(tenantId, currentUser.TenantId);
    }

    [Fact]
    public void Current_user_can_be_resolved_before_authentication_completes()
    {
        var tenantId = Guid.NewGuid();
        var currentUser = new ClaimsCurrentUser();

        Assert.Null(currentUser.TenantId);

        currentUser.SetPrincipal(Principal(tenantId.ToString("D")));

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
        var currentUser = new ClaimsCurrentUser();

        currentUser.SetPrincipal(Principal(claimValue, authenticated));

        Assert.Null(currentUser.TenantId);
    }

    [Fact]
    public void Duplicate_tenant_claims_resolve_to_no_tenant()
    {
        var currentUser = new ClaimsCurrentUser();
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(SchoolClaimTypes.TenantId, Guid.NewGuid().ToString("D")),
                    new Claim(SchoolClaimTypes.TenantId, Guid.NewGuid().ToString("D"))
                ],
                authenticationType: "Test"));

        currentUser.SetPrincipal(principal);

        Assert.Null(currentUser.TenantId);
    }

    [Fact]
    public async Task Circuit_handler_captures_late_authentication_and_refreshes_on_reconnection()
    {
        var firstTenantId = Guid.NewGuid();
        var secondTenantId = Guid.NewGuid();
        var currentUser = new ClaimsCurrentUser();
        var authenticationStateProvider = new TestAuthenticationStateProvider(Principal(firstTenantId.ToString("D")));
        using var handler = new CurrentUserCircuitHandler(
            authenticationStateProvider,
            currentUser,
            NullLogger<CurrentUserCircuitHandler>.Instance);

        Assert.Null(currentUser.TenantId);

        await handler.OnCircuitOpenedAsync(null!, CancellationToken.None);
        Assert.Equal(firstTenantId, currentUser.TenantId);

        authenticationStateProvider.SetPrincipal(Principal(secondTenantId.ToString("D")));
        await handler.OnConnectionUpAsync(null!, CancellationToken.None);
        Assert.Equal(secondTenantId, currentUser.TenantId);
    }

    private static ClaimsPrincipal Principal(string? tenantClaim, bool authenticated = true)
    {
        var claims = tenantClaim is null
            ? Array.Empty<Claim>()
            : [new Claim(SchoolClaimTypes.TenantId, tenantClaim)];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticated ? "Test" : null));
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal principal)
        : AuthenticationStateProvider
    {
        private ClaimsPrincipal _principal = principal;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_principal));

        public void SetPrincipal(ClaimsPrincipal principal) =>
            _principal = principal;
    }
}
