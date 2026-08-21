using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CompileTimeFirst.Sample.Analyzers;

/// <summary>
/// Analyzer que garante que classes ViewModel e componentes Blazor:
/// 1. NÃO injetem SchoolDbContext diretamente (apenas IReadSchoolDb/IReadSchoolDbFactory)
/// 2. NÃO usem ToListAsync() do EF Core diretamente
/// 3. Usem IReadQueryExecutor para todos os terminais assíncronos de leitura
/// 4. NÃO armazenem IQueryable, read scope ou read DbContext como estado da UI
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReadOnlyArchitectureAnalyzer : DiagnosticAnalyzer
{
    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";
    private const string DbContextFactoryMetadataName = "Microsoft.EntityFrameworkCore.IDbContextFactory`1";
    private const string EfQueryableExtensionsMetadataName = "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions";
    private const string InjectAttributeMetadataName = "Microsoft.AspNetCore.Components.InjectAttribute";
    private const string QueryableMetadataName = "System.Linq.IQueryable";
    private const string ReadDbMetadataName = "CompileTimeFirst.Sample.ReadModel.IReadSchoolDb";
    private const string ReadDbScopeMetadataName = "CompileTimeFirst.Sample.ReadModel.IReadSchoolDbScope";
    private const string ReadOnlyDbContextMetadataName = "CompileTimeFirst.Sample.Data.ReadOnlySchoolDbContext";
    private const string WriteDbContextMetadataName = "CompileTimeFirst.Sample.Data.SchoolDbContext";

    // Regra 1: Não injetar DbContext de escrita em ViewModels ou componentes Blazor
    public const string NoWriteDbContextInUIId = "CTFA001";
    private static readonly DiagnosticDescriptor NoWriteDbContextInUIRule = new DiagnosticDescriptor(
        id: NoWriteDbContextInUIId,
        title: "ViewModel ou componente Blazor não pode injetar DbContext de escrita",
        messageFormat: "'{0}' não pode injetar 'SchoolDbContext' ou 'IDbContextFactory<SchoolDbContext>'. Use 'IReadSchoolDbFactory' para leitura.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels e componentes Blazor devem usar apenas IReadSchoolDb para queries, nunca o DbContext de escrita.");

    // Regra 2: Não usar ToListAsync() do EF Core diretamente
    public const string NoDirectEfCoreToListAsyncId = "CTFA002";
    private static readonly DiagnosticDescriptor NoDirectEfCoreToListAsyncRule = new DiagnosticDescriptor(
        id: NoDirectEfCoreToListAsyncId,
        title: "Não use ToListAsync() do EF Core diretamente na UI",
        messageFormat: "Não use 'ToListAsync()' do EF Core. Use 'IReadQueryExecutor.ToListAsync()' para manter a abstração de leitura.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels e componentes Blazor devem usar IReadQueryExecutor.ToListAsync() em vez de chamar EF Core diretamente.");

    // Regra 3: Não usar FirstOrDefaultAsync() do EF Core diretamente
    public const string NoDirectEfCoreFirstOrDefaultAsyncId = "CTFA003";
    private static readonly DiagnosticDescriptor NoDirectEfCoreFirstOrDefaultAsyncRule = new DiagnosticDescriptor(
        id: NoDirectEfCoreFirstOrDefaultAsyncId,
        title: "Não use FirstOrDefaultAsync() do EF Core diretamente na UI",
        messageFormat: "Não use 'FirstOrDefaultAsync()' do EF Core. Use 'IReadQueryExecutor.FirstOrDefaultAsync()' para manter a abstração de leitura.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels e componentes Blazor devem usar IReadQueryExecutor.FirstOrDefaultAsync() em vez de chamar EF Core diretamente.");

    // Regra 4: Não armazenar provider/contexto de leitura em estado da UI
    public const string NoEscapedReadStateId = "CTFA004";
    private static readonly DiagnosticDescriptor NoEscapedReadStateRule = new DiagnosticDescriptor(
        id: NoEscapedReadStateId,
        title: "Consulta ou contexto de leitura não pode ser estado da UI",
        messageFormat: "'{0}' não pode armazenar '{1}'. Mantenha a query e o read scope locais à operação e armazene apenas dados materializados.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels e componentes não podem manter IQueryable, IReadSchoolDbScope ou read DbContext em campos/propriedades.");

    // Regra 5: Não usar os demais terminais assíncronos de leitura do EF Core diretamente
    public const string NoDirectEfCoreReadTerminalAsyncId = "CTFA005";
    private static readonly DiagnosticDescriptor NoDirectEfCoreReadTerminalAsyncRule = new DiagnosticDescriptor(
        id: NoDirectEfCoreReadTerminalAsyncId,
        title: "Não use terminais assíncronos do EF Core diretamente na UI",
        messageFormat: "Não use '{0}()' do EF Core diretamente. Use 'IReadQueryExecutor.{0}()'.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels e componentes Blazor devem usar IReadQueryExecutor para SingleOrDefaultAsync(), CountAsync() e AnyAsync().");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            NoWriteDbContextInUIRule,
            NoDirectEfCoreToListAsyncRule,
            NoDirectEfCoreFirstOrDefaultAsyncRule,
            NoEscapedReadStateRule,
            NoDirectEfCoreReadTerminalAsyncRule);

    public override void Initialize(AnalysisContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Analisa construtores para detectar injeção de DbContext de escrita
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzePrimaryConstructor, SyntaxKind.ClassDeclaration);

        // Analisa propriedades para detectar [Inject] em componentes Blazor
        context.RegisterSyntaxNodeAction(AnalyzeProperty, SyntaxKind.PropertyDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);

        // Analisa invocações para detectar terminais assíncronos de leitura do EF Core
        context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzePrimaryConstructor(SyntaxNodeAnalysisContext context)
    {
        var containingClass = (ClassDeclarationSyntax)context.Node;
        if (containingClass.ParameterList is null ||
            !IsViewModelOrBlazorComponent(
                containingClass.Identifier.Text,
                context.SemanticModel,
                containingClass))
        {
            return;
        }

        AnalyzeParameters(
            context,
            containingClass.Identifier.Text,
            containingClass.ParameterList.Parameters);
    }

    private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;
        var containingClass = constructor.Parent as ClassDeclarationSyntax;

        if (containingClass == null)
        {
            return;
        }

        var className = containingClass.Identifier.Text;

        // Verifica se é ViewModel ou componente Blazor
        if (!IsViewModelOrBlazorComponent(className, context.SemanticModel, containingClass))
        {
            return;
        }

        AnalyzeParameters(context, className, constructor.ParameterList.Parameters);
    }

    private void AnalyzeParameters(
        SyntaxNodeAnalysisContext context,
        string className,
        SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        foreach (var parameter in parameters)
        {
            var parameterType = context.SemanticModel.GetTypeInfo(parameter.Type!).Type;
            if (parameterType == null)
            {
                continue;
            }

            if (IsWriteContextType(parameterType, context.Compilation))
            {
                var diagnostic = Diagnostic.Create(
                    NoWriteDbContextInUIRule,
                    parameter.GetLocation(),
                    className);
                context.ReportDiagnostic(diagnostic);
            }

            if (IsForbiddenReadStateType(parameterType, context.Compilation))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NoEscapedReadStateRule,
                    parameter.GetLocation(),
                    className,
                    parameterType.ToDisplayString()));
            }
        }
    }

    private void AnalyzeProperty(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var containingClass = property.Parent as ClassDeclarationSyntax;

        if (containingClass == null)
        {
            return;
        }

        var className = containingClass.Identifier.Text;

        var propertyType = context.SemanticModel.GetTypeInfo(property.Type).Type;
        if (propertyType != null &&
            IsViewModelOrBlazorComponent(className, context.SemanticModel, containingClass) &&
            IsForbiddenReadStateType(propertyType, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoEscapedReadStateRule,
                property.GetLocation(),
                className,
                propertyType.ToDisplayString()));
        }

        if (!HasInjectAttribute(property, context.SemanticModel))
        {
            return;
        }

        if (propertyType == null)
        {
            return;
        }

        if (IsWriteContextType(propertyType, context.Compilation))
        {
            var diagnostic = Diagnostic.Create(
                NoWriteDbContextInUIRule,
                property.GetLocation(),
                className);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var containingClass = field.Parent as ClassDeclarationSyntax;

        if (containingClass == null ||
            !IsViewModelOrBlazorComponent(
                containingClass.Identifier.Text,
                context.SemanticModel,
                containingClass))
        {
            return;
        }

        var fieldType = context.SemanticModel.GetTypeInfo(field.Declaration.Type).Type;
        if (fieldType == null || !IsForbiddenReadStateType(fieldType, context.Compilation))
        {
            return;
        }

        foreach (var variable in field.Declaration.Variables)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoEscapedReadStateRule,
                variable.GetLocation(),
                containingClass.Identifier.Text,
                fieldType.ToDisplayString()));
        }
    }

    private static bool IsForbiddenReadStateType(ITypeSymbol type, Compilation compilation)
    {
        var queryableType = compilation.GetTypeByMetadataName(QueryableMetadataName);
        var readDbType = compilation.GetTypeByMetadataName(ReadDbMetadataName);
        var readDbScopeType = compilation.GetTypeByMetadataName(ReadDbScopeMetadataName);
        var readOnlyContextType = compilation.GetTypeByMetadataName(ReadOnlyDbContextMetadataName);

        return IsSameTypeOrImplements(type, queryableType) ||
               IsSameTypeOrImplements(type, readDbType) ||
               IsSameTypeOrImplements(type, readDbScopeType) ||
               IsSameTypeOrInherits(type, readOnlyContextType);
    }

    private static bool IsWriteContextType(ITypeSymbol type, Compilation compilation)
    {
        var writeContextType = compilation.GetTypeByMetadataName(WriteDbContextMetadataName);
        if (writeContextType is null)
        {
            return false;
        }

        if (IsSameTypeOrInherits(type, writeContextType))
        {
            return true;
        }

        var factoryType = compilation.GetTypeByMetadataName(DbContextFactoryMetadataName);
        if (factoryType is null)
        {
            return false;
        }

        return GetTypeAndInterfaces(type)
            .Any(candidate =>
                candidate.IsGenericType &&
                SymbolEqualityComparer.Default.Equals(candidate.ConstructedFrom, factoryType) &&
                IsSameTypeOrInherits(candidate.TypeArguments[0], writeContextType));
    }

    private static bool HasInjectAttribute(
        PropertyDeclarationSyntax property,
        SemanticModel semanticModel)
    {
        var injectAttributeType = semanticModel.Compilation.GetTypeByMetadataName(
            InjectAttributeMetadataName);
        if (injectAttributeType is null)
        {
            return false;
        }

        return property.AttributeLists
            .SelectMany(attributeList => attributeList.Attributes)
            .Select(attribute => semanticModel.GetSymbolInfo(attribute).Symbol as IMethodSymbol)
            .Any(constructor =>
                SymbolEqualityComparer.Default.Equals(
                    constructor?.ContainingType,
                    injectAttributeType));
    }

    private static bool IsSameTypeOrImplements(
        ITypeSymbol type,
        INamedTypeSymbol? expectedType)
        => expectedType is not null && GetTypeAndInterfaces(type)
            .Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, expectedType));

    private static bool IsSameTypeOrInherits(
        ITypeSymbol type,
        INamedTypeSymbol? expectedType)
    {
        if (expectedType is null)
        {
            return false;
        }

        for (var candidate = type as INamedTypeSymbol;
             candidate is not null;
             candidate = candidate.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, expectedType))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetTypeAndInterfaces(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType)
        {
            yield return namedType;
        }

        foreach (var implementedInterface in type.AllInterfaces)
        {
            yield return implementedInterface;
        }
    }

    private void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var containingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

        if (containingClass == null)
        {
            return;
        }

        var className = containingClass.Identifier.Text;

        if (!IsViewModelOrBlazorComponent(
                className,
                context.SemanticModel,
                containingClass))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null)
        {
            return;
        }

        var efQueryableExtensions = context.Compilation.GetTypeByMetadataName(
            EfQueryableExtensionsMetadataName);
        var declaredMethod = methodSymbol.ReducedFrom ?? methodSymbol;

        if (efQueryableExtensions is null ||
            !SymbolEqualityComparer.Default.Equals(
                declaredMethod.OriginalDefinition.ContainingType,
                efQueryableExtensions))
        {
            return;
        }

        if (methodSymbol.Name == "ToListAsync")
        {
            var diagnostic = Diagnostic.Create(
                NoDirectEfCoreToListAsyncRule,
                invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        if (methodSymbol.Name == "FirstOrDefaultAsync")
        {
            var diagnostic = Diagnostic.Create(
                NoDirectEfCoreFirstOrDefaultAsyncRule,
                invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        if (methodSymbol.Name is "SingleOrDefaultAsync" or "CountAsync" or "AnyAsync")
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NoDirectEfCoreReadTerminalAsyncRule,
                invocation.GetLocation(),
                methodSymbol.Name));
        }
    }

    private static bool IsViewModelOrBlazorComponent(
        string className,
        SemanticModel semanticModel,
        ClassDeclarationSyntax classDecl)
    {
        // ViewModels terminam com "ViewModel"
        if (className.EndsWith("ViewModel", StringComparison.Ordinal))
        {
            return true;
        }

        // Componentes Blazor herdam de ComponentBase
        var componentBase = semanticModel.Compilation.GetTypeByMetadataName(
            ComponentBaseMetadataName);
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null || componentBase is null)
        {
            return false;
        }

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, componentBase))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }
}
