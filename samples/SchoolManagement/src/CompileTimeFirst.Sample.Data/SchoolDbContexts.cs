using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Data;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options)
    : DbContext(options), ITenantScope
{
    /// <summary>Assigned by the factory that creates this context, once per operation.</summary>
    public Guid? TenantId { get; set; }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionOption> QuestionOptions => Set<QuestionOption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        DomainModelConfiguration.Configure(modelBuilder, this);
    }
}
