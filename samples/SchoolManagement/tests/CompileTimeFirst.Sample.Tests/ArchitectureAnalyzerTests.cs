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
            public sealed class SchoolDbContext;
            public interface IDbContextFactory<T>;
            public sealed class BadViewModel(IDbContextFactory<SchoolDbContext> factory);
            """;

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

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyArchitectureAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

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

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ReadOnlyArchitectureAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Id == ReadOnlyArchitectureAnalyzer.NoEscapedReadStateId);
    }
}
