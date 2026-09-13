using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CompileTimeFirst.Sample.Data;

/// <summary>Projects the account's persisted tenant boundary into its authenticated cookie.</summary>
public sealed class TenantUserClaimsPrincipalFactory(
    UserManager<SchoolUser> userManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<SchoolUser>(userManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(SchoolUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var identity = await base.GenerateClaimsAsync(user);
        foreach (var claim in identity.FindAll(SchoolClaimTypes.TenantId).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(SchoolClaimTypes.TenantId, user.TenantId.ToString("D")));
        return identity;
    }
}
