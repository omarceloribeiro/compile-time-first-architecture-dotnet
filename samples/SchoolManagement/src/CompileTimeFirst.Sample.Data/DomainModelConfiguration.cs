using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CompileTimeFirst.Sample.Data;

/// <summary>
/// The tenant a context instance reads under. Null means no tenant is resolved.
/// </summary>
public interface ITenantScope
{
    Guid? TenantId { get; }
}

/// <summary>
/// One model configuration shared by the write and read contexts.
///
/// Both contexts map the same entities, so configuring them separately lets the two models drift
/// apart silently - a relationship configured on one side and missing on the other still compiles
/// and still passes tests that only exercise one context.
/// </summary>
public static class DomainModelConfiguration
{
    /// <summary>
    /// Named so a single filter can be lifted deliberately with
    /// <c>IgnoreQueryFilters([DomainModelConfiguration.TenantFilter])</c> instead of disabling every
    /// filter the model may ever carry.
    /// </summary>
    public const string TenantFilter = "Tenant";

    public static void Configure(ModelBuilder modelBuilder, ITenantScope tenantScope)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(tenantScope);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            // Deliberately unfiltered - see the Tenant entity.
        });

        modelBuilder.Entity<Subject>(entity =>
        {
            TenantOwned(entity, "Subjects");
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.HasQueryFilter(TenantFilter, x => x.TenantId == tenantScope.TenantId);
        });

        modelBuilder.Entity<Grade>(entity =>
        {
            TenantOwned(entity, "Grades");
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasQueryFilter(TenantFilter, x => x.TenantId == tenantScope.TenantId);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            TenantOwned(entity, "Questions");
            entity.Property(x => x.Statement).HasMaxLength(4_000).IsRequired();
            entity.HasQueryFilter(TenantFilter, x => x.TenantId == tenantScope.TenantId);

            // Composite foreign keys carrying TenantId make a cross-tenant reference
            // unrepresentable in the database, rather than merely forbidden by a rule someone has
            // to remember. The query filter protects reads; these protect writes.
            entity.HasOne<Subject>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.SubjectId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Grade>()
                .WithMany()
                .HasForeignKey(x => new { x.TenantId, x.GradeId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Options)
                .WithOne()
                .HasForeignKey(x => new { x.TenantId, x.QuestionId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id })
                .OnDelete(DeleteBehavior.Cascade);

            // Options is exposed as IReadOnlyList over a private list, so EF reads and writes the
            // backing field instead of the read-only property.
            entity.Metadata
                .FindNavigation(nameof(Question.Options))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<QuestionOption>(entity =>
        {
            TenantOwned(entity, "QuestionOptions");
            entity.Property(x => x.Text).HasMaxLength(1_000).IsRequired();
            entity.HasQueryFilter(TenantFilter, x => x.TenantId == tenantScope.TenantId);
        });
    }

    /// <summary>
    /// The key shape every tenant-owned entity shares: a global identity, a tenant-scoped alternate
    /// key that composite foreign keys point at, and an index on the column every query filters by.
    /// </summary>
    private static void TenantOwned<TEntity>(EntityTypeBuilder<TEntity> entity, string tableName)
        where TEntity : class
    {
        // Named explicitly. Table naming otherwise follows the DbSet property, which the read
        // context does not declare - the two contexts would then map the same entity to different
        // tables while every other part of the model matched.
        entity.ToTable(tableName);
        entity.HasKey("Id");
        entity.HasAlternateKey("TenantId", "Id");
        entity.HasIndex("TenantId");

        entity.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey("TenantId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
