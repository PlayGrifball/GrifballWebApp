using System.Collections.Immutable;
using System.Reflection;
using GrifballWebApp.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// The repository's analyzer settings, run on a small compilation: <see cref="EfAddSuppressor"/> silences SonarAnalyzer's S6966
/// on EF Core's Add and AddRange only, and BannedSymbols.txt bans every AddAsync and AddRangeAsync (RS0030).
/// </summary>
[TestFixture]
public class AnalyzerTests
{
    private const string Probe = """
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;

        public class Thing { public int Id { get; set; } }

        public class ProbeContext : DbContext { public DbSet<Thing> Things => Set<Thing>(); }

        public class Probe
        {
            public async Task Tracks(ProbeContext context)
            {
                await Task.Yield();
                context.Things.Add(new Thing()); // set.Add
                context.Things.AddRange(new Thing(), new Thing()); // set.AddRange
                context.Add(new Thing()); // context.Add
                context.AddRange(new Thing()); // context.AddRange
                context.SaveChanges(); // stays
                _ = context.Things.ToList(); // stays
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

    private static readonly string[] Tracking = ["set.Add", "set.AddRange", "context.Add", "context.AddRange"];
    private static readonly string[] Others = ["stays", "stays"];

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

    /// <summary>Reports S6966 on every call, as a stand-in that raises it on Add and AddRange too.</summary>
#pragma warning disable RS1036, RS1038, RS1041, RS2008 // A test's stand-in, run by the test itself, never by a compiler
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class EveryCall : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor S6966 = new("S6966", "Awaitable method should be used", "Await the async form", "Code Smell", DiagnosticSeverity.Warning, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [S6966];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(c => c.ReportDiagnostic(Diagnostic.Create(S6966, c.Node.GetLocation())), Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
        }
    }
#pragma warning restore RS1036, RS1038, RS1041, RS2008

    /// <summary>The probe's diagnostics, suppressed ones included, each with the comment that ends its line.</summary>
    private static async Task<List<(string Id, bool Suppressed, string Comment)>> Analyze(bool everyCall = false)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(DbContext).Assembly.Location)
            .Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText(Probe)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["S6966"] = ReportDiagnostic.Warn, ["RS0030"] = ReportDiagnostic.Error }));
        Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty, "The probe compiles");

        var sonar = AnalyzersFor("S6966", [Path.Combine(PackagePath("SonarAnalyzerPackage"), "analyzers", "SonarAnalyzer.CSharp.dll")]).ToList();
        var banned = AnalyzersFor("RS0030", Directory.GetFiles(Path.Combine(PackagePath("BannedApiAnalyzersPackage"), "analyzers", "dotnet"), "*.dll", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}vb{Path.DirectorySeparatorChar}"))).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sonar, Is.Not.Empty, "SonarAnalyzer's S6966");
            Assert.That(banned, Is.Not.Empty, "The banned API analyzer");
        }

        var options = new AnalyzerOptions([new FileText(Path.Combine(AppContext.BaseDirectory, "BannedSymbols.txt"))]);
        var analyzers = (everyCall ? [new EveryCall()] : sonar).Concat(banned).Append(new EfAddSuppressor()).ToImmutableArray();
        var diagnostics = await compilation
            .WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(options, null, concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync();
        return diagnostics
            .Where(d => d.Id is "S6966" or "RS0030")
            .Select(d =>
            {
                var line = d.Location.SourceTree!.GetText().Lines[d.Location.GetLineSpan().StartLinePosition.Line].ToString();
                return (d.Id, d.IsSuppressed, line[(line.LastIndexOf("// ", StringComparison.Ordinal) + 3)..]);
            })
            .ToList();
    }

    [Test]
    public async Task SonarsS6966_IsRaisedOnSaveChangesAndToList_NotOnAddOrAddRange_AndTheAsyncAddsAreBanned()
    {
        var diagnostics = await Analyze();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics.Where(d => d.Id == "S6966" && !d.Suppressed).Select(d => d.Comment), Is.EquivalentTo(Others), "Raised on the other calls, never on Add or AddRange");
            Assert.That(diagnostics.Where(d => d.Id == "RS0030").Select(d => d.Comment), Is.EquivalentTo(Enumerable.Repeat("banned", 7)),
                "Every AddAsync and AddRangeAsync overload is banned");
        }
    }

    [Test]
    public async Task TheSuppressor_SilencesS6966_OnEfsAddAndAddRange_Only()
    {
        // A stand-in raises S6966 on every call of the probe's tracking method: the suppressor takes only EF's adds
        var s6966 = (await Analyze(everyCall: true)).Where(d => d.Id == "S6966" && d.Comment is not "banned").ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(s6966.Where(d => d.Suppressed).Select(d => d.Comment), Is.EquivalentTo(Tracking), "Silenced on EF's Add and AddRange");
            Assert.That(s6966.Where(d => !d.Suppressed).Select(d => d.Comment), Is.SupersetOf(Others), "Still raised elsewhere");
        }
    }

    [Test]
    public void IsEfAdd_KnowsEfsAddsFromOtherMethodsOfTheSameName()
    {
        var compilation = CSharpCompilation.Create("Probe", [CSharpSyntaxTree.ParseText("public class Other { public void Add(object o) { } }")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location)]);
        var other = compilation.GetTypeByMetadataName("Other")!.GetMembers("Add").OfType<IMethodSymbol>().Single();
        var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext")!;
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1")!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(EfAddSuppressor.IsEfAdd(other, compilation), Is.False);
            Assert.That(dbContext.GetMembers("Add").OfType<IMethodSymbol>().All(m => EfAddSuppressor.IsEfAdd(m, compilation)), Is.True);
            Assert.That(dbSet.GetMembers("AddRange").OfType<IMethodSymbol>().All(m => EfAddSuppressor.IsEfAdd(m, compilation)), Is.True);
            Assert.That(dbContext.GetMembers("SaveChanges").OfType<IMethodSymbol>().Any(m => EfAddSuppressor.IsEfAdd(m, compilation)), Is.False);
            Assert.That(EfAddSuppressor.IsEfAdd(other, CSharpCompilation.Create("Empty")), Is.False, "No EF Core referenced");
        }
    }
}
