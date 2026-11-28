using System.Reflection;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Historical;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// Guards <see cref="JobsService.MergeCapableJobs"/> (#566, the other half of #549's default-deny), two different
/// ways for the two different ways a job runs:
///
/// <list type="bullet">
/// <item>A <b>recurring job</b> (<see cref="RecurringJobs.All"/>) is run by <c>JobsController.Run</c> -&gt;
/// <see cref="JobsService.RunNow"/>, which checks <see cref="JobsService.MergeCapableJobs"/> before it does anything
/// else: every one that reaches a merge type must be on that list.</item>
/// <item>An <b>on-demand job</b> (<see cref="RecurringJobs.OnDemand"/>) is queued through
/// <c>OnDemandJobs.Enqueue</c> directly from its own controller (now <c>[AllowAccessToken]</c> on several of them,
/// #566) - never through <see cref="JobsService.RunNow"/>. Listing one on <see cref="JobsService.MergeCapableJobs"/>
/// would protect nothing, so none of them may reach a merge type at all.</item>
/// </list>
///
/// "Reaches a merge type" is read from the compiled code with Mono.Cecil (<see cref="Dependencies"/>, already used by
/// <see cref="LayerTests"/>): every type a job type's own code names - fields, properties, method signatures and
/// method bodies' IL, generic arguments included - followed the same way through whatever that names, recursively,
/// through the solution's own assemblies (Domain, Application, Infrastructure, Historical, and this test project
/// itself, for the fixtures below - a job never depends on the Server). An interface or abstract type is followed to
/// every concrete type assignable to it that this test can see (reflection, not Cecil, finds those); more than one
/// candidate reaching it is enough, which only risks a job being asked to go on the list that, at runtime, wouldn't
/// actually need to - never the reverse. Reading the IL (not just a constructor's parameters) also catches a merge
/// type reached through <c>IServiceProvider.GetRequiredService&lt;IUserMergeService&gt;()</c>, a
/// <c>Lazy&lt;IUserMergeService&gt;</c> field, or any other wrapper a plain constructor walk would miss.
///
/// A merge type reached only off this graph entirely - a database queue a separate hosted service drains later, with
/// no reference from the job's own type to anything of the merge service's at all, the way <c>historical-refresh</c>
/// actually reaches it (<see cref="GrifballWebApp.Historical.Import.HistoricalImportJobs"/>'s queue, drained by
/// <see cref="GrifballWebApp.Historical.Worker.HistoricalImportWorker"/>) - isn't found this way and still has to be
/// listed by hand, as <see cref="JobsService.MergeCapableJobs"/>'s own comment already says. This test's own sanity
/// check only confirms that job is still found among the real recurring jobs and still on the list; it doesn't claim
/// the graph is how that job reaches the merge service.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MergeCapableJobsTests
{
    /// <summary>One type from each assembly this test's graph walk covers, read once for both the Cecil pass
    /// (<see cref="BuildEdges"/>) and the reflection pass (<see cref="OwnTypes"/>). This test project's own assembly
    /// is included so the fixture types below (compiled the same way a real job is) go through the same graph the
    /// real jobs do, not a hand-rolled stand-in.</summary>
    private static readonly Type[] RepresentativeTypes =
    [
        typeof(GrifballWebApp.Domain.Shared.AggregateRootAttribute),
        typeof(IUserMergeService),
        typeof(JobsService),
        typeof(GrifballWebApp.Historical.Worker.HistoricalRefreshJob),
        typeof(MergeCapableJobsTests),
    ];

    private static readonly Type[] OwnTypes = [.. RepresentativeTypes.SelectMany(t => t.Assembly.GetTypes())];

    /// <summary>Reaching any one of these (the interface, or either concrete class a token's merge path actually
    /// runs through) counts as reaching the merge service: <see cref="UserMergeService.Merge"/> and
    /// <see cref="UserMergeAuditService.Undo"/> are the two places <see cref="GrifballWebApp.Server.Mcp.DenyAccessTokenAttribute"/>'s
    /// own comment says a merge is actually refused, not the interface alone.</summary>
    private static readonly HashSet<string> MergeTypeNames = new(StringComparer.Ordinal)
    {
        typeof(IUserMergeService).FullName!,
        typeof(UserMergeService).FullName!,
        typeof(UserMergeAuditService).FullName!,
    };

    /// <summary>Every reference a type in <see cref="RepresentativeTypes"/>' assemblies makes to another type of
    /// theirs, keyed by the referencing type's full name: built once, from the compiled assemblies (Mono Cecil),
    /// not re-read per node visited while walking the graph.</summary>
    private static readonly Dictionary<string, List<string>> EdgesByType = BuildEdges();

    private static Dictionary<string, List<string>> BuildEdges()
    {
        var edges = new List<(string From, string To)>();
        foreach (var representative in RepresentativeTypes)
        {
            using var module = Dependencies.ModuleOf(representative);
            edges.AddRange(Dependencies.Of(module).Select(d => (d.From.FullName, d.To.FullName)));
        }
        return edges.GroupBy(e => e.From, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
    }

    /// <summary>Every interface or abstract type in <see cref="OwnTypes"/>, to the concrete types of theirs assignable
    /// to it: built once (reflection's own <c>IsAssignableFrom</c>), not with a fresh scan of every type for every
    /// node the graph walk visits.</summary>
    private static readonly Dictionary<string, List<string>> ImplementationsOf = BuildImplementations();

    private static Dictionary<string, List<string>> BuildImplementations()
    {
        var concrete = OwnTypes.Where(t => !t.IsInterface && !t.IsAbstract).ToList();
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var basis in OwnTypes.Where(t => t.IsInterface || t.IsAbstract))
        {
            var implementations = concrete.Where(basis.IsAssignableFrom).Select(t => t.FullName!).ToList();
            if (implementations.Count > 0)
                map[basis.FullName!] = implementations;
        }
        return map;
    }

    /// <summary>
    /// Whether <paramref name="typeName"/> is one of <see cref="MergeTypeNames"/>, or something reachable from it is:
    /// recursively, through <paramref name="edgesByType"/> (what each type names) and <paramref name="implementationsOf"/>
    /// (an interface or abstract type followed to every one of its implementations - any one reaching it is enough).
    /// <paramref name="visited"/> both stops a cycle (two types depending on each other, neither reaching the merge
    /// service) and memoises: reachability doesn't depend on the path taken to a type, so one already ruled out
    /// needn't be walked again.
    /// </summary>
    private static bool Reaches(string typeName, IReadOnlyDictionary<string, List<string>> edgesByType,
        IReadOnlyDictionary<string, List<string>> implementationsOf, HashSet<string> visited)
    {
        if (!visited.Add(typeName))
            return false;
        if (MergeTypeNames.Contains(typeName))
            return true;
        if (!edgesByType.TryGetValue(typeName, out var references))
            return false;

        foreach (var reference in references)
        {
            if (MergeTypeNames.Contains(reference))
                return true;
            var candidates = implementationsOf.TryGetValue(reference, out var implementations) ? implementations : [reference];
            foreach (var candidate in candidates)
                if (Reaches(candidate, edgesByType, implementationsOf, visited))
                    return true;
        }
        return false;
    }

    /// <summary>The same walk, over this test's own real graph (<see cref="EdgesByType"/>, <see cref="ImplementationsOf"/>).</summary>
    private static bool Reaches(string typeName) => Reaches(typeName, EdgesByType, ImplementationsOf, []);

    private static JobKey[] MergeCapableJobs()
        => (JobKey[])typeof(JobsService).GetField("MergeCapableJobs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    /// <summary>
    /// The app's own jobs, registered the way it really registers them (<see cref="InfrastructureServiceCollectionExtensions.AddInfrastructure"/>
    /// with <see cref="HistoricalServiceCollectionExtensions.AddHistoricalJobs"/>, as <c>Program.cs</c> does), so this
    /// test's job list and keys are the real ones - never a type's own guessed-at static field, and never missing a
    /// job added since. A minimal in-memory configuration (Sqlite, no real connection ever opened: building an
    /// <see cref="IServiceProvider"/> doesn't start Quartz or touch a database) that also turns on the on-demand
    /// scratch-stop job, the one on-demand job that's otherwise only registered when a chart setting is present.
    /// </summary>
    private static readonly Lazy<RecurringJobs> RegisteredJobs = new(() =>
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:GrifballWebApp"] = "Data Source=merge-capable-jobs-tests.db",
            ["DatabaseBackup:Scratch:Deployment"] = "merge-capable-jobs-tests-scratch",
        }).Build();
        services.AddInfrastructure(configuration, discordOff: true, HistoricalServiceCollectionExtensions.AddHistoricalJobs);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<RecurringJobs>();
    });

    [Test]
    public void EveryRecurringJobThatReachesAMergeType_IsOnTheList()
    {
        var recurring = RegisteredJobs.Value.All;
        var mergeCapable = MergeCapableJobs();
        var missing = recurring.Where(j => Reaches(j.Type.FullName!)).Select(j => j.Job.Key).Where(k => !mergeCapable.Contains(k)).ToList();
        var historicalRefresh = new JobKey("historical-refresh", RecurringJobs.Group);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recurring, Is.Not.Empty);
            Assert.That(recurring.Select(j => j.Job.Key), Does.Contain(historicalRefresh), "sanity: the real registration still has this job");
            Assert.That(mergeCapable, Does.Contain(historicalRefresh),
                "historical-refresh reaches the merge service only through the import queue (HistoricalImportJobs, drained by HistoricalImportWorker), off this graph entirely - it must stay listed here by hand");
            Assert.That(missing, Is.Empty, "add it to JobsService.MergeCapableJobs");
        }
    }

    /// <summary>
    /// An on-demand job is queued through <c>OnDemandJobs.Enqueue</c>, called straight from its own controller, never
    /// through <see cref="JobsService.RunNow"/> - the only place <see cref="JobsService.MergeCapableJobs"/> is
    /// checked. Listing an on-demand job there would protect nothing, so none of them may reach a merge type at all.
    /// </summary>
    [Test]
    public void NoOnDemandJobReachesAMergeType()
    {
        var onDemand = RegisteredJobs.Value.OnDemand;
        var reaching = onDemand.Where(j => Reaches(j.Type.FullName!)).Select(j => j.Key.ToString()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(onDemand, Is.Not.Empty);
            Assert.That(reaching, Is.Empty,
                "an on-demand job is queued through OnDemandJobs.Enqueue, which JobsService.MergeCapableJobs never guards (only JobsService.RunNow checks it): listing it there would protect nothing, so it must not reach a merge type at all");
        }
    }

    [Test]
    public void MergeTypeNames_IncludesTheConcreteGuardedTypesTooNotJustTheInterface()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MergeTypeNames, Does.Contain(typeof(IUserMergeService).FullName));
            Assert.That(MergeTypeNames, Does.Contain(typeof(UserMergeService).FullName), "UserMergeService.Merge is one of the two places a merge is actually refused");
            Assert.That(MergeTypeNames, Does.Contain(typeof(UserMergeAuditService).FullName), "UserMergeAuditService.Undo is the other");
        }
    }

    // The algorithm itself, over small hand-built graphs: deterministic, so the "any implementation" case below can
    // put the non-reaching one first on purpose, which this test project's own real (reflection-ordered) types can't promise.

    private static readonly Dictionary<string, List<string>> NoEdges = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<string>> NoImplementations = new(StringComparer.Ordinal);

    [Test]
    public void Reaches_TheMergeTypeItself_IsTrueEvenWithNoEdges()
        => Assert.That(Reaches(typeof(IUserMergeService).FullName!, NoEdges, NoImplementations, []), Is.True);

    [Test]
    public void Reaches_ATypeThatReferencesTheMergeType_IsTrue()
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["A"] = [typeof(IUserMergeService).FullName!] };

        Assert.That(Reaches("A", edges, NoImplementations, []), Is.True);
    }

    [Test]
    public void Reaches_ATypeThatReferencesSomethingThatReferencesTheMergeType_IsTrue()
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["A"] = ["B"],
            ["B"] = [typeof(IUserMergeService).FullName!],
        };

        Assert.That(Reaches("A", edges, NoImplementations, []), Is.True);
    }

    /// <summary>An interface with two implementations, the non-reaching one listed first: proves the walk checks
    /// every implementation, not just the first one it finds.</summary>
    [Test]
    public void Reaches_AnInterfaceParameter_FollowsEveryImplementation_NotJustTheFirst()
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["ViaInterface"] = ["IDependency"],
            ["Harmless"] = ["System.String"],
            ["Reaching"] = [typeof(IUserMergeService).FullName!],
        };
        var implementations = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["IDependency"] = ["Harmless", "Reaching"] };

        Assert.That(Reaches("ViaInterface", edges, implementations, []), Is.True);
    }

    [Test]
    public void Reaches_NothingInTheGraphReachesTheMergeType_IsFalse()
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["Unrelated"] = ["System.String"] };

        Assert.That(Reaches("Unrelated", edges, NoImplementations, []), Is.False);
    }

    [Test]
    public void Reaches_ACycleThatNeverReachesTheMergeType_IsFalseAndTerminates()
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal) { ["A"] = ["B"], ["B"] = ["A"] };

        Assert.That(Reaches("A", edges, NoImplementations, []), Is.False);
    }

    // Patterns a plain constructor walk would miss, read correctly off the real IL of the fixture types below (this
    // test project's own assembly, included in RepresentativeTypes above, so these go through the real
    // EdgesByType/ImplementationsOf). They're declared outside this class, not nested in it: Mono.Cecil attributes a
    // nested type's own references to its outermost declaring type (Dependencies.Outermost), never to the nested
    // type itself, so a nested fixture's edges would show up under MergeCapableJobsTests - every other fixture's
    // edges mixed in - never under the fixture's own name the way EdgesByType is keyed. Top-level avoids that.

    [Test]
    public void Reaches_AGetRequiredServiceCallInAMethodBody_IsTrue()
        => Assert.That(Reaches(typeof(ServiceLocatorFixture).FullName!), Is.True);

    [Test]
    public void Reaches_ALazyFieldOfTheMergeType_IsTrue()
        => Assert.That(Reaches(typeof(LazyMergeFixture).FullName!), Is.True);

    [Test]
    public void Reaches_AnAbstractParameterWhoseSubclassReachesTheMergeType_IsTrue()
        => Assert.That(Reaches(typeof(AbstractParameterFixture).FullName!), Is.True);
}

/// <summary>Resolves the merge service from a service locator in its method body, not its constructor - the pattern
/// <c>HistoricalRefreshJob</c> doesn't use, but a future job reaching the merge service this way could. Static: never
/// actually called, only reflected on, and it touches no instance member.</summary>
internal sealed class ServiceLocatorFixture
{
    public static void Resolve(IServiceProvider services) => services.GetRequiredService<IUserMergeService>();
}

/// <summary>A field that only ever holds the merge service lazily: a constructor walk sees <c>Lazy&lt;T&gt;</c>'s own
/// constructor, never the <c>T</c> it was built with.</summary>
internal sealed class LazyMergeFixture
{
    private readonly Lazy<IUserMergeService> _merge = null!;
}

/// <summary>A base class with one subclass that reaches the merge service: a constructor parameter typed to the
/// abstract base alone never shows that, only following to the subclass does.</summary>
internal abstract class AbstractDependencyFixture;

internal sealed class ReachesViaBaseFixture : AbstractDependencyFixture
{
    public ReachesViaBaseFixture(IUserMergeService merge) { }
}

internal sealed class AbstractParameterFixture
{
    public AbstractParameterFixture(AbstractDependencyFixture dependency) { }
}
