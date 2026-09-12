using Microsoft.AspNetCore.Identity;

namespace CompileTimeFirst.Sample.Data;

/// <summary>The authenticated account and its single tenant boundary.</summary>
public sealed class SchoolUser : IdentityUser<Guid>
{
    private SchoolUser()
    {
    }

    public SchoolUser(Guid id, string userName, Guid tenantId)
    {
        Id = id;
        UserName = userName;
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }
}
