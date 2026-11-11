using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace GrifballWebApp.Analyzers;

/// <summary>
/// GRIF001: a query (an <see cref="System.Linq.IQueryable"/>) run synchronously in async code. Microsoft's
/// VSTHRD103 pairs a call with its async twin on the same type, so it sees First, Any, Count and SaveChanges, but not
/// ToList, ToArray, ToDictionary, ToHashSet, ToLookup or AsEnumerable: those are System.Linq.Enumerable's, on
/// IEnumerable&lt;T&gt;, while EF's ToListAsync and the others extend IQueryable&lt;T&gt;. Nor does it see a foreach over
/// a query. In a method, local function or lambda that is async or returns a Task or ValueTask, this reports:
/// <list type="bullet">
/// <item>an Enumerable method called on a query (it runs the query, or turns the rest of the chain client-side);</item>
/// <item>a query passed where an IEnumerable is taken (new List&lt;T&gt;(query), string.Join(",", query), AddRange);</item>
/// <item>a foreach (not an await foreach) over a query, and a query spread into a collection expression ([.. query]).</item>
/// </list>
/// What a query's type says is all it goes by: an IEnumerable that is a query underneath isn't seen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SyncQueryExecutionAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "GRIF001";

    internal static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Synchronous query execution in async code",
        messageFormat: "{0} runs the query synchronously in async code: {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A query (IQueryable) enumerated by System.Linq.Enumerable, a foreach or an IEnumerable parameter blocks a thread on the database. In async code, use EF Core's async operators (ToListAsync, ToArrayAsync, ToDictionaryAsync, ToHashSetAsync) or AsAsyncEnumerable with await foreach.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var queryable = start.Compilation.GetTypeByMetadataName("System.Linq.IQueryable");
            if (queryable is null)
                return;
            var types = new KnownTypes(queryable,
                start.Compilation.GetTypeByMetadataName("System.Linq.Enumerable"),
                start.Compilation.GetTypeByMetadataName("System.Linq.Queryable"),
                start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task"),
                start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask"),
                start.Compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
            start.RegisterOperationAction(c => AnalyzeCall(c, types, ((IInvocationOperation)c.Operation).TargetMethod, ((IInvocationOperation)c.Operation).Arguments), OperationKind.Invocation);
            start.RegisterOperationAction(c => AnalyzeCreation(c, types), OperationKind.ObjectCreation);
            start.RegisterSyntaxNodeAction(c => AnalyzeForEach(c, types), SyntaxKind.ForEachStatement, SyntaxKind.ForEachVariableStatement);
            start.RegisterSyntaxNodeAction(c => AnalyzeSpread(c, types), SyntaxKind.SpreadElement);
        });
    }

    private sealed class KnownTypes(INamedTypeSymbol queryable, INamedTypeSymbol? enumerable, INamedTypeSymbol? queryableOperators, params INamedTypeSymbol?[] tasks)
    {
        public INamedTypeSymbol? Enumerable { get; } = enumerable;

        public INamedTypeSymbol? Queryable { get; } = queryableOperators;

        /// <summary>Whether <paramref name="type"/> is IQueryable or implements it: a query, not yet run.</summary>
        public bool IsQuery(ITypeSymbol? type)
            => type is not null
               && (SymbolEqualityComparer.Default.Equals(type, queryable) || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, queryable)));

        public bool IsTask(ITypeSymbol type)
            => tasks.Any(t => SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, t));
    }

    /// <summary>A constructor call, when it binds to a constructor</summary>
    private static void AnalyzeCreation(OperationAnalysisContext context, KnownTypes types)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Constructor is { } constructor)
            AnalyzeCall(context, types, constructor, creation.Arguments);
    }

    private static void AnalyzeCall(OperationAnalysisContext context, KnownTypes types, IMethodSymbol method, ImmutableArray<IArgumentOperation> arguments)
    {
        // Queryable's operators take a query and add to it (Concat, Join and Zip take their second sequence as an
        // IEnumerable, and translate a query there too)
        if (SymbolEqualityComparer.Default.Equals(method.ContainingType, types.Queryable))
            return;
        var isEnumerable = SymbolEqualityComparer.Default.Equals(method.ContainingType, types.Enumerable);
        foreach (var argument in arguments)
        {
            if (argument.Parameter is not { } parameter || types.IsQuery(parameter.Type) || !IsSequence(parameter.Type)
                || !types.IsQuery(Unconverted(argument.Value).Type))
                continue;
            if (!IsAsyncContext(context.Operation.Syntax, context.Operation.SemanticModel!, types, context.CancellationToken))
                return;
            var (call, fix) = isEnumerable
                ? Describe(method)
                : ($"Passing a query to {method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}", "materialize it first with await ToListAsync() (or ToArrayAsync)");
            context.ReportDiagnostic(Diagnostic.Create(Rule, Location(context.Operation.Syntax), call, fix));
            return;
        }
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        var loop = (CommonForEachStatementSyntax)context.Node;
        if (loop.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
            || !types.IsQuery(context.SemanticModel.GetTypeInfo(loop.Expression, context.CancellationToken).Type)
            || !IsAsyncContext(loop, context.SemanticModel, types, context.CancellationToken))
            return;
        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.ForEachKeyword.GetLocation(), "A foreach over a query", "use await foreach over AsAsyncEnumerable(), or await ToListAsync() first"));
    }

    private static void AnalyzeSpread(SyntaxNodeAnalysisContext context, KnownTypes types)
    {
        var spread = (SpreadElementSyntax)context.Node;
        if (!types.IsQuery(context.SemanticModel.GetTypeInfo(spread.Expression, context.CancellationToken).Type)
            || !IsAsyncContext(spread, context.SemanticModel, types, context.CancellationToken))
            return;
        context.ReportDiagnostic(Diagnostic.Create(Rule, spread.GetLocation(), "Spreading a query", "materialize it first with await ToListAsync() (or ToArrayAsync)"));
    }

    /// <summary>The call's name and its async form for an Enumerable method on a query.</summary>
    private static (string Call, string Fix) Describe(IMethodSymbol method)
    {
        var fix = method.Name switch
        {
            "ToList" or "ToArray" or "ToDictionary" or "ToHashSet" => $"use await {method.Name}Async()",
            "AsEnumerable" => "use AsAsyncEnumerable() with await foreach, or await ToListAsync()",
            "ToLookup" => "await ToListAsync(), then ToLookup on the list",
            _ => $"await ToListAsync() first, then {method.Name} on the list",
        };
        return ($"{method.Name}()", fix);
    }

    /// <summary>The expression's value before its implicit conversions (to the parameter's type).</summary>
    private static IOperation Unconverted(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;
        return operation;
    }

    /// <summary>Whether a parameter of this type takes a sequence it may enumerate: IEnumerable or IEnumerable&lt;T&gt;.</summary>
    private static bool IsSequence(ITypeSymbol type)
        => type.SpecialType == SpecialType.System_Collections_IEnumerable
           || type.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;

    /// <summary>The call's name (query.ToList() at ToList) or else the whole expression.</summary>
    private static Location Location(SyntaxNode syntax)
        => syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access }
            ? Microsoft.CodeAnalysis.Location.Create(syntax.SyntaxTree, Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(access.Name.SpanStart, syntax.Span.End))
            : syntax.GetLocation();

    /// <summary>
    /// Whether the code at <paramref name="node"/> runs in an async context, as VSTHRD103 has it: its nearest
    /// enclosing method, local function or lambda is async or returns a Task or ValueTask. A field's or property's
    /// initializer is in none.
    /// </summary>
    private static bool IsAsyncContext(SyntaxNode node, SemanticModel model, KnownTypes types, System.Threading.CancellationToken cancellationToken)
    {
        var scope = node.AncestorsAndSelf().FirstOrDefault(a => a is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax or ArrowExpressionClauseSyntax { Parent: BasePropertyDeclarationSyntax } or MemberDeclarationSyntax);
        var function = scope switch
        {
            AnonymousFunctionExpressionSyntax lambda => model.GetSymbolInfo(lambda, cancellationToken).Symbol as IMethodSymbol,
            LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax => model.GetDeclaredSymbol(scope, cancellationToken) as IMethodSymbol,
            ArrowExpressionClauseSyntax arrow => ((IPropertySymbol)model.GetDeclaredSymbol(arrow.Parent!, cancellationToken)!).GetMethod,
            // A top-level statement: the generated Main, async when the statements await
            GlobalStatementSyntax global => model.GetEnclosingSymbol(global.SpanStart, cancellationToken) as IMethodSymbol,
            _ => null,
        };
        return function is not null && (function.IsAsync || types.IsTask(function.ReturnType));
    }
}
