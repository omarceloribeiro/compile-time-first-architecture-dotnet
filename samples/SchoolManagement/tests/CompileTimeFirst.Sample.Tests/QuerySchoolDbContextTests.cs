using System.Text;
using CompileTimeFirst.Sample.Application.Dashboard;
using CompileTimeFirst.Sample.Application.Exports;
using CompileTimeFirst.Sample.Data;
using CompileTimeFirst.Sample.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class QuerySchoolDbContextTests
{
    [Fact]
    public async Task Original_schema_including_identity_is_inherited()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var write = await database.WriteFactory.CreateDbContextAsync();
        await using var query = await database.QueryFactory.CreateDbContextAsync();
        Assert.Equal(write.Database.GenerateCreateScript(), query.Database.GenerateCreateScript());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task All_save_overloads_throw_through_every_base_reference(bool trackedChanges)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var query = await database.QueryFactory.CreateDbContextAsync();
        SchoolDbContext originalReference = query;
        DbContext baseReference = query;
        if (trackedChanges)
        {
            query.Subjects.Add(new Subject(Guid.NewGuid(), TestDatabase.TenantA, "Must not persist"));
        }

        Assert.Throws<InvalidOperationException>(() => query.SaveChanges());
        Assert.Throws<InvalidOperationException>(() => query.SaveChanges(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => query.SaveChangesAsync(false));
        Assert.Throws<InvalidOperationException>(() => originalReference.SaveChanges());
        Assert.Throws<InvalidOperationException>(() => originalReference.SaveChanges(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => originalReference.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => originalReference.SaveChangesAsync(false));
        Assert.Throws<InvalidOperationException>(() => baseReference.SaveChanges());
        Assert.Throws<InvalidOperationException>(() => baseReference.SaveChanges(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => baseReference.SaveChangesAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => baseReference.SaveChangesAsync(false));

        await using var verify = await database.WriteFactory.CreateDbContextAsync();
        Assert.Empty(await verify.Subjects.ToListAsync());
        verify.Subjects.Add(new Subject(Guid.NewGuid(), TestDatabase.TenantA, "Write still works"));
        Assert.Equal(1, await verify.SaveChangesAsync());
    }

    [Fact]
    public async Task Tenant_filter_survives_model_caching_and_queries_do_not_track()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedAsync(database, TestDatabase.TenantA, "Tenant A");
        await SeedAsync(database, TestDatabase.TenantB, "Tenant B");

        await using var first = await database.QueryFactory.CreateDbContextAsync();
        Assert.Equal("Tenant A", (await first.Subjects.SingleAsync()).Name);
        Assert.Empty(first.ChangeTracker.Entries());
        Assert.Equal(QueryTrackingBehavior.NoTracking, first.ChangeTracker.QueryTrackingBehavior);

        database.CurrentUser.TenantId = TestDatabase.TenantB;
        await using var second = await database.QueryFactory.CreateDbContextAsync();
        Assert.Equal("Tenant B", (await second.Subjects.SingleAsync()).Name);
        Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());
        // Existing operations retain their tenant even when a later factory call resolves another.
        Assert.Equal("Tenant A", (await first.Subjects.SingleAsync()).Name);

        database.CurrentUser.TenantId = null;
        await using var unresolved = await database.QueryFactory.CreateDbContextAsync();
        Assert.Empty(await unresolved.Subjects.ToListAsync());
    }

    [Fact]
    public async Task Business_reads_return_typed_results_from_entities_and_dispose_contexts()
    {
        await using var database = await TestDatabase.CreateAsync();
        await SeedAsync(database, TestDatabase.TenantA, "Own");
        await SeedAsync(database, TestDatabase.TenantB, "Other");
        var factory = new CapturingFactory(database.QueryFactory);

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

    private sealed class CapturingFactory(IDbContextFactory<QuerySchoolDbContext> inner)
        : IDbContextFactory<QuerySchoolDbContext>
    {
        public QuerySchoolDbContext? Last { get; private set; }
        public QuerySchoolDbContext CreateDbContext() => Last = inner.CreateDbContext();
    }
}
