using System.Text;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class BusinessReadUseCaseTests
{
    [Fact]
    public async Task Original_factory_preserves_tenants_with_entity_queries_without_tracking()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedAsync(database, TestDatabase.TenantA, "Tenant A");
        await SeedAsync(database, TestDatabase.TenantB, "Tenant B");

        await using var first = await database.WriteFactory.CreateDbContextAsync();
        Assert.Equal("Tenant A", (await first.Subjects.AsNoTracking().SingleAsync()).Name);
        Assert.Empty(first.ChangeTracker.Entries());
        // The shared factory still supports write operations; reads opt out per query.
        Assert.Equal(QueryTrackingBehavior.TrackAll, first.ChangeTracker.QueryTrackingBehavior);

        database.CurrentUser.TenantId = TestDatabase.TenantB;
        await using var second = await database.WriteFactory.CreateDbContextAsync();
        Assert.Equal("Tenant B", (await second.Subjects.AsNoTracking().SingleAsync()).Name);
        Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());
        // Existing operations retain their tenant even when a later factory call resolves another.
        Assert.Equal("Tenant A", (await first.Subjects.AsNoTracking().SingleAsync()).Name);

        database.CurrentUser.TenantId = null;
        await using var unresolved = await database.WriteFactory.CreateDbContextAsync();
        Assert.Empty(await unresolved.Subjects.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Business_reads_return_typed_results_from_entities_and_dispose_contexts()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedAsync(database, TestDatabase.TenantA, "Own");
        await SeedAsync(database, TestDatabase.TenantB, "Other");
        var factory = new CapturingFactory(database.WriteFactory);

        var dashboard = await new GetSchoolDashboardUseCase(factory).ExecuteAsync(new());
        Assert.Equal(new GetSchoolDashboardResult(1, 1, 1), dashboard);
        AssertDisposed(factory);

        var export = new ExportQuestionsUseCase(factory);
        foreach (var format in new[] { ExportFormat.Csv, ExportFormat.Json })
        {
            var result = await export.ExecuteAsync(new(format));
            var content = Encoding.UTF8.GetString(result.Content);
            Assert.Contains("Own question", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Other question", content, StringComparison.Ordinal);
            AssertDisposed(factory);
        }

        await Assert.ThrowsAsync<NotSupportedException>(() => export.ExecuteAsync(new((ExportFormat)99)));
        AssertDisposed(factory);
    }

    private static void AssertDisposed(CapturingFactory factory) =>
        Assert.Throws<ObjectDisposedException>(() => factory.Last!.Subjects.ToList());

    private static async Task SeedAsync(TestDatabase database, Guid tenantId, string label)
    {
        await using var write = database.CreateContextFor(tenantId);
        var subject = new Subject(Guid.NewGuid(), tenantId, label);
        var grade = new Grade(Guid.NewGuid(), tenantId, label, 1);
        write.Subjects.Add(subject);
        write.Grades.Add(grade);
        write.Questions.Add(new Question(Guid.NewGuid(), tenantId, subject.Id, grade.Id,
            label + " question", QuestionType.OpenText, DateTimeOffset.UnixEpoch));
        await write.SaveChangesAsync();
    }

    private sealed class CapturingFactory(IDbContextFactory<SchoolDbContext> inner)
        : IDbContextFactory<SchoolDbContext>
    {
        public SchoolDbContext? Last { get; private set; }
        public SchoolDbContext CreateDbContext() => Last = inner.CreateDbContext();
    }
}
