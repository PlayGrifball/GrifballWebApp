using System.Reflection;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Infrastructure.Jobs;
using Quartz;

namespace GrifballWebApp.Architecture.Test;

/// <summary>
/// Guards <see cref="JobsService.MergeCapableJobs"/> (#566, the other half of #549's default-deny): every job type
/// whose constructor - or one of its own dependencies', followed the same way, recursively - takes
/// <see cref="IUserMergeService"/> must be on that list, or a token could run it through <c>JobsController.Run</c>
/// and reach the merge service with no request of its own, the gap <see cref="JobChange.DeniedToAccessToken"/>'s own
/// comment describes.
///
/// Traced the way the #549 review did it by hand: a dependency named by an interface is followed to every concrete
/// type in the solution's own assemblies (Domain, Application, Infrastructure, Historical - a job never depends on
/// the Server) that implements it, not to a real DI container, since building one needs a database connection this
/// test doesn't have. More than one implementation reaching it is enough to flag the job: favouring one wrongly
/// caught (it only costs an access token the job, never a person) over one missed.
///
/// A dependency reachable only off this graph - a database queue a separate hosted service drains later, with no
/// constructor link at all, the way <c>historical-refresh</c> actually reaches the merge service - isn't found this
/// way and still has to be listed by hand (<see cref="JobsService.MergeCapableJobs"/>'s own comment says so, and
/// <see cref="EveryJobThatReachesTheMergeService_IsOnTheList"/>'s sanity check below only confirms that job is still
/// found and still listed, not that the graph is how it's reached). This test only ever adds a job the graph does
/// find; it never removes the need to look for one it can't.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MergeCapableJobsTests
{
    private static readonly Type[] OwnTypes =
    [
        .. typeof(GrifballWebApp.Domain.Shared.AggregateRootAttribute).Assembly.GetTypes(),
        .. typeof(IUserMergeService).Assembly.GetTypes(),
        .. typeof(JobsService).Assembly.GetTypes(),
        .. typeof(GrifballWebApp.Historical.Worker.HistoricalRefreshJob).Assembly.GetTypes(),
    ];

    private static IEnumerable<Type> JobTypes() => OwnTypes.Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

    /// <summary>A job's key the way the app itself computes it: a recurring job's own <c>Key</c>, or, with none, an
    /// on-demand one's (<see cref="OnDemandJobs.JobKeyOf"/>) - never a key this test names itself, so a renamed job
    /// is still matched correctly.</summary>
    private static JobKey KeyOf(Type job)
        => (JobKey?)job.GetField("Key", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) ?? OnDemandJobs.JobKeyOf(job);

    private static JobKey[] MergeCapableJobs()
        => (JobKey[])typeof(JobsService).GetField("MergeCapableJobs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    /// <summary>Every constructor's parameters, not just the one DI would pick: missing a dependency because of which
    /// constructor it would have chosen is worse than looking at one it wouldn't.</summary>
    private static IEnumerable<Type> Dependencies(Type type)
        => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);

    private static IEnumerable<Type> Implementations(Type @interface, IReadOnlyCollection<Type> universe)
        => universe.Where(t => !t.IsAbstract && !t.IsInterface && @interface.IsAssignableFrom(t));

    /// <summary>
    /// Whether <paramref name="type"/> is <see cref="IUserMergeService"/>, or something reachable from its
    /// constructors is: recursively, through <paramref name="universe"/> only (a framework or third-party type -
    /// <c>ILogger&lt;T&gt;</c>, <c>IConfiguration</c>, <c>TimeProvider</c>, EF's <c>DbContextOptions&lt;T&gt;</c> -
    /// is never the merge service and isn't walked into). <paramref name="visited"/> both stops a cycle (two types
    /// depending on each other, neither reaching it) and memoises: reachability doesn't depend on the path taken to a
    /// type, so a type already ruled out needn't be walked again.
    /// </summary>
    private static bool Reaches(Type type, IReadOnlyCollection<Type> universe, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return false;
        if (type == typeof(IUserMergeService))
            return true;
        if (!universe.Contains(type))
            return false;

        foreach (var dependency in Dependencies(type))
        {
            if (dependency == typeof(IUserMergeService))
                return true;
            IEnumerable<Type> candidates = dependency.IsInterface ? Implementations(dependency, universe) : [dependency];
            if (candidates.Any(c => Reaches(c, universe, visited)))
                return true;
        }
        return false;
    }

    [Test]
    public void EveryJobThatReachesTheMergeService_IsOnTheList()
    {
        var mergeCapable = MergeCapableJobs();
        var jobs = JobTypes().ToList();
        var missing = jobs.Where(j => Reaches(j, OwnTypes, [])).Select(KeyOf).Where(k => !mergeCapable.Contains(k)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jobs, Is.Not.Empty);
            // Sanity: the known merge-capable job is still there under the key the list itself uses, so a rename that
            // broke KeyOf's matching (rather than an actual gap) can't hide as "nothing missing".
            Assert.That(jobs.Select(KeyOf), Does.Contain(new JobKey("historical-refresh", RecurringJobs.Group)));
            Assert.That(missing, Is.Empty, "add it to JobsService.MergeCapableJobs");
        }
    }

    /// <summary>Takes the merge service itself.</summary>
    private sealed class DirectFixture
    {
        public DirectFixture(IUserMergeService merge) { }
    }

    /// <summary>Takes something that takes the merge service - two steps away.</summary>
    private sealed class IndirectFixture
    {
        public IndirectFixture(DirectFixture inner) { }
    }

    /// <summary>An interface with two implementations; only one of them reaches the merge service, which must be enough.</summary>
    private interface IFixtureDependency;

    private sealed class ReachingImplementation : IFixtureDependency
    {
        public ReachingImplementation(DirectFixture inner) { }
    }

    private sealed class HarmlessImplementation : IFixtureDependency
    {
        public HarmlessImplementation(string notATrackedType) { }
    }

    private sealed class ViaInterfaceFixture
    {
        public ViaInterfaceFixture(IFixtureDependency dependency) { }
    }

    /// <summary>Its only dependency is outside the universe (as a job's <c>ILogger&lt;T&gt;</c> or <c>TimeProvider</c> is): never followed.</summary>
    private sealed class UnrelatedFixture
    {
        public UnrelatedFixture(string notATrackedType) { }
    }

    /// <summary>Two fixtures depending on each other, neither reaching the merge service: the cycle must terminate, not recurse forever.</summary>
    private sealed class CycleFixtureA
    {
        public CycleFixtureA(CycleFixtureB b) { }
    }

    private sealed class CycleFixtureB
    {
        public CycleFixtureB(CycleFixtureA a) { }
    }

    private static readonly Type[] FixtureUniverse =
    [
        typeof(DirectFixture), typeof(IndirectFixture), typeof(IFixtureDependency), typeof(ReachingImplementation),
        typeof(HarmlessImplementation), typeof(ViaInterfaceFixture), typeof(UnrelatedFixture), typeof(CycleFixtureA), typeof(CycleFixtureB),
    ];

    [Test]
    public void Reaches_ATypeThatTakesTheMergeServiceDirectly_IsTrue()
        => Assert.That(Reaches(typeof(DirectFixture), FixtureUniverse, []), Is.True);

    [Test]
    public void Reaches_ATypeThatTakesSomethingThatTakesTheMergeService_IsTrue()
        => Assert.That(Reaches(typeof(IndirectFixture), FixtureUniverse, []), Is.True);

    [Test]
    public void Reaches_AnInterfaceParameter_FollowsEveryImplementation()
        => Assert.That(Reaches(typeof(ViaInterfaceFixture), FixtureUniverse, []), Is.True, "ReachingImplementation alone makes it reachable");

    [Test]
    public void Reaches_NothingInTheDependencyGraphUsesIt_IsFalse()
        => Assert.That(Reaches(typeof(UnrelatedFixture), FixtureUniverse, []), Is.False);

    [Test]
    public void Reaches_ACycleThatNeverReachesIt_IsFalseAndTerminates()
        => Assert.That(Reaches(typeof(CycleFixtureA), FixtureUniverse, []), Is.False);
}
