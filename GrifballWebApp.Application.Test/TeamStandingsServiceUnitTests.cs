using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamStandingsService against the fake IApplicationDbContext: every team's record, its one rule
/// (TeamRecord.Of), shared with TeamPageService (TeamHistoryServiceGetAsyncUnitTests covers it there).</summary>
[TestFixture]
internal sealed class TeamStandingsServiceUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private static readonly string[] ExpectedStandingsOrder = { "A", "B", "C" };
    private TeamStandingsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TeamStandingsService(Context);
    }

    private async Task SeedSeason(int seasonID = SeasonID)
        => await EnsureSeason(seasonID);

    private async Task EnsureSeason(int seasonID)
    {
        Context.Seasons.Add(Season.Seed(seasonName: $"Season {seasonID}", seasonID: seasonID));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetTeamStandings_NoTeams_ReturnsEmptyArray()
    {
        var result = await _service.GetTeamStandings(SeasonID);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetTeamStandings_TeamsWithoutMatches_ReturnsZeroRecords()
    {
        await SeedSeason();
        var team1 = Team.Seed(seasonID: SeasonID, teamName: "Alpha");
        var team2 = Team.Seed(seasonID: SeasonID, teamName: "Beta");
        Context.Teams.AddRange(team1, team2);
        await Context.SaveChangesAsync();

        var result = await _service.GetTeamStandings(SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Length.EqualTo(2));
            Assert.That(result[0].TeamName, Is.EqualTo("Alpha"));
            Assert.That(result[0].Wins, Is.Zero);
            Assert.That(result[0].Losses, Is.Zero);
            Assert.That(result[0].RecordFromSource, Is.False);
        }
    }

    [Test]
    public async Task GetTeamStandings_CountsWinsLossesAndForfeitsFromMatches()
    {
        await SeedSeason();
        var alpha = Team.Seed(seasonID: SeasonID, teamName: "Alpha");
        var beta = Team.Seed(seasonID: SeasonID, teamName: "Beta");
        Context.Teams.AddRange(alpha, beta);
        await Context.SaveChangesAsync();

        // Alpha wins at home, wins away, and loses by forfeiting at home: 2 wins, 1 loss
        var homeWin = SeasonMatch.Seed(SeasonID, alpha.TeamID, beta.TeamID, bestOf: 3).WonBy(SeasonMatchSide.Home);
        var awayWin = SeasonMatch.Seed(SeasonID, beta.TeamID, alpha.TeamID, bestOf: 3).WonBy(SeasonMatchSide.Away);
        var forfeit = SeasonMatch.Seed(SeasonID, alpha.TeamID, beta.TeamID, bestOf: 3).Forfeited(Forfeiter.Home);
        Context.SeasonMatches.AddRange(homeWin, awayWin, forfeit);
        await Context.SaveChangesAsync();

        var result = await _service.GetTeamStandings(SeasonID);

        var alphaRecord = result.Single(r => r.TeamName == "Alpha");
        var betaRecord = result.Single(r => r.TeamName == "Beta");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(alphaRecord.Wins, Is.EqualTo(2));
            Assert.That(alphaRecord.Losses, Is.EqualTo(1));
            Assert.That(betaRecord.Wins, Is.EqualTo(1));
            Assert.That(betaRecord.Losses, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task GetTeamStandings_OrdersByWinsDescendingThenLossesAscending()
    {
        await SeedSeason();
        var a = Team.Seed(seasonID: SeasonID, teamName: "A"); // 2 wins, 0 losses
        var b = Team.Seed(seasonID: SeasonID, teamName: "B"); // 2 wins, 1 loss
        var c = Team.Seed(seasonID: SeasonID, teamName: "C"); // 1 win, 0 losses
        Context.Teams.AddRange(a, b, c);
        await Context.SaveChangesAsync();

        Context.SeasonMatches.AddRange(
            SeasonMatch.Seed(SeasonID, a.TeamID, c.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(SeasonID, a.TeamID, b.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(SeasonID, b.TeamID, c.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home),
            SeasonMatch.Seed(SeasonID, b.TeamID, c.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home));
        await Context.SaveChangesAsync();

        var result = await _service.GetTeamStandings(SeasonID);

        Assert.That(result.Select(r => r.TeamName), Is.EqualTo(ExpectedStandingsOrder));
    }

    [Test]
    public async Task GetTeamStandings_OnlyReturnsTeamsFromSpecifiedSeason()
    {
        await EnsureSeason(1);
        await EnsureSeason(2);
        Context.Teams.AddRange(
            Team.Seed(seasonID: 1, teamName: "Season1Team"),
            Team.Seed(seasonID: 2, teamName: "Season2Team"));
        await Context.SaveChangesAsync();

        var result = await _service.GetTeamStandings(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Length.EqualTo(1));
            Assert.That(result[0].TeamName, Is.EqualTo("Season1Team"));
        }
    }

    [Test]
    public async Task GetTeamStandings_ArchivedRecord_UsesRecordedWinsAndLossesInsteadOfMatches()
    {
        await SeedSeason();
        var team = Team.Seed(seasonID: SeasonID, teamName: "Relentless", recordedWins: 9, recordedLosses: 1);
        var opponent = Team.Seed(seasonID: SeasonID, teamName: "Benchwarmers");
        Context.Teams.AddRange(team, opponent);
        await Context.SaveChangesAsync();
        // Even if a match existed it must not be counted: the archived record wins instead
        Context.SeasonMatches.Add(SeasonMatch.Seed(SeasonID, team.TeamID, opponent.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Away));
        await Context.SaveChangesAsync();

        var result = await _service.GetTeamStandings(SeasonID);

        var record = result.Single(r => r.TeamName == "Relentless");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(record.Wins, Is.EqualTo(9));
            Assert.That(record.Losses, Is.EqualTo(1));
            Assert.That(record.RecordFromSource, Is.True);
        }
    }
}
