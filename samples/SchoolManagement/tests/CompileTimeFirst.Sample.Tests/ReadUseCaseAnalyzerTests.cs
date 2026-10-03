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
                private void Helper(QuerySchoolDbContext db) { {{statement}} }
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
                    public void Save(QuerySchoolDbContext db) => db.SaveChanges();
                }
            }
            """);
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal(ReadUseCaseAnalyzer.PersistenceId, d.Id));
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
                public async Task Run(QuerySchoolDbContext db)
                {
                    _ = await db.Subjects.CountAsync();
                    _ = await db.Subjects.Select(x => x.Name).ToListAsync();
                    new Other().SaveChanges();
                    new Other().ExecuteDelete();
                }
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SchoolDbContext", true)]
    [InlineData("IDbContextFactory<SchoolDbContext>", true)]
    [InlineData("TenantSchoolDbContextFactory", true)]
    [InlineData("QuerySchoolDbContext", false)]
    [InlineData("IDbContextFactory<QuerySchoolDbContext>", false)]
    [InlineData("TenantQuerySchoolDbContextFactory", false)]
    [InlineData("CompileTimeFirst.Sample.ReadModel.IReadSchoolDbFactory", false)]
    public async Task Read_dependency_must_not_be_the_original_writable_context(string dependency, bool rejected)
    {
        var diagnostics = await AnalyzeAsync($$"""
            public sealed class Read({{dependency}} db) : IReadUseCase { }
            """);
        Assert.Equal(rejected, diagnostics.Any(d => d.Id == ReadUseCaseAnalyzer.WritableContextId));
    }

    [Fact]
    public async Task Constructor_field_and_property_dependencies_are_checked()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Read : IReadUseCase
            {
                private readonly SchoolDbContext db;
                public IDbContextFactory<SchoolDbContext> Factory { get; set; }
                public Read(SchoolDbContext context) { db = context; }
            }
            """);
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal(ReadUseCaseAnalyzer.WritableContextId, d.Id));
    }

    [Fact]
    public async Task Protected_context_cannot_escape_into_component_state_or_injection()
    {
        var diagnostics = await AnalyzeAsync("""
            public sealed class Page(IDbContextFactory<QuerySchoolDbContext> factory)
                : Microsoft.AspNetCore.Components.ComponentBase
            {
                private QuerySchoolDbContext db;
            }
            """, new ReadOnlyArchitectureAnalyzer());
        Assert.Contains(diagnostics, d => d.Id == ReadOnlyArchitectureAnalyzer.NoWriteDbContextInUIId);
        Assert.Contains(diagnostics, d => d.Id == ReadOnlyArchitectureAnalyzer.NoEscapedReadStateId);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        string declaration, DiagnosticAnalyzer? analyzer = null)
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using CompileTimeFirst.Sample.Application;
            using CompileTimeFirst.Sample.Data;
            using Microsoft.EntityFrameworkCore;
            """ + Environment.NewLine + declaration, new CSharpParseOptions(LanguageVersion.Preview));
        // Use real EF and application symbols, not stubs that could hide an incompatible API.
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat([typeof(IReadUseCase).Assembly.Location, typeof(QuerySchoolDbContext).Assembly.Location,
                typeof(RelationalQueryableExtensions).Assembly.Location]);
        var compilation = CSharpCompilation.Create("ReadUseCaseTest", [tree],
            paths.Distinct().Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        return await compilation.WithAnalyzers(
            ImmutableArray.Create(analyzer ?? new ReadUseCaseAnalyzer())).GetAnalyzerDiagnosticsAsync();
    }
}
