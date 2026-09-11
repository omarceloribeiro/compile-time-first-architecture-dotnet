namespace CompileTimeFirst.Sample.Domain;

/// <summary>
/// The isolation boundary every other entity belongs to.
///
/// Deliberately not itself filtered: a tenant selector has to list tenants before a tenant exists.
/// In production the tenant comes from a claim resolved on the server and is never chosen by the
/// client - the sample's selector stands in for authentication, not for the trust model.
/// </summary>
public sealed class Tenant
{
    private Tenant()
    {
    }

    public Tenant(Guid id, string name)
    {
        Id = id;
        Name = name;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}
