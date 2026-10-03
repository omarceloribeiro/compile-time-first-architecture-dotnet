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
    public const string NoTrackingId = "CTFA009";

    private static readonly DiagnosticDescriptor Classification = new(
        ClassificationId, "Classify the use case as read or write",
        "'{0}' must implement exactly one of IReadUseCase and IWriteUseCase",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Persistence = new(
        PersistenceId, "A read use case cannot persist",
        "Read use case code cannot reference '{0}'; remove the mutation from the read flow, or classify the operation as IWriteUseCase if its actor intention is to write",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NoTracking = new(
        NoTrackingId, "Start read queries with an explicit no-tracking modifier",
        "Read use case queries must apply AsNoTracking() or AsNoTrackingWithIdentityResolution() immediately at the source and cannot use AsTracking(): {0}",
        "Architecture", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Classification, Persistence, NoTracking);

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

            var forbiddenMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            AddMethods(forbiddenMethods, "Microsoft.EntityFrameworkCore.DbContext", "SaveChanges", "SaveChangesAsync");
            AddMethods(forbiddenMethods, "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions",
                "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync");
            // EF Core 10 exposes bulk writes on the non-relational extension type.
            AddMethods(forbiddenMethods, "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions",
                "ExecuteUpdate", "ExecuteUpdateAsync", "ExecuteDelete", "ExecuteDeleteAsync");
            AddMethods(forbiddenMethods, "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions",
                "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync",
                "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync");

            var dbSet = start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
            var querySources = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            AddMethods(querySources, "Microsoft.EntityFrameworkCore.DbContext", "Set");
            AddMethods(querySources, "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions",
                "FromSql", "FromSqlRaw", "FromSqlInterpolated");
            var noTrackingMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            AddMethods(noTrackingMethods, "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions",
                "AsNoTracking", "AsNoTrackingWithIdentityResolution");
            var trackingMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            AddMethods(trackingMethods, "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "AsTracking");

            start.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
            start.RegisterOperationAction(AnalyzePersistence, OperationKind.Invocation, OperationKind.MethodReference);
            start.RegisterOperationAction(AnalyzeTracking, OperationKind.Invocation, OperationKind.MethodReference,
                OperationKind.PropertyReference, OperationKind.FieldReference);

            void AddMethods(HashSet<IMethodSymbol> methods, string metadataName, params string[] memberNames)
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
                        methods.Add(method.OriginalDefinition);
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

                if (MatchesMethod(target, forbiddenMethods))
                {
                    analysis.ReportDiagnostic(Diagnostic.Create(
                        Persistence, analysis.Operation.Syntax.GetLocation(), target!.ToDisplayString()));
                }
            }

            void AnalyzeTracking(OperationAnalysisContext analysis)
            {
                if (!IsReadScope(analysis.ContainingSymbol.ContainingType, read))
                {
                    return;
                }

                var operation = analysis.Operation;
                var method = operation switch
                {
                    IInvocationOperation invocation => invocation.TargetMethod,
                    IMethodReferenceOperation reference => reference.Method,
                    _ => null
                };
                if (MatchesMethod(method, trackingMethods))
                {
                    analysis.ReportDiagnostic(Diagnostic.Create(
                        NoTracking, operation.Syntax.GetLocation(), "AsTracking is forbidden"));
                    return;
                }

                var isSource = operation is IInvocationOperation && MatchesMethod(method, querySources)
                    || operation is IPropertyReferenceOperation or IFieldReferenceOperation
                        && dbSet is not null && operation.Type is INamedTypeSymbol type
                        && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, dbSet);
                if (!isSource)
                {
                    return;
                }

                var consumer = operation.Parent;
                while (consumer is IConversionOperation { OperatorMethod: null }
                    or IParenthesizedOperation or IArgumentOperation)
                {
                    consumer = consumer.Parent;
                }

                // Metadata inspection and assigning a DbSet member are not query reads.
                if (consumer is INameOfOperation or IPropertyReferenceOperation
                    || consumer is ISimpleAssignmentOperation assignment && assignment.Target == operation)
                {
                    return;
                }

                if (consumer is IInvocationOperation call &&
                    (MatchesMethod(call.TargetMethod, noTrackingMethods)
                        // FromSql needs a DbSet receiver; check its result as the query source instead.
                        || MatchesMethod(call.TargetMethod, querySources)
                        // These calls already receive a persistence or tracking diagnostic.
                        || MatchesMethod(call.TargetMethod, forbiddenMethods)
                        || MatchesMethod(call.TargetMethod, trackingMethods)))
                {
                    return;
                }

                analysis.ReportDiagnostic(Diagnostic.Create(
                    NoTracking, operation.Syntax.GetLocation(), "the query source has no immediate modifier"));
            }
        });
    }

    private static bool MatchesMethod(IMethodSymbol? target, HashSet<IMethodSymbol> methods)
    {
        for (var method = target; method is not null; method = method.OverriddenMethod)
        {
            if (methods.Contains((method.ReducedFrom ?? method).OriginalDefinition))
            {
                return true;
            }
        }

        return false;
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
}
