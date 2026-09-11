namespace CompileTimeFirst.Sample.Domain;

public sealed class Subject
{
    // Parameterless constructor for EF Core materialization only.
    private Subject()
    {
    }

    public Subject(Guid id, Guid tenantId, string name)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = name;

    public void Deactivate() => IsActive = false;
}
