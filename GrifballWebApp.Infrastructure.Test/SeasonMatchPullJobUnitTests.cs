using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using System.Reflection;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>SeasonMatchPullJobTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchPullJobUnitTests
{
    [Test]
    public void TheScheduledPull_RunsEvery30Minutes_WithTheOtherHaloPulls()
    {
        var trigger = (ISimpleTrigger)ActiveSeasonPullJob.Definition.DefaultTrigger(DateTimeOffset.UtcNow);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(trigger.RepeatInterval, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(trigger.ExecutionGroup, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
            Assert.That(SeasonMatchPullJob.RunName(42), Is.EqualTo("season-match-42"));
        }
    }

    [Test]
    public void BothPulls_DeclareTheHaloGroup_SoAManualRunCountsAgainstIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(typeof(ActiveSeasonPullJob).GetCustomAttribute<ExecutionGroupAttribute>()?.Template, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
            Assert.That(typeof(SeasonMatchPullJob).GetCustomAttribute<ExecutionGroupAttribute>()?.Template, Is.EqualTo(JobsServiceCollectionExtensions.HaloMatchPulls));
        }
    }
}
