using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Scheduler;

namespace GrifballWebApp.Application.Test;

/// <summary>ScheduleServiceTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScheduleServiceUnitTests
{
    [Test]
    public void LeagueTimeZone_IsUsEastern_WithDaylightSaving()
    {
        var zone = ScheduleService.LeagueTimeZone();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(zone.GetUtcOffset(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo(TimeSpan.FromHours(-5)));
            Assert.That(zone.GetUtcOffset(new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)), Is.EqualTo(TimeSpan.FromHours(-4)));
        }
    }
}
