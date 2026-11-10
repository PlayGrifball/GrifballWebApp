using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace GrifballWebApp.Analyzers;

/// <summary>
/// Silences SonarAnalyzer's S6966 ("await the async method instead") on EF Core's <c>Add</c> and <c>AddRange</c>, of a
/// <c>DbSet&lt;T&gt;</c> or a <c>DbContext</c> (subclasses included): they only start tracking, and their async forms
/// matter only for HiLo key generation, which Grif doesn't use. The async forms are banned (BannedSymbols.txt, RS0030).
/// Every other S6966 stays.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EfAddSuppressor : DiagnosticSuppressor
{
    public static readonly SuppressionDescriptor Rule = new(
        id: "GRIF0001",
        suppressedDiagnosticId: "S6966",
        justification: "EF Core's Add and AddRange only start tracking: AddAsync and AddRangeAsync matter only for HiLo key generation, which Grif doesn't use.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is not { } tree)
                continue;
            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (node.FirstAncestorOrSelf<InvocationExpressionSyntax>() is not { } invocation)
                continue;
            var model = context.GetSemanticModel(tree);
            if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method && IsEfAdd(method, model.Compilation))
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
        }
    }

    /// <summary>
    /// Whether <paramref name="method"/> is (or overrides) <c>Add</c> or <c>AddRange</c> declared by EF Core's
    /// <c>DbContext</c> or <c>DbSet&lt;T&gt;</c>.
    /// </summary>
    internal static bool IsEfAdd(IMethodSymbol method, Compilation compilation)
    {
        var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        if (dbContext is null && dbSet is null)
            return false;

        for (var declared = method.OriginalDefinition; declared is not null; declared = declared.OverriddenMethod?.OriginalDefinition)
        {
            var type = declared.ContainingType.OriginalDefinition;
            var isEfType = SymbolEqualityComparer.Default.Equals(type, dbContext) || SymbolEqualityComparer.Default.Equals(type, dbSet);
            if (isEfType)
                return IsAddMember(declared, type);
        }
        return false;
    }

    /// <summary>One of <paramref name="type"/>'s own Add or AddRange overloads.</summary>
    private static bool IsAddMember(IMethodSymbol declared, INamedTypeSymbol type)
        => type.GetMembers("Add").Concat(type.GetMembers("AddRange"))
            .Any(member => SymbolEqualityComparer.Default.Equals(member, declared));
}
