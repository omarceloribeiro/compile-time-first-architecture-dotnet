using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace CompileTimeFirst.Sample.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadUseCaseAnalyzer : DiagnosticAnalyzer
{
    public const string ClassificationId = "CTFA006";
    public const string PersistenceId = "CTFA007";
    public const string WritableContextId = "CTFA008";

    private static readonly DiagnosticDescriptor Classification = new(
        ClassificationId, "Classify the use case as read or write",
        "'{0}' must implement exactly one of IReadUseCase and IWriteUseCase",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Persistence = new(
        PersistenceId, "A read use case cannot persist",
        "Read use case code cannot reference '{0}'; persist through a write use case",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor WritableContext = new(
        WritableContextId, "A read use case cannot receive a writable context",
        "Read use case '{0}' cannot receive '{1}'; use IDbContextFactory<QuerySchoolDbContext> or the explicitly approved CQRS read surface",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Classification, Persistence, WritableContext);

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var useCase = start.Compilation.GetTypeByMetadataName("CompileTimeFirst.Sample.Application.IUseCase");
            var read = start.Compilation.GetTypeByMetadataName("CompileTimeFirst.Sample.Application.IReadUseCase");
            var write = start.Compilation.GetTypeByMetadataName("CompileTimeFirst.Sample.Application.IWriteUseCase");
            if (useCase is null || read is null || write is null)
            {
                return;
            }

            var original = start.Compilation.GetTypeByMetadataName("CompileTimeFirst.Sample.Data.SchoolDbContext");
            var query = start.Compilation.GetTypeByMetadataName("CompileTimeFirst.Sample.Data.QuerySchoolDbContext");
            var factory = start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IDbContextFactory`1");
            var forbiddenMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            AddMethods("Microsoft.EntityFrameworkCore.DbContext", "SaveChanges", "SaveChangesAsync");
            AddMethods("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions",
                "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync");
            // EF Core 10 exposes bulk writes on the non-relational extension type.
            AddMethods("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions",
                "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync");
            AddMethods("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions",
                "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync",
                "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync");

            start.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
            start.RegisterOperationAction(AnalyzePersistence, OperationKind.Invocation, OperationKind.MethodReference);

            void AddMethods(string metadataName, params string[] memberNames)
            {
                var type = start.Compilation.GetTypeByMetadataName(metadataName);
                if (type is null)
                {
                    return;
                }

                foreach (var name in memberNames)
                {
                    foreach (var method in type.GetMembers(name).OfType<IMethodSymbol>())
                    {
                        forbiddenMethods.Add(method.OriginalDefinition);
                    }
                }
            }

            void AnalyzeType(SymbolAnalysisContext analysis)
            {
                var type = (INamedTypeSymbol)analysis.Symbol;
                if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
                {
                    return;
                }

                if (!type.IsAbstract && Implements(type, useCase) &&
                    Implements(type, read) == Implements(type, write))
                {
                    analysis.ReportDiagnostic(Diagnostic.Create(Classification, type.Locations[0], type.Name));
                }

                if (!IsReadScope(type, read))
                {
                    return;
                }

                foreach (var constructor in type.InstanceConstructors.Where(c => !c.IsImplicitlyDeclared))
                {
                    foreach (var parameter in constructor.Parameters)
                    {
                        CheckDependency(parameter, parameter.Type);
                    }
                }

                foreach (var member in type.GetMembers().Where(m => !m.IsImplicitlyDeclared))
                {
                    if (member is IPropertySymbol property)
                    {
                        CheckDependency(property, property.Type);
                    }
                    else if (member is IFieldSymbol field)
                    {
                        CheckDependency(field, field.Type);
                    }
                }

                void CheckDependency(ISymbol member, ITypeSymbol dependency)
                {
                    if (IsWritable(dependency) ||
                        TypeAndInterfaces(dependency).Any(candidate =>
                            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, factory) &&
                            candidate.TypeArguments.Length == 1 && IsWritable(candidate.TypeArguments[0])))
                    {
                        analysis.ReportDiagnostic(Diagnostic.Create(
                            WritableContext, member.Locations[0], type.Name, dependency.ToDisplayString()));
                    }
                }

                bool IsWritable(ITypeSymbol candidate) =>
                    Inherits(candidate, original) && !Inherits(candidate, query);
            }

            void AnalyzePersistence(OperationAnalysisContext analysis)
            {
                if (!IsReadScope(analysis.ContainingSymbol.ContainingType, read))
                {
                    return;
                }

                var target = analysis.Operation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IMethodReferenceOperation reference => reference.Method,
                    _ => null
                };

                for (var method = target; method is not null; method = method.OverriddenMethod)
                {
                    if (forbiddenMethods.Contains((method.ReducedFrom ?? method).OriginalDefinition))
                    {
                        analysis.ReportDiagnostic(Diagnostic.Create(
                            Persistence, analysis.Operation.Syntax.GetLocation(), target!.ToDisplayString()));
                        return;
                    }
                }
            }
        });
    }

    private static bool IsReadScope(INamedTypeSymbol? type, INamedTypeSymbol read)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (Implements(current, read))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Implements(INamedTypeSymbol type, INamedTypeSymbol contract) =>
        type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, contract));

    private static bool Inherits(ITypeSymbol type, INamedTypeSymbol? expected)
    {
        if (expected is null)
        {
            return false;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expected))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> TypeAndInterfaces(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named)
        {
            yield return named;
        }

        foreach (var contract in type.AllInterfaces)
        {
            yield return contract;
        }
    }
}
