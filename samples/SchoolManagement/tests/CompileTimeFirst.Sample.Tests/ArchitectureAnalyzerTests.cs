using System.Collections.Immutable;
using CompileTimeFirst.Sample.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CompileTimeFirst.Sample.Tests;

public sealed class ArchitectureAnalyzerTests
{
    [Fact]
    public async Task Primary_constructor_write_context_is_reported()
    {
        const string source = """
            namespace CompileTimeFirst.Sample.Data
            {
                public sealed class SchoolDbContext;
            }

            namespace Microsoft.EntityFrameworkCore
            {
                public interface IDbContextFactory<T>;
            }

            public sealed class BadViewModel(
                Microsoft.EntityFrameworkCore.IDbContextFactory<
                    CompileTimeFirst.Sample.Data.SchoolDbContext> factory);
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoWriteDbContextInUIId);
    }

    [Fact]
    public async Task IQueryable_field_in_view_model_is_reported()
    {
        const string source = """
            using System.Linq;
            public sealed class ProductsViewModel
            {
                private IQueryable<int>? _query;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoEscapedReadStateId);
    }

    [Fact]
    public async Task IOrderedQueryable_field_in_view_model_is_reported()
    {
        const string source = """
            using System.Linq;
            public sealed class ProductsViewModel
            {
                private IOrderedQueryable<int>? _query;
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoEscapedReadStateId);
    }

    [Fact]
    public async Task Direct_ef_async_terminals_in_blazor_component_are_reported()
    {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;
            using System.Threading.Tasks;
            using Microsoft.AspNetCore.Components;
            using Microsoft.EntityFrameworkCore;

            namespace Microsoft.AspNetCore.Components
            {
                public abstract class ComponentBase;
            }

            namespace Microsoft.EntityFrameworkCore
            {
                public static class EntityFrameworkQueryableExtensions
                {
                    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> query) =>
                        Task.FromResult(new List<T>());

                    public static Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> query) =>
                        Task.FromResult(default(T)!);

                    public static Task<int> CountAsync<T>(this IQueryable<T> query) =>
                        Task.FromResult(0);

                    public static Task<bool> AnyAsync<T>(this IQueryable<T> query) =>
                        Task.FromResult(false);

                    public static Task<T> SingleOrDefaultAsync<T>(this IQueryable<T> query) =>
                        Task.FromResult(default(T)!);
                }

                public static class CustomQueryExtensions
                {
                    public static Task<int> CountAsync<T>(IQueryable<T> query) =>
                        Task.FromResult(0);
                }
            }

            public interface IReadQueryExecutor
            {
                Task<int> CountAsync<T>(IQueryable<T> query);
            }

            public sealed class QueryComponent : ComponentBase
            {
                public async Task LoadAsync(
                    IQueryable<int> query,
                    IReadQueryExecutor executor)
                {
                    _ = await query.ToListAsync();
                    _ = await query.FirstOrDefaultAsync();
                    _ = await query.CountAsync();
                    _ = await query.AnyAsync();
                    _ = await query.SingleOrDefaultAsync();
                    _ = await executor.CountAsync(query);
                    _ = await Microsoft.EntityFrameworkCore.CustomQueryExtensions.CountAsync(query);
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);
        Assert.Single(diagnostics.Where(diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoDirectEfCoreToListAsyncId));
        Assert.Single(diagnostics.Where(diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoDirectEfCoreFirstOrDefaultAsyncId));

        var terminalDiagnostics = diagnostics
            .Where(diagnostic =>
                diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoDirectEfCoreReadTerminalAsyncId)
            .ToArray();

        Assert.Equal(3, terminalDiagnostics.Length);
        Assert.Contains(terminalDiagnostics, diagnostic =>
            diagnostic.GetMessage().Contains("CountAsync", StringComparison.Ordinal));
        Assert.Contains(terminalDiagnostics, diagnostic =>
            diagnostic.GetMessage().Contains("AnyAsync", StringComparison.Ordinal));
        Assert.Contains(terminalDiagnostics, diagnostic =>
            diagnostic.GetMessage().Contains("SingleOrDefaultAsync", StringComparison.Ordinal));
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Preview));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyArchitectureAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }
}
