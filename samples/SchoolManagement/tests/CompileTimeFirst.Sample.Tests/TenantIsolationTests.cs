using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class TenantIsolationTests
{
    /// <summary>
    /// The filter compares against a property of the executing context, not a value captured when
    /// the model was first built. EF caches the model per context type, so a captured value would
    /// freeze the first tenant that ever queried and serve its rows to everyone afterwards - the
    /// worst possible failure mode, because it only appears once a second tenant exists.
    /// </summary>
    [Fact]
    public async Task Model_cache_does_not_freeze_the_first_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedSubjectAsync(database, TestDatabase.TenantA, "Computing");
        await SeedSubjectAsync(database, TestDatabase.TenantB, "Geography");

        // Tenant A queries first, which is what builds and caches the model.
        await using (var contextA = database.CreateContextFor(TestDatabase.TenantA))
        {
            Assert.Equal(["Computing"], await contextA.Subjects.Select(x => x.Name).ToListAsync());
        }

        await using var contextB = database.CreateContextFor(TestDatabase.TenantB);
        Assert.Equal(["Geography"], await contextB.Subjects.Select(x => x.Name).ToListAsync());
    }

    /// <summary>
    /// The headline guarantee: a query with no tenant predicate of its own still cannot read
    /// another tenant. This is what makes the hand-written filter unnecessary, and therefore
    /// forbidden.
    /// </summary>
    [Fact]
    public async Task Query_without_a_tenant_predicate_cannot_see_another_tenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedSubjectAsync(database, TestDatabase.TenantA, "Computing");
        await SeedSubjectAsync(database, TestDatabase.TenantB, "Geography");

        await using var scope = await database.ReadFactory.CreateDbContextAsync();

        var names = await scope.Subjects.Select(x => x.Name).ToListAsync();

        Assert.Equal(["Computing"], names);
    }

    /// <summary>
    /// An unresolved tenant denies everything rather than revealing everything. This falls out of
    /// comparing Guid to Guid?; the shape "TenantId == null || ..." would have done the opposite.
    /// </summary>
    [Fact]
    public async Task Unresolved_tenant_reads_nothing()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedSubjectAsync(database, TestDatabase.TenantA, "Computing");

        database.CurrentUser.TenantId = null;
        await using var scope = await database.ReadFactory.CreateDbContextAsync();

        Assert.Empty(await scope.Subjects.ToListAsync());
    }

    /// <summary>
    /// Lifting the named filter is the authorized escape hatch, and it lifts only that filter.
    /// </summary>
    [Fact]
    public async Task Named_filter_can_be_lifted_deliberately()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedSubjectAsync(database, TestDatabase.TenantA, "Computing");
        await SeedSubjectAsync(database, TestDatabase.TenantB, "Geography");

        await using var scope = await database.ReadFactory.CreateDbContextAsync();

        var names = await scope.Subjects
            .IgnoreQueryFilters([DomainModelConfiguration.TenantFilter])
            .Select(x => x.Name)
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Equal(["Computing", "Geography"], names);
    }

    /// <summary>
    /// The filter protects reads. Writes are protected by composite foreign keys carrying TenantId,
    /// which make a cross-tenant reference unrepresentable rather than merely discouraged.
    /// </summary>
    [Fact]
    public async Task Cross_tenant_reference_is_rejected_by_the_database()
    {
        await using var database = await TestDatabase.CreateAsync();
        var subjectId = await SeedSubjectAsync(database, TestDatabase.TenantA, "Computing");
        var gradeId = Guid.NewGuid();

        await using (var seed = database.CreateContextFor(TestDatabase.TenantB))
        {
            seed.Grades.Add(new Grade(gradeId, TestDatabase.TenantB, "Grade 9", 9));
            await seed.SaveChangesAsync();
        }

        // A question in tenant B pointing at a subject owned by tenant A.
        await using var db = database.CreateContextFor(TestDatabase.TenantB);
        db.Questions.Add(new Question(
            Guid.NewGuid(),
            TestDatabase.TenantB,
            subjectId,
            gradeId,
            "Whose subject is this?",
            QuestionType.OpenText,
            DateTimeOffset.UnixEpoch));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static async Task<Guid> SeedSubjectAsync(TestDatabase database, Guid tenantId, string name)
    {
        var id = Guid.NewGuid();
        await using var db = database.CreateContextFor(tenantId);
        db.Subjects.Add(new Subject(id, tenantId, name));
        await db.SaveChangesAsync();
        return id;
    }
}
