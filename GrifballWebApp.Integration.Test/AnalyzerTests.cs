using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// BannedSymbols.txt, run on a small compilation with the banned API analyzer: every overload of EF Core's AddAsync and
/// AddRangeAsync is banned (RS0030), and Add and AddRange are not.
/// </summary>
[TestFixture]
public class AnalyzerTests
{
    private const string Probe = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;

        public class Thing { public int Id { get; set; } }

        public class ProbeContext : DbContext { public DbSet<Thing> Things => Set<Thing>(); }

        public class Probe
        {
            public void Tracks(ProbeContext context)
            {
                context.Things.Add(new Thing()); // allowed
                context.Things.AddRange(new Thing(), new Thing()); // allowed
                context.Add(new Thing()); // allowed
                context.AddRange(new Thing()); // allowed
            }

            public async Task Banned(ProbeContext context, IEnumerable<Thing> things, IEnumerable<object> objects)
            {
                await context.AddAsync((object)new Thing()); // banned
                await context.AddAsync(new Thing()); // banned
                await context.AddRangeAsync(new Thing()); // banned
                await context.AddRangeAsync(objects); // banned
                await context.Things.AddAsync(new Thing()); // banned
                await context.Things.AddRangeAsync(new Thing()); // banned
                await context.Things.AddRangeAsync(things); // banned
            }
        }
        """;

    private static string PackagePath(string key)
        => typeof(AnalyzerTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == key).Value!;

    /// <summary>The analyzers in the package's assemblies that report <paramref name="id"/>.</summary>
    private static IEnumerable<DiagnosticAnalyzer> AnalyzersFor(string id, IEnumerable<string> assemblies)
    {
        foreach (var path in assemblies)
        {
            Type?[] types;
            try
            {
                types = Assembly.LoadFrom(path).GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types;
            }
            foreach (var type in types.OfType<Type>().Where(t => !t.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) is not null))
            {
                if (type.GetCustomAttribute<DiagnosticAnalyzerAttribute>()?.Languages.Contains(LanguageNames.CSharp) is not true)
                    continue;
                var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
                if (analyzer.SupportedDiagnostics.Any(d => d.Id == id))
                    yield return analyzer;
            }
        }
    }

    private sealed class FileText(string path) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(File.ReadAllText(path));
    }

    [Test]
    public async Task EveryAsyncAdd_IsBanned_AndAddAndAddRangeAreNot()
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(DbContext).Assembly.Location)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(Probe)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["RS0030"] = ReportDiagnostic.Error }));
        Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "The probe compiles");

        var banned = AnalyzersFor("RS0030", Directory.GetFiles(Path.Combine(PackagePath("BannedApiAnalyzersPackage"), "analyzers", "dotnet"), "*.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}vb{Path.DirectorySeparatorChar}"))).ToImmutableArray();
        Assert.That(banned, Is.Not.Empty, "The banned API analyzer");

        var options = new AnalyzerOptions([new FileText(Path.Combine(AppContext.BaseDirectory, "BannedSymbols.txt"))]);
        var diagnostics = await compilation.WithAnalyzers(banned, options).GetAnalyzerDiagnosticsAsync();
        var comments = diagnostics
            .Where(d => d.Id == "RS0030")
            .Select(d =>
            {
                var line = d.Location.SourceTree!.GetText().Lines[d.Location.GetLineSpan().StartLinePosition.Line].ToString();
                return line[(line.LastIndexOf("// ", StringComparison.Ordinal) + 3)..];
            });

        Assert.That(comments, Is.EquivalentTo(Enumerable.Repeat("banned", 7)), "Every AddAsync and AddRangeAsync overload is banned, nothing else");
    }
}
