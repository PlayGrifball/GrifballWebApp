using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class DeploymentHistoryServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private DeploymentHistoryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new DeploymentHistoryService(Context, _time);
    }

    [TestCase(SeasonFormat.LeagueSeason, true, false, false, DeploymentFinish.Champion)]
    [TestCase(SeasonFormat.LeagueSeason, true, false, true, DeploymentFinish.Champion)]
    [TestCase(SeasonFormat.Tournament, false, false, true, DeploymentFinish.Eliminated)]
    [TestCase(SeasonFormat.Tournament, false, false, false, DeploymentFinish.InProgress)]
    [TestCase(SeasonFormat.LeagueSeason, false, true, false, DeploymentFinish.Playoffs)]
    [TestCase(SeasonFormat.LeagueSeason, false, false, true, DeploymentFinish.RegularSeason)]
    [TestCase(SeasonFormat.LeagueSeason, false, false, false, DeploymentFinish.InProgress)]
    public void FinishOf_ReturnsTheBestTheTeamDid(SeasonFormat format, bool champion, bool playedPlayoffs, bool seasonEnded, DeploymentFinish expected)
    {
        var finish = DeploymentHistoryService.FinishOf(format, champion, playedPlayoffs, seasonEnded);

        Assert.That(finish, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetAsync_NoSuchUser_ReturnsNull()
    {
        var result = await _service.GetAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetAsync_UserWithNoTeams_ReturnsEmptyList()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(1);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetAsync_PlayerOnATeam_ReturnsItsDeployment()
    {
        var season = Season.Seed(seasonName: "S1", seasonID: 1);
        season.Schedule(Now.AddDays(-60), Now.AddDays(-50), Now.AddDays(-40), Now.AddDays(-30), Now.AddDays(-1)); // ended
        Context.Seasons.Add(season);
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        var team = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 10);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Context.Add(TeamPlayer.Seed(teamID: 10, userID: 1, draftCaptainOrder: 1));
        await Context.SaveChangesAsync();
        team.SetCaptain(1);
        await Context.SaveChangesAsync();

        var result = await _service.GetAsync(1);

        Assert.That(result, Has.Count.EqualTo(1));
        var deployment = result![0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(deployment.TeamID, Is.EqualTo(10));
            Assert.That(deployment.TeamName, Is.EqualTo("Reclaimers"));
            Assert.That(deployment.IsCaptain, Is.True);
            Assert.That(deployment.Finish, Is.EqualTo(DeploymentFinish.RegularSeason));
        }
    }
}
