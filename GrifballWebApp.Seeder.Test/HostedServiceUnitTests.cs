using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using NSubstitute;

namespace GrifballWebApp.Seeder.Test;

/// <summary>HostedService's unit tests: no database, host or file. SeedTestSeasonAsync is internal; called directly
/// (InternalsVisibleTo). ExecuteAsync and the rest of Old() (EnsureDeleted/Migrate: EF's actual provider, not a
/// virtual member a test double can stand in for) stay in GrifballWebApp.Integration.Test, a real database.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HostedServiceUnitTests
{
    private static GrifballContext NewContext(out FakeDbSet<Season> seasons, out FakeDbSet<User> users, out FakeDbSet<SeasonSignup> signups)
    {
        seasons = new();
        users = new();
        signups = new();
        var context = Substitute.For<GrifballContext>();
        context.Seasons = seasons;
        context.Users = users;
        context.SeasonSignups = signups;
        context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(1));
        return context;
    }

    [Test]
    public async Task SeedTestSeasonAsync_AddsTheSysAdminSixCaptainsAndTwentyFourPlayers()
    {
        var context = NewContext(out var seasons, out var users, out var signups);

        await HostedService.SeedTestSeasonAsync(context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Items, Has.Count.EqualTo(1));
            Assert.That(seasons.Items[0].SeasonName, Is.EqualTo("Test Season 2024"));
            // The sysadmin, 6 captains and 24 players
            Assert.That(users.Items, Has.Count.EqualTo(31));
            Assert.That(signups.Items, Has.Count.EqualTo(30), "Only the 6 captains and 24 players sign up; the sysadmin doesn't");
            Assert.That(signups.Items.Count(s => s.WillCaptain), Is.EqualTo(6));
            Assert.That(signups.Items.Where(s => s.WillCaptain).Select(s => s.TeamName), Has.All.EndWith("'s Team"));
            Assert.That(signups.Items.Where(s => !s.WillCaptain).All(s => s.TeamName is null), Is.True, "Players don't choose a team name");
        }
    }
}
