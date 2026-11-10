using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Infrastructure.Historical;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>XboxPlaceholderTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxPlaceholderUnitTests
{
    [Test]
    public void TheJob_IsHourly_FromTheWorkersFirstStart()
    {
        var now = DateTimeOffset.UtcNow;
        var trigger = (Quartz.ISimpleTrigger)XboxPlaceholderJob.Definition.DefaultTrigger(now);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((trigger.RepeatInterval, trigger.RepeatCount, trigger.StartTimeUtc), Is.EqualTo((TimeSpan.FromHours(1), -1, now)));
            Assert.That(trigger.JobKey, Is.EqualTo(XboxPlaceholderJob.Key));
        }
    }
}
