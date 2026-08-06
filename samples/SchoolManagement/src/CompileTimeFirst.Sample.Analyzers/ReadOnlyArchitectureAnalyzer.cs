using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace CompileTimeFirst.Sample.Analyzers;

/// <summary>
/// Analyzer que garante que classes ViewModel e componentes Blazor:
/// 1. NÃO injetem SchoolDbContext diretamente (apenas IReadSchoolDb/IReadSchoolDbFactory)
/// 2. NÃO usem ToListAsync() do EF Core diretamente
/// 3. Usem IReadQueryExecutor.ToListAsync() para queries
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReadOnlyArchitectureAnalyzer : DiagnosticAnalyzer
{
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
        title: "Não use ToListAsync() do EF Core diretamente em ViewModels",
        messageFormat: "Não use 'ToListAsync()' do EF Core. Use 'IReadQueryExecutor.ToListAsync()' para manter a abstração de leitura.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels devem usar IReadQueryExecutor.ToListAsync() em vez de chamar EF Core diretamente.");

    // Regra 3: Não usar FirstOrDefaultAsync() do EF Core diretamente
    public const string NoDirectEfCoreFirstOrDefaultAsyncId = "CTFA003";
    private static readonly DiagnosticDescriptor NoDirectEfCoreFirstOrDefaultAsyncRule = new DiagnosticDescriptor(
        id: NoDirectEfCoreFirstOrDefaultAsyncId,
        title: "Não use FirstOrDefaultAsync() do EF Core diretamente em ViewModels",
        messageFormat: "Não use 'FirstOrDefaultAsync()' do EF Core. Use 'IReadQueryExecutor.FirstOrDefaultAsync()' para manter a abstração de leitura.",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ViewModels devem usar IReadQueryExecutor.FirstOrDefaultAsync() em vez de chamar EF Core diretamente.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            NoWriteDbContextInUIRule,
            NoDirectEfCoreToListAsyncRule,
            NoDirectEfCoreFirstOrDefaultAsyncRule);

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

        // Analisa invocações de métodos para detectar ToListAsync/FirstOrDefaultAsync do EF Core
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

            var typeName = parameterType.ToDisplayString();

            // Detecta injeção de SchoolDbContext ou IDbContextFactory<SchoolDbContext>
            if (typeName.Contains("SchoolDbContext") &&
                !typeName.Contains("ReadOnlySchoolDbContext") &&
                !typeName.Contains("IReadSchoolDb"))
            {
                var diagnostic = Diagnostic.Create(
                    NoWriteDbContextInUIRule,
                    parameter.GetLocation(),
                    className);
                context.ReportDiagnostic(diagnostic);
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

        // Verifica se é componente Blazor (tem atributo [Inject])
        var hasInjectAttribute = property.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => attr.Name.ToString() == "Inject");

        if (!hasInjectAttribute)
        {
            return;
        }

        var propertyType = context.SemanticModel.GetTypeInfo(property.Type).Type;
        if (propertyType == null)
        {
            return;
        }

        var typeName = propertyType.ToDisplayString();

        // Detecta injeção de SchoolDbContext ou IDbContextFactory<SchoolDbContext>
        if (typeName.Contains("SchoolDbContext") &&
            !typeName.Contains("ReadOnlySchoolDbContext") &&
            !typeName.Contains("IReadSchoolDb"))
        {
            var diagnostic = Diagnostic.Create(
                NoWriteDbContextInUIRule,
                property.GetLocation(),
                className);
            context.ReportDiagnostic(diagnostic);
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

        // Verifica se é ViewModel
        if (!className.EndsWith("ViewModel"))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var methodSymbol = symbolInfo.Symbol as IMethodSymbol;

        if (methodSymbol == null)
        {
            return;
        }

        var methodName = methodSymbol.Name;
        var containingTypeName = methodSymbol.ContainingType?.ToDisplayString() ?? "";

        // Detecta ToListAsync() do EF Core
        if (methodName == "ToListAsync" &&
            containingTypeName.StartsWith("Microsoft.EntityFrameworkCore"))
        {
            var diagnostic = Diagnostic.Create(
                NoDirectEfCoreToListAsyncRule,
                invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        // Detecta FirstOrDefaultAsync() do EF Core
        if (methodName == "FirstOrDefaultAsync" &&
            containingTypeName.StartsWith("Microsoft.EntityFrameworkCore"))
        {
            var diagnostic = Diagnostic.Create(
                NoDirectEfCoreFirstOrDefaultAsyncRule,
                invocation.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private bool IsViewModelOrBlazorComponent(string className, SemanticModel semanticModel, ClassDeclarationSyntax classDecl)
    {
        // ViewModels terminam com "ViewModel"
        if (className.EndsWith("ViewModel"))
        {
            return true;
        }

        // Componentes Blazor herdam de ComponentBase
        var classSymbol = semanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
        {
            return false;
        }

        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "ComponentBase")
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }
}
