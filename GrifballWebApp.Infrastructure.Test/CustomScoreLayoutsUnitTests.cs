using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Surprenant.Grunt.Models.HaloInfinite;
using Match = GrifballWebApp.Domain.Halo.Match;
using Team = Surprenant.Grunt.Models.HaloInfinite.Team;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Application.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>CustomScoreLayoutsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CustomScoreLayoutsUnitTests
{
    [Test]
    public void TheJob_IsHourly_InTheHaloPullsExecutionGroup()
    {
        var trigger = MatchVariantBackfillJob.Definition.DefaultTrigger(DateTimeOffset.UtcNow);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(MatchVariantBackfillJob.Key.Name, Is.EqualTo("match-variant-backfill"));
            Assert.That(((ISimpleTrigger)trigger).RepeatInterval, Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(typeof(MatchVariantBackfillJob).GetCustomAttributes(typeof(ExecutionGroupAttribute), false), Is.Not.Empty);
        }
    }

    [Test]
    public void TheJobsReport_SucceedsWhenHaloGaveEverything()
    {
        var report = MatchVariantBackfillJob.Report(new VariantBackfillResult(10, 10, 10, 0, []), 0);

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Succeeded, "10 of 10 matches given their game variant, 0 left")));
    }
}
