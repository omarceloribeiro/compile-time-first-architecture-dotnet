namespace CompileTimeFirst.Sample.Domain;

/// <summary>
/// The isolation boundary every other entity belongs to.
///
/// Deliberately not itself filtered: Identity must resolve an account before the account's tenant
/// exists in the current operation. The server emits that tenant as a claim; the client never
/// chooses the isolation boundary.
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
