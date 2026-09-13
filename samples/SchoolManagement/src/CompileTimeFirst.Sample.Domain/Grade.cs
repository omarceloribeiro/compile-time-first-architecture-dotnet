namespace CompileTimeFirst.Sample.Domain;

public sealed class Grade
{
    // Parameterless constructor for EF Core materialization only.
    private Grade()
    {
    }

    public Grade(Guid id, Guid tenantId, string name, int order)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Order = order;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = name;

    public void Reorder(int order) => Order = order;

    public void Deactivate() => IsActive = false;
}
