using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Test;

// GetAsync isn't covered here: its team projection (no AsSplitQuery, several nested collections - two from
// TeamRecord.Of, one for ChampionshipTiers, plus PlayedPlayoffs' Any() checks) needs the SQL APPLY operation on
// SQLite ("Translating this query requires the SQL APPLY operation, which is not supported on SQLite"), which only
// Infrastructure's DbContextOptionsBuilder provides (see UserManagementServiceUnitTests' note on the same gap).
// FakeApplicationDbContext doesn't have it. GrifballWebApp.Integration.Test covers GetAsync against the real
// providers.
[TestFixture]
internal sealed class DeploymentHistoryServiceUnitTests
{
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
}
