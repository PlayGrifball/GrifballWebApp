using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class SeasonServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private SeasonService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        var stats = new SeasonStatsService(Context, _time);
        _service = new SeasonService(Context, stats, _time);
    }

    [Test]
    public async Task GetCurrentSeasonID_NoneRunning_ReturnsZero()
    {
        var id = await _service.GetCurrentSeasonID();

        Assert.That(id, Is.Zero);
    }

    [Test]
    public async Task GetCurrentSeasonID_SeasonRunning_ReturnsItsID()
    {
        var season = Season.Seed(seasonName: "Current", seasonID: 1);
        season.Schedule(Now.AddDays(-30), Now.AddDays(-20), Now.AddDays(-10), Now.AddDays(-5), Now.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var id = await _service.GetCurrentSeasonID();

        Assert.That(id, Is.EqualTo(1));
    }

    [Test]
    public async Task GetSeasonName_Found_ReturnsName()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "Winter Cup", seasonID: 1));
        await Context.SaveChangesAsync();

        var name = await _service.GetSeasonName(1);

        Assert.That(name, Is.EqualTo("Winter Cup"));
    }

    [Test]
    public async Task GetSeasonName_NotFound_ReturnsNull()
    {
        var name = await _service.GetSeasonName(999);

        Assert.That(name, Is.Null);
    }

    [Test]
    public async Task GetSeasonSummary_NoSuchSeason_ReturnsNull()
    {
        var summary = await _service.GetSeasonSummary(999);

        Assert.That(summary, Is.Null);
    }

    [Test]
    public async Task GetSeasonSummary_EmptySeason_ReturnsZeroCounts()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        await Context.SaveChangesAsync();

        var summary = await _service.GetSeasonSummary(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary!.TeamCount, Is.Zero);
            Assert.That(summary.SignupCount, Is.Zero);
            Assert.That(summary.MatchCount, Is.Zero);
            Assert.That(summary.BracketMatchCount, Is.Zero);
            Assert.That(summary.PlayerStatCount, Is.Zero);
            Assert.That(summary.Champions, Is.Empty);
        }
    }

    [Test]
    public async Task GetSeasonSummary_CountsTeamsSignupsAndMatches()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        var home = Team.Seed(teamName: "Home", seasonID: 1, teamID: 10);
        var away = Team.Seed(teamName: "Away", seasonID: 1, teamID: 20);
        Context.Teams.AddRange(home, away);
        Context.Users.Add(User.Seed(userName: "signer", id: 1));
        Context.Add(SeasonSignup.Seed(1, 1));
        Context.SeasonMatches.Add(SeasonMatch.Between(1, 10, 20, bestOf: 3));
        await Context.SaveChangesAsync();

        var summary = await _service.GetSeasonSummary(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary!.TeamCount, Is.EqualTo(2));
            Assert.That(summary.SignupCount, Is.EqualTo(1));
            Assert.That(summary.MatchCount, Is.EqualTo(1));
            Assert.That(summary.BracketMatchCount, Is.Zero);
        }
    }

    [Test]
    public async Task GetSeasonTeams_NoSuchSeason_ReturnsNull()
    {
        var teams = await _service.GetSeasonTeams(999);

        Assert.That(teams, Is.Null);
    }

    [Test]
    public async Task GetSeasonTeams_ReturnsRosterAndCaptainFirst()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        Context.Users.Add(User.Seed(userName: "captain", displayName: "Captain", id: 1));
        Context.Users.Add(User.Seed(userName: "pick", displayName: "Pick", id: 2));
        var team = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 10);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Context.Add(TeamPlayer.Seed(teamID: 10, userID: 1, draftCaptainOrder: 1));
        Context.Add(TeamPlayer.Seed(teamID: 10, userID: 2, draftRound: 1, draftPick: 1));
        await Context.SaveChangesAsync();
        team.SetCaptain(1);
        await Context.SaveChangesAsync();

        var teams = await _service.GetSeasonTeams(1);

        Assert.That(teams, Has.Count.EqualTo(1));
        var dto = teams![0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.TeamName, Is.EqualTo("Reclaimers"));
            Assert.That(dto.Wins, Is.Zero);
            Assert.That(dto.Losses, Is.Zero);
            Assert.That(dto.Players, Has.Count.EqualTo(2));
            Assert.That(dto.Players[0].UserID, Is.EqualTo(1));
            Assert.That(dto.Players[0].IsCaptain, Is.True);
        }
    }

    [Test]
    public async Task GetSeasons_FiltersByEnded()
    {
        var ended = Season.Seed(seasonName: "Ended", seasonID: 1);
        ended.Schedule(Now.AddDays(-60), Now.AddDays(-50), Now.AddDays(-40), Now.AddDays(-30), Now.AddDays(-10));
        var current = Season.Seed(seasonName: "Current", seasonID: 2);
        current.Schedule(Now.AddDays(-30), Now.AddDays(-20), Now.AddDays(-10), Now.AddDays(-5), Now.AddDays(30));
        Context.Seasons.AddRange(ended, current);
        await Context.SaveChangesAsync();

        var result = await _service.GetSeasons(new SeasonListFilter { Ended = true });

        Assert.That(result.Results.Select(s => s.SeasonID), Is.EqualTo(OnlyEndedSeason));
    }

    private static readonly int[] OnlyEndedSeason = [1];

    [Test]
    public async Task GetSeasonPlayerStats_NoGamesOrStats_ReturnsEmpty()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: 1));
        await Context.SaveChangesAsync();

        var stats = await _service.GetSeasonPlayerStats(1);

        Assert.That(stats, Is.Empty);
    }
}
