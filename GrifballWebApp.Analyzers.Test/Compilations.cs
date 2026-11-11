using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Analyzers.Test;

/// <summary>
/// Small compilations against the running .NET and EF Core, with GRIF001's analyzer: what it reports. Each compiles
/// cleanly first, so a probe that stops compiling fails rather than passes for want of anything to report.
/// </summary>
internal static class Compilations
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;

        """;

    private const string Types = """

        public class Thing { public int Id { get; set; } public string Name { get; set; } = ""; }

        public class ProbeContext : DbContext { public DbSet<Thing> Things => Set<Thing>(); }
        """;

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(() =>
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Append(typeof(DbContext).Assembly.Location)
                .Distinct()
                .Select(path => MetadataReference.CreateFromFile(path)),
        ]);

    /// <summary>GRIF001's diagnostics on <paramref name="members"/>, in a class with a context (db) and a list (list).</summary>
    public static Task<List<Diagnostic>> Diagnose(string members)
        => Run(Usings + $$"""
            public class Probe
            {
                private readonly ProbeContext db = new();
                private readonly List<Thing> list = [];
                private static int Enumerate(System.Collections.IEnumerable items) => items.GetHashCode();

                {{members}}
            }
            """ + Types, OutputKind.DynamicallyLinkedLibrary, References.Value);

    /// <summary>GRIF001's diagnostics on <paramref name="members"/> that need not compile (a foreach over null).</summary>
    public static Task<List<Diagnostic>> DiagnoseUnchecked(string members)
        => Analyze(CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(Usings + "public class Probe { " + members + " }")], References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

    /// <summary>GRIF001's diagnostics on a program of top-level <paramref name="statements"/>.</summary>
    public static Task<List<Diagnostic>> DiagnoseProgram(string statements)
        => Run(Usings + statements + Types, OutputKind.ConsoleApplication, References.Value);

    /// <summary>GRIF001's diagnostics on <paramref name="source"/> compiled with no references at all (no System.Linq).</summary>
    public static async Task<List<Diagnostic>> DiagnoseWithoutReferences(string source)
        => await Analyze(CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(source)], [], new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

    private static async Task<List<Diagnostic>> Run(string source, OutputKind kind, IEnumerable<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty, () => "The probe compiles:\n" + string.Join("\n", errors) + "\n" + source);
        return await Analyze(compilation);
    }

    private static async Task<List<Diagnostic>> Analyze(Compilation compilation)
    {
        var diagnostics = await compilation.WithAnalyzers([new SyncQueryExecutionAnalyzer()]).GetAnalyzerDiagnosticsAsync();
        return diagnostics.OrderBy(d => d.Location.SourceSpan.Start).ToList();
    }
}
