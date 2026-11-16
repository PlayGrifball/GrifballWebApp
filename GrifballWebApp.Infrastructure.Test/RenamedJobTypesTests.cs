using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Extensibility;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The job types' names from before they moved to GrifballWebApp.Infrastructure, as an older pod stores them during a
/// rolling update, still load: Quartz's job store reads JOB_CLASS_NAME with the container's type loader.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RenamedJobTypesTests
{
    private static ITypeLoader TypeLoader(ServiceProvider provider) => provider.GetRequiredService<ITypeLoader>();

    private static ServiceProvider Jobs(Action<RecurringJobs>? recurring = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddJobs(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["ConnectionStrings:GrifballWebApp"] = "Data Source=renamed-jobs.db",
        }).Build(), recurring ?? (_ => { }));
        return services.BuildServiceProvider();
    }

    [TestCase("GrifballWebApp.Server.Matchmaking.MatchmakingQueueJob, GrifballWebApp.Server", typeof(GrifballWebApp.Infrastructure.Matchmaking.MatchmakingQueueJob))]
    [TestCase("GrifballWebApp.Server.Events.SeasonEventsJob, GrifballWebApp.Server", typeof(GrifballWebApp.Infrastructure.Events.SeasonEventsJob))]
    [TestCase("GrifballWebApp.Server.DatabaseBackup.DatabaseBackupJob, GrifballWebApp.Server", typeof(GrifballWebApp.Infrastructure.DatabaseBackup.DatabaseBackupJob))]
    [TestCase("GrifballWebApp.Server.SeasonMatchPage.ActiveSeasonPullJob, GrifballWebApp.Server", typeof(GrifballWebApp.Infrastructure.SeasonMatchPage.ActiveSeasonPullJob))]
    public void AnOldStoredName_LoadsTheMovedType(string stored, Type moved)
    {
        using var provider = Jobs();
        Assert.That(TypeLoader(provider).LoadType(stored), Is.EqualTo(moved));
    }

    [Test]
    public void EveryOldName_LoadsAQuartzJobInInfrastructure_UnderTheSameName()
    {
        using var provider = Jobs();
        var loader = TypeLoader(provider);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(RenamedJobTypes.FromServer, Has.Count.EqualTo(12));
            foreach (var (stored, moved) in RenamedJobTypes.FromServer)
            {
                Assert.That(loader.LoadType(stored), Is.EqualTo(moved), stored);
                Assert.That(moved.GetInterfaces(), Does.Contain(typeof(IJob)), stored);
                Assert.That(stored, Is.EqualTo($"{moved.FullName!.Replace("GrifballWebApp.Infrastructure.", "GrifballWebApp.Server.")}, GrifballWebApp.Server"));
            }
        }
    }

    [Test]
    public void ARenameDeclaredWithTheJobs_LoadsItsType()
    {
        // A project above Infrastructure (GrifballWebApp.Historical) declares its own jobs' old names
        using var provider = Jobs(jobs => jobs.Renamed("Old.Place.MatchmakingQueueJob, Old.Place", typeof(GrifballWebApp.Infrastructure.Matchmaking.MatchmakingQueueJob)));

        Assert.That(TypeLoader(provider).LoadType("Old.Place.MatchmakingQueueJob, Old.Place"), Is.EqualTo(typeof(GrifballWebApp.Infrastructure.Matchmaking.MatchmakingQueueJob)));
    }

    [Test]
    public void TheNewNames_StillLoad()
    {
        using var provider = Jobs();
        var type = typeof(GrifballWebApp.Infrastructure.Matchmaking.MatchmakingQueueJob);
        Assert.That(TypeLoader(provider).LoadType($"{type.FullName}, {type.Assembly.GetName().Name}"), Is.EqualTo(type));
    }
}
