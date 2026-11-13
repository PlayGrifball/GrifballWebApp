using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// TeamPageService's matches, and the roster every team page shows (RosterPlayer.Of read through the fake
/// IApplicationDbContext, then RosterPlayer.ToPlayers). TeamPageService.GetAsync and TeamHistoryService.GetAsync's found
/// path aren't here: their projection (Titles with Distinct, and Players: two nested collections) needs SQL APPLY, which
/// the fake context's plain UseSqlite() lacks (see TeamHistoryServiceGetAsyncUnitTests); Integration.Test covers both.
/// </summary>
[TestFixture]
internal sealed class TeamPageServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Day1 = new(2026, 6, 1, 20, 0, 0, DateTimeKind.Utc);
    private const int SeasonID = 1;
    private const int TeamID = 10;
    private const int RivalID = 20;
    private const int ThirdID = 30;
    private static readonly int[] CaptainsThenByRoundThenName = [4, 1, 2, 3];
    private static readonly string[] ProTitle = ["Pro"];

    private TeamPageService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TeamPageService(Context, new FakeTimeProvider(Day1));
    }

    private async Task SeedTeams()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        Context.Teams.AddRange(Team.Seed(teamName: "Bombers", seasonID: SeasonID, teamID: TeamID),
            Team.Seed(teamName: "Rivals", seasonID: SeasonID, teamID: RivalID),
            Team.Seed(teamName: "Thirds", seasonID: SeasonID, teamID: ThirdID));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GetMatchesAsync_NoMatches_ReturnsEmpty()
    {
        await SeedTeams();

        var matches = await _service.GetMatchesAsync(TeamID);

        Assert.That(matches, Is.Empty);
    }

    [Test]
    public async Task GetMatchesAsync_HomeAndAway_UnplayedFirstByTimeThenPlayed_FromTheTeamsSide()
    {
        await SeedTeams();
        var lateHome = SeasonMatch.Between(SeasonID, TeamID, RivalID, bestOf: 3, scheduledTime: Day1.AddDays(2));
        var earlyHome = SeasonMatch.Between(SeasonID, TeamID, ThirdID, bestOf: 3, scheduledTime: Day1.AddDays(1));
        var playedAway = SeasonMatch.Between(SeasonID, RivalID, TeamID, bestOf: 3, scheduledTime: Day1).WonBy(SeasonMatchSide.Home);
        var notTheirs = SeasonMatch.Between(SeasonID, RivalID, ThirdID, bestOf: 3, scheduledTime: Day1);
        Context.SeasonMatches.AddRange(lateHome, earlyHome, playedAway, notTheirs);
        await Context.SaveChangesAsync();

        var matches = await _service.GetMatchesAsync(TeamID);

        Assert.That(matches, Has.Length.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches[0].SeasonMatchID, Is.EqualTo(earlyHome.SeasonMatchID));
            Assert.That(matches[0].OtherTeamID, Is.EqualTo(ThirdID));
            Assert.That(matches[0].OtherTeamName, Is.EqualTo("Thirds"));
            Assert.That(matches[0].ScheduledTime, Is.EqualTo(Day1.AddDays(1)));
            Assert.That(matches[0].BestOf, Is.EqualTo(3));
            Assert.That(matches[0].Result, Is.Null);
            Assert.That(matches[0].Score, Is.Null);

            Assert.That(matches[1].SeasonMatchID, Is.EqualTo(lateHome.SeasonMatchID));
            Assert.That(matches[1].OtherTeamName, Is.EqualTo("Rivals"));

            Assert.That(matches[2].SeasonMatchID, Is.EqualTo(playedAway.SeasonMatchID));
            Assert.That(matches[2].OtherTeamID, Is.EqualTo(RivalID));
            Assert.That(matches[2].OtherTeamName, Is.EqualTo("Rivals"));
            Assert.That(matches[2].Score, Is.Zero);
            Assert.That(matches[2].OtherScore, Is.EqualTo(2));
            Assert.That(matches[2].Result, Is.EqualTo(SeasonMatchResult.Loss));
            Assert.That(matches[2].OtherResult, Is.EqualTo(SeasonMatchResult.Won));
        }
    }

    /// <summary>Team 10's roster: user 1 its set captain (gamertag Chief, West North America), user 4 a draft captain
    /// (gamertag Alpha), user 2 a round 1 pick with only a Discord name, user 3 a round 2 pick with only a display name.</summary>
    private async Task SeedRoster()
    {
        await SeedTeams();
        Context.Users.AddRange(
            User.Seed(userName: "chief", xboxUser: XboxUser.FromXbox(100, "Chief"), regionID: 1, regionSource: RegionSource.User, id: 1),
            User.Seed(userName: "disc", discordUser: DiscordUser.FromDiscord(200, "disc"), id: 2),
            User.Seed(userName: "zed", displayName: "Zed", id: 3),
            User.Seed(userName: "alpha", xboxUser: XboxUser.FromXbox(400, "Alpha"), id: 4));
        await Context.SaveChangesAsync();
        Context.AddRange(
            TeamPlayer.Seed(teamID: TeamID, userID: 1),
            TeamPlayer.Seed(teamID: TeamID, userID: 2, draftRound: 1),
            TeamPlayer.Seed(teamID: TeamID, userID: 3, draftRound: 2),
            TeamPlayer.Seed(teamID: TeamID, userID: 4, draftCaptainOrder: 1),
            TeamPlayer.Seed(teamID: RivalID, userID: 3));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var team = await Context.Teams.Include(t => t.TeamPlayers).SingleAsync(t => t.TeamID == TeamID);
        team.SetCaptain(1);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private Task<List<RosterPlayer>> ReadRoster() => Context.Teams.Where(t => t.TeamID == TeamID)
        .SelectMany(t => t.TeamPlayers).AsNoTracking().Select(RosterPlayer.Of).ToListAsync();

    [Test]
    public async Task RosterPlayerOf_ReadsNamesRegionAndCaptaincy()
    {
        await SeedRoster();

        var roster = (await ReadRoster()).OrderBy(p => p.UserID).ToList();

        Assert.That(roster, Has.Count.EqualTo(4));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(roster[0].Gamertag, Is.EqualTo("Chief"));
            Assert.That(roster[0].Region, Is.EqualTo("West North America"));
            Assert.That(roster[0].IsCaptain, Is.True);
            Assert.That(roster[0].UserName, Is.EqualTo("chief"));
            Assert.That(roster[1].Gamertag, Is.Null);
            Assert.That(roster[1].Discord, Is.EqualTo("disc"));
            Assert.That(roster[1].DraftRound, Is.EqualTo(1));
            Assert.That(roster[1].IsCaptain, Is.False);
            Assert.That(roster[1].Region, Is.Null);
            Assert.That(roster[2].DisplayName, Is.EqualTo("Zed"));
            Assert.That(roster[2].Discord, Is.Null);
            Assert.That(roster[3].DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(roster[3].IsCaptain, Is.True);
            Assert.That(roster.Select(p => p.TeamPlayerID), Is.All.GreaterThan(0));
        }
    }

    [Test]
    public async Task RosterPlayerToPlayers_CaptainsFirstThenByRoundThenName_NamedAsTheAppNamesThem()
    {
        await SeedRoster();

        var players = RosterPlayer.ToPlayers(await ReadRoster());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Select(p => p.UserID), Is.EqualTo(CaptainsThenByRoundThenName));
            Assert.That(players[0].Gamertag, Is.EqualTo("Alpha"));
            Assert.That(players[0].DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(players[1].Gamertag, Is.EqualTo("Chief"));
            Assert.That(players[1].Region, Is.EqualTo("West North America"));
            Assert.That(players[1].IsCaptain, Is.True);
            Assert.That(players[2].Gamertag, Is.EqualTo("disc"));
            Assert.That(players[2].DraftRound, Is.EqualTo(1));
            Assert.That(players[3].Gamertag, Is.EqualTo("Zed"));
            Assert.That(players[3].IsCaptain, Is.False);
            Assert.That(players[3].TeamPlayerID, Is.GreaterThan(0));
        }
    }

    [Test]
    public void RosterPlayerToPlayers_NoNameAtAll_IsEmpty()
    {
        var players = RosterPlayer.ToPlayers([new RosterPlayer { TeamPlayerID = 5, UserID = 9 }]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Single().Gamertag, Is.Empty);
            Assert.That(players.Single().DraftRound, Is.Null);
        }
    }

    [Test]
    public void TeamDto_HoldsWhatThePageShows()
    {
        var players = RosterPlayer.ToPlayers([new RosterPlayer { TeamPlayerID = 5, UserID = 9, Gamertag = "Nine", IsCaptain = true }]);
        var season = new SeasonInfoDto { SeasonID = SeasonID, SeasonName = "S1" };

        var dto = new TeamDto
        {
            TeamName = "Bombers",
            Season = season,
            RecordFromSource = true,
            Titles = ["Pro"],
            Wins = 7,
            Losses = 2,
            TBD = 1,
            Players = players,
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.TeamName, Is.EqualTo("Bombers"));
            Assert.That(dto.Season.SeasonName, Is.EqualTo("S1"));
            Assert.That(dto.RecordFromSource, Is.True);
            Assert.That(dto.Titles, Is.EqualTo(ProTitle));
            Assert.That(dto.Wins, Is.EqualTo(7));
            Assert.That(dto.Losses, Is.EqualTo(2));
            Assert.That(dto.TBD, Is.EqualTo(1));
            Assert.That(dto.Players.Single().Gamertag, Is.EqualTo("Nine"));
        }
    }
}
