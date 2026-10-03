using System.Collections.Immutable;
using CompileTimeFirst.Sample.Analyzers;
using CompileTimeFirst.Sample.Application;
using CompileTimeFirst.Sample.Data;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ReadUseCaseAnalyzerTests
{
    [Theory]
    [InlineData("public class Case : IUseCase { }", true)]
    [InlineData("public class Case : IReadUseCase, IWriteUseCase { }", true)]
    [InlineData("public abstract class Case : IUseCase { }", false)]
    [InlineData("public class Case : IReadUseCase { }", false)]
    [InlineData("public class Case : IWriteUseCase { }", false)]
    [InlineData("public interface IReport : IReadUseCase { } public class Case : IReport { }", false)]
    [InlineData("public abstract class Base : IUseCase { } public class Case : Base { }", true)]
    [InlineData("public abstract class Base : IReadUseCase { } public class Case : Base { }", false)]
    [InlineData("public struct Case : IUseCase { }", true)]
    public async Task Concrete_use_cases_require_exactly_one_classification(string declaration, bool rejected)
    {
        var diagnostics = await AnalyzeAsync(declaration);
        Assert.Equal(rejected, diagnostics.Any(d => d.Id == ReadUseCaseAnalyzer.ClassificationId));
    }

    [Theory]
    [InlineData("db.SaveChanges();")]
    [InlineData("db.SaveChanges(false);")]
    [InlineData("_ = db.SaveChangesAsync();")]
    [InlineData("_ = db.SaveChangesAsync(false, default);")]
    [InlineData("((DbContext)db).SaveChanges();")]
    [InlineData("((SchoolDbContext)db).SaveChanges();")]
    [InlineData("db.Subjects.ExecuteDelete();")]
    [InlineData("_ = db.Subjects.ExecuteDeleteAsync();")]
    [InlineData("db.Subjects.ExecuteUpdate(s => s.SetProperty(x => x.Name, \"Changed\"));")]
    [InlineData("_ = db.Subjects.ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, \"Changed\"));")]
    [InlineData("db.Database.ExecuteSql($\"DELETE FROM Subjects\");")]
    [InlineData("_ = db.Database.ExecuteSqlAsync($\"DELETE FROM Subjects\");")]
    [InlineData("db.Database.ExecuteSqlRaw(\"DELETE FROM Subjects\");")]
    [InlineData("_ = db.Database.ExecuteSqlRawAsync(\"DELETE FROM Subjects\");")]
    [InlineData("db.Database.ExecuteSqlInterpolated($\"DELETE FROM Subjects\");")]
    [InlineData("_ = db.Database.ExecuteSqlInterpolatedAsync($\"DELETE FROM Subjects\");")]
    [InlineData("Func<int> save = db.SaveChanges;")]
    [InlineData("Func<int> save = () => db.SaveChanges();")]
    [InlineData("int Save() => db.SaveChanges(); Save();")]
    [InlineData("Action save = () => { db.Database.ExecuteSqlRaw(\"DELETE FROM Subjects\"); };")]
    [InlineData("Func<IQueryable<CompileTimeFirst.Sample.Domain.Subject>, int> delete = EntityFrameworkQueryableExtensions.ExecuteDelete;")]
    public async Task Read_use_case_rejects_ef_writes_and_method_references(string statement)
    {
        var diagnostics = await AnalyzeAsync($$"""
            public sealed class Report : IReadUseCase
            {
                private void Helper(SchoolDbContext db) { {{statement}} }
            }
            """);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ReadUseCaseAnalyzer.PersistenceId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task Inherited_classification_and_nested_helpers_are_in_scope()
    {
        var diagnostics = await AnalyzeAsync("""
            public interface IReport : IReadUseCase { }
            public abstract class ReportBase : IReport
            {
                protected void Save(DbContext db) => db.SaveChanges();
            }
            public sealed class Report : ReportBase
            {
                private sealed class Helper
                {
                    public void Save(SchoolDbContext db) => db.SaveChanges();
                }
            }
            """);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal(ReadUseCaseAnalyzer.PersistenceId, d.Id));
    }

    [Fact]
    public async Task Save_override_is_still_recognized_as_ef_persistence()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class CustomContext(DbContextOptions options) : DbContext(options)
            {
                public override int SaveChanges() => base.SaveChanges();
            }
            public sealed class Read : IReadUseCase
            {
                public void Run(CustomContext db) => db.SaveChanges();
            }
            """);
        Assert.Equal(ReadUseCaseAnalyzer.PersistenceId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Writes_queries_and_unrelated_homonyms_are_allowed()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Write : IWriteUseCase
            {
                public void Run(SchoolDbContext db)
                {
                    db.SaveChanges();
                    db.Subjects.ExecuteDelete();
                }
            }
            public sealed class Other
            {
                public void SaveChanges() { }
                public void ExecuteDelete() { }
            }
            public sealed class Read : IReadUseCase
            {
                public async Task Run(SchoolDbContext db)
                {
                    _ = await db.Subjects.AsNoTracking().ToListAsync();
                    _ = await db.Subjects.AsNoTracking().CountAsync();
                    _ = await db.Subjects.AsNoTracking().Select(x => x.Name).ToListAsync();
                    new Other().SaveChanges();
                    new Other().ExecuteDelete();
                }
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("_ = db.Subjects.ToList();")]
    [InlineData("_ = db.Subjects.ToListAsync();")]
    [InlineData("_ = db.Subjects.CountAsync();")]
    [InlineData("_ = db.Subjects.Any();")]
    [InlineData("_ = db.Subjects.Select(x => x.Name).ToList();")]
    [InlineData("_ = db.Subjects.Where(x => x.Name != null).AsNoTracking().ToList();")]
    [InlineData("_ = db.Subjects.AsQueryable().AsNoTracking().Count();")]
    [InlineData("var query = db.Subjects; _ = query.AsNoTracking().Count();")]
    [InlineData("_ = db.Set<Subject>().Count();")]
    [InlineData("_ = db.Set<Subject>(\"subjects\").Count();")]
    [InlineData("_ = Queryable.Count(db.Subjects);")]
    [InlineData("_ = from subject in db.Subjects select subject.Name;")]
    [InlineData("_ = db.Subjects.FromSqlRaw(\"SELECT * FROM Subjects\").ToList();")]
    [InlineData("_ = db.Subjects.FromSqlInterpolated($\"SELECT * FROM Subjects\").Count();")]
    [InlineData("_ = db.Subjects.FromSql($\"SELECT * FROM Subjects\").Count();")]
    [InlineData("_ = db.Subjects.AsNoTracking().AsTracking().Count();")]
    [InlineData("_ = db.Subjects.AsTracking().Count();")]
    [InlineData("var query = db.Subjects.AsNoTracking(); _ = query.AsTracking().Any();")]
    [InlineData("Func<IQueryable<Subject>, IQueryable<Subject>> track = EntityFrameworkQueryableExtensions.AsTracking;")]
    [InlineData("_ = db.Subjects.Find(Guid.Empty);")]
    [InlineData("db.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking; _ = db.Subjects.Count();")]
    public async Task Read_queries_require_an_immediate_modifier_even_for_scalars(string statement)
    {
        var diagnostics = await AnalyzeAsync($$"""
            public sealed class Report : IReadUseCase
            {
                public void Run(SchoolDbContext db) { {{statement}} }
            }
            """);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ReadUseCaseAnalyzer.NoTrackingId, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Theory]
    [InlineData("_ = db.Subjects.AsNoTracking().ToList();")]
    [InlineData("_ = db.Subjects.AsNoTracking().CountAsync();")]
    [InlineData("_ = db.Subjects.AsNoTracking().Any();")]
    [InlineData("_ = db.Subjects.AsNoTracking().Select(x => x.Name).ToListAsync();")]
    [InlineData("_ = db.Subjects.AsNoTrackingWithIdentityResolution().ToListAsync();")]
    [InlineData("_ = db.Set<Subject>().AsNoTracking().Count();")]
    [InlineData("_ = db.Set<Subject>(\"subjects\").AsNoTracking().Count();")]
    [InlineData("_ = EntityFrameworkQueryableExtensions.AsNoTracking(source: db.Subjects).Count();")]
    [InlineData("_ = Queryable.Count(db.Subjects.AsNoTracking());")]
    [InlineData("_ = ((IQueryable<Subject>)(db.Subjects)).AsNoTracking().Count();")]
    [InlineData("_ = db.Subjects.FromSqlRaw(\"SELECT * FROM Subjects\").AsNoTracking().ToList();")]
    [InlineData("_ = db.Set<Subject>().FromSqlInterpolated($\"SELECT * FROM Subjects\").AsNoTracking().Count();")]
    [InlineData("_ = db.Subjects.FromSql($\"SELECT * FROM Subjects\").AsNoTrackingWithIdentityResolution().Count();")]
    [InlineData("var query = db.Subjects.AsNoTracking(); _ = query.Count(); _ = query.Select(x => x.Name).ToList();")]
    [InlineData("_ = from subject in db.Subjects.AsNoTracking() select subject.Name;")]
    [InlineData("_ = nameof(db.Subjects); _ = db.Subjects.EntityType;")]
    public async Task Explicit_no_tracking_sources_and_metadata_access_are_allowed(string statement)
    {
        var diagnostics = await AnalyzeAsync($$"""
            public sealed class Report : IReadUseCase
            {
                public void Run(SchoolDbContext db) { {{statement}} }
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Each_query_source_needs_its_own_modifier()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Report : IReadUseCase
            {
                public void Run(SchoolDbContext db)
                {
                    _ = db.Subjects.AsNoTracking().Concat(db.Subjects).ToList();
                }
            }
            """);
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(ReadUseCaseAnalyzer.NoTrackingId, diagnostic.Id);
        Assert.Equal("db.Subjects", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task Tracking_checks_cover_inherited_read_markers_and_nested_helpers_in_generated_code()
    {
        var diagnostics = await AnalyzeAsync("""
            public interface IReport : IReadUseCase { }
            public abstract class ReportBase : IReport
            {
                protected int Count(SchoolDbContext db) => db.Subjects.Count();
            }
            public sealed class Report : ReportBase
            {
                private sealed class Helper
                {
                    public int Count(SchoolDbContext db) => db.Subjects.Count();
                }
            }
            """, path: "Report.g.cs");
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(ReadUseCaseAnalyzer.NoTrackingId, diagnostic.Id));
    }

    [Fact]
    public async Task DbSet_fields_are_checked_but_similarly_named_non_ef_methods_are_not()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Store { public DbSet<Subject> Subjects; }
            public sealed class Other
            {
                public Other AsTracking() => this;
                public int Count() => 0;
            }
            public sealed class Report : IReadUseCase
            {
                public void Run(Store store)
                {
                    _ = store.Subjects.Count();
                    _ = store.Subjects.AsNoTracking().Count();
                    _ = new Other().AsTracking().Count();
                    _ = Enumerable.Empty<Subject>().Count();
                }
            }
            """);
        Assert.Equal(ReadUseCaseAnalyzer.NoTrackingId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task A_non_ef_AsNoTracking_homonym_does_not_satisfy_the_convention()
    {
        var diagnostics = await AnalyzeAsync("""
            public static class FakeExtensions
            {
                public static IQueryable<T> AsNoTracking<T>(this DbSet<T> source) where T : class => source;
            }
            public sealed class Report : IReadUseCase
            {
                public int Run(SchoolDbContext db) => db.Subjects.AsNoTracking().Count();
            }
            """);
        Assert.Equal(ReadUseCaseAnalyzer.NoTrackingId, Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Tracking_checks_do_not_change_writes_or_analyze_external_query_flow()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Write : IWriteUseCase
            {
                public void Run(SchoolDbContext db)
                {
                    _ = db.Subjects.Count();
                    _ = db.Subjects.AsTracking().ToList();
                }
            }
            public static class QueryHelper
            {
                public static IQueryable<Subject> GetQuery(SchoolDbContext db) => db.Subjects;
            }
            public sealed class Report : IReadUseCase
            {
                public void Run(SchoolDbContext db, IQueryable<Subject> query)
                {
                    _ = QueryHelper.GetQuery(db).ToList();
                    _ = query.Count();
                }
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SchoolDbContext")]
    [InlineData("IDbContextFactory<SchoolDbContext>")]
    [InlineData("TenantSchoolDbContextFactory")]
    [InlineData("CompileTimeFirst.Sample.ReadModel.IReadSchoolDbFactory")]
    public async Task Read_dependencies_may_use_the_original_context(string dependency)
    {
        var diagnostics = await AnalyzeAsync($$"""
            public sealed class Read({{dependency}} db) : IReadUseCase { }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Original_context_dependencies_are_allowed_in_read_use_cases()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Read : IReadUseCase
            {
                private readonly SchoolDbContext db;
                public IDbContextFactory<SchoolDbContext> Factory { get; set; }
                public Read(SchoolDbContext context) { db = context; }
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Original_context_cannot_escape_into_component_state_or_injection()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Page(IDbContextFactory<SchoolDbContext> factory)
                : Microsoft.AspNetCore.Components.ComponentBase
            {
                private SchoolDbContext db;
            }
            """, new ReadOnlyArchitectureAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == ReadOnlyArchitectureAnalyzer.NoWriteDbContextInUIId);
        Assert.Contains(diagnostics, d => d.Id == ReadOnlyArchitectureAnalyzer.NoEscapedReadStateId);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string declaration, DiagnosticAnalyzer? analyzer = null, string path = "Report.cs")
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using CompileTimeFirst.Sample.Application;
            using CompileTimeFirst.Sample.Data;
            using CompileTimeFirst.Sample.Domain;
            using Microsoft.EntityFrameworkCore;
            """ + Environment.NewLine + declaration, new CSharpParseOptions(LanguageVersion.Preview), path);
        // Use real EF and application symbols, not stubs that could hide an incompatible API.
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(IReadUseCase).Assembly.Location, typeof(SchoolDbContext).Assembly.Location,
                typeof(RelationalQueryableExtensions).Assembly.Location]);
        var compilation = CSharpCompilation.Create("ReadUseCaseTest", [tree],
            paths.Distinct().Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer ?? new ReadUseCaseAnalyzer())).GetAnalyzerDiagnosticsAsync();
    }
}
