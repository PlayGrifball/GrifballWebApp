using GrifballWebApp.Domain.League;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.Test;

/// <summary>DeploymentHistoryTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DeploymentHistoryUnitTests
{
    [Test]
    public void TeamRecordSelect_NeedsAMemberInitializer()
    {
        Assert.Throws<ArgumentException>(() => TeamRecord.Select<TeamRecord>(t => null!));
    }

    [TestCase(SeasonFormat.LeagueSeason, true, true, true, DeploymentFinish.Champion)]
    [TestCase(SeasonFormat.LeagueSeason, true, false, false, DeploymentFinish.Champion)]
    [TestCase(SeasonFormat.LeagueSeason, false, true, false, DeploymentFinish.Playoffs)]
    [TestCase(SeasonFormat.LeagueSeason, false, true, true, DeploymentFinish.Playoffs)]
    [TestCase(SeasonFormat.LeagueSeason, false, false, true, DeploymentFinish.RegularSeason)]
    [TestCase(SeasonFormat.LeagueSeason, false, false, false, DeploymentFinish.InProgress)]
    [TestCase(SeasonFormat.Tournament, true, true, true, DeploymentFinish.Champion)]
    [TestCase(SeasonFormat.Tournament, false, true, true, DeploymentFinish.Eliminated)]
    [TestCase(SeasonFormat.Tournament, false, false, true, DeploymentFinish.Eliminated)]
    [TestCase(SeasonFormat.Tournament, false, true, false, DeploymentFinish.InProgress)]
    [TestCase(SeasonFormat.Tournament, false, false, false, DeploymentFinish.InProgress)]
    public void FinishOf_TheBestTheTeamDid(SeasonFormat format, bool champion, bool playedPlayoffs, bool seasonEnded, DeploymentFinish expected)
    {
        Assert.That(DeploymentHistoryService.FinishOf(format, champion, playedPlayoffs, seasonEnded), Is.EqualTo(expected));
    }
}
