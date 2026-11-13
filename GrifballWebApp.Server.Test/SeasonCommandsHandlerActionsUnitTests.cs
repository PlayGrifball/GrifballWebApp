using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SeasonCommandsHandler with something to show, past the empty replies SeasonCommandsHandlerUnitTests covers: on a
/// temp-file SQLite GrifballContext with the real season page services and a fixed clock; the bracket's service
/// substituted (its own tests build brackets).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonCommandsHandlerActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly IBracketService _brackets = Substitute.For<IBracketService>();

    private SeasonCommandsHandler Handler()
    {
        var seasons = new SeasonService(Db, new SeasonStatsService(Db), _time);
        return new SeasonCommandsHandler(new CommandLookups(Db, seasons), seasons, new TeamStandingsService(Db), new MatchPlannerService(Db),
            new GradesService(Db), new SignupsService(Db, _time), _brackets, Urls());
    }

    /// <summary>A season running now (started a month ago).</summary>
    private async Task<Season> RunningSeason(string name = "Winter")
    {
        var season = Season.Seed(seasonName: name).WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50), draftStart: Now.AddDays(-40),
            seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task List_ListsTheSeasonsStarted_MarkingTheRunningOnes()
    {
        var season = await RunningSeason();
        var discord = new CommandContextRecorder();

        await Handler().List(discord.Context);

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((embed.Title, embed.Url), Is.EqualTo(("Seasons", "https://grif.test/seasons")));
            Assert.That(embed.Description, Does.StartWith($"[Winter](https://grif.test/season/{season.SeasonID}) · ").And.EndWith(" · 2026 · **running**"));
        }
    }

    [Test]
    public async Task List_NoSeasonStartedYet_SaysSo()
    {
        var discord = new CommandContextRecorder();

        await Handler().List(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("There are no seasons yet."));
    }

    [Test]
    public async Task Info_ShowsTheSeasonsChampions_WithTheirRoster()
    {
        var season = await RunningSeason();
        var team = await AddTeam(season, "Champs");
        var player = await AddUser(5, "Grifter1");
        Db.Set<PlayerChampionship>().Add(PlayerChampionship.Seed(seasonID: season.SeasonID, teamID: team.TeamID, userID: player.Id, tier: "Gold", gamertagUsed: "Grifter1", isCaptain: true));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Info(discord.Context, "Winter");

        var champion = discord.Embed().Fields!.Single(f => f.Name == "Gold champion");
        Assert.That((champion.Value, champion.Inline), Is.EqualTo(("Champs: Grifter1", (bool?)false)));
    }

    [Test]
    public async Task Standings_ListsEachTeam_WithItsRecord()
    {
        var season = await RunningSeason();
        var home = await AddTeam(season, "Home");
        await AddTeam(season, "Away");
        var discord = new CommandContextRecorder();

        await Handler().Standings(discord.Context, "Winter");

        var lines = discord.Embed().Description!.Split('\n');
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines, Has.Length.EqualTo(2));
            // Both 0-0: ranked either way round
            Assert.That(lines.Select(l => l[3..]), Has.Exactly(1).EqualTo($"[Home](https://grif.test/season/{season.SeasonID}/team/{home.TeamID}) · 0-0"));
        }
    }

    [Test]
    public async Task Schedule_ListsTheUpcomingMatches_AndCountsTheUnscheduled()
    {
        var season = await RunningSeason();
        var home = await AddTeam(season, "Home");
        var away = await AddTeam(season, "Away");
        var when = Now.AddDays(2);
        var scheduled = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, scheduledTime: when);
        Db.SeasonMatches.AddRange(scheduled, SeasonMatch.Seed(season.SeasonID, away.TeamID, home.TeamID));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Schedule(discord.Context, "Winter");

        Assert.That(discord.Embed().Description, Is.EqualTo(
            $"<t:{new DateTimeOffset(when).ToUnixTimeSeconds()}> · [Home vs Away](https://grif.test/seasonmatch/{scheduled.SeasonMatchID})\n\n1 more match is not scheduled yet."));
    }

    [TestCase(SeasonStat.Kills)]
    [TestCase(SeasonStat.Wins)]
    [TestCase(SeasonStat.KD)]
    public async Task Stats_EachStat_IsTitledForIt(SeasonStat stat)
    {
        var season = await RunningSeason();
        var discord = new CommandContextRecorder();

        await Handler().Stats(discord.Context, "Winter", stat);

        var embed = discord.Embed();
        Assert.That((embed.Title, embed.Url, embed.Description), Is.EqualTo(($"Winter: top {stat}", $"https://grif.test/season/{season.SeasonID}", "No stats yet.")));
    }

    [Test]
    public async Task Signups_ListsWhoSignedUp_InOrder_MarkingCaptains()
    {
        var season = await RunningSeason();
        var captain = await AddUser(5, "Captain1");
        var player = await AddUser(6, "Player1");
        Db.SeasonSignups.AddRange(
            SeasonSignup.Seed(season.SeasonID, player.Id, timestamp: Now.AddDays(-55)),
            SeasonSignup.Seed(season.SeasonID, captain.Id, timestamp: Now.AddDays(-58), teamName: "Caps", willCaptain: true));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Signups(discord.Context, "Winter");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Winter signups: 2"));
            Assert.That(embed.Description, Is.EqualTo("Captain1 (captain)\nPlayer1"));
        }
    }

    [Test]
    public async Task Bracket_ListsEachRound_AndTheGrandFinal()
    {
        var season = await RunningSeason();
        _brackets.GetBracketsAsync(season.SeasonID, Arg.Any<CancellationToken>()).Returns(new BracketDto
        {
            WinnerRounds =
            [
                new RoundDto { RoundNumber = 1, Matches = [new MatchDto { HomeTeam = "Seed 1", AwayTeam = "Seed 4" }, new MatchDto { HomeTeam = "Seed 2", AwayTeam = "Seed 3" }] },
                new RoundDto { RoundNumber = 2, Matches = [new MatchDto { HomeTeam = "Winner of W1", AwayTeam = "Winner of W2" }] },
            ],
            LoserRounds = [],
            GrandFinal = new MatchDto { HomeTeam = "Winner of W3", AwayTeam = "Winner of L2" },
        });
        var discord = new CommandContextRecorder();

        await Handler().Bracket(discord.Context, "Winter");

        Assert.That(discord.Embed().Description, Is.EqualTo(
            "**Round 1**\nSeed 1 vs Seed 4\nSeed 2 vs Seed 3\n**Round 2**\nWinner of W1 vs Winner of W2\n**Grand final**\nWinner of W3 vs Winner of L2"));
    }

    [Test]
    public async Task Bracket_None_SaysSo()
    {
        var season = await RunningSeason();
        _brackets.GetBracketsAsync(season.SeasonID, Arg.Any<CancellationToken>()).Returns(new BracketDto { WinnerRounds = [], LoserRounds = [] });
        var discord = new CommandContextRecorder();

        await Handler().Bracket(discord.Context, "Winter");

        Assert.That(discord.Embed().Description, Is.EqualTo("This season has no bracket yet."));
    }
}
