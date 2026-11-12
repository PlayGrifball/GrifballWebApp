using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Integration.Test;

/// <summary>/season (<see cref="SeasonCommandsHandler"/>) and the lookups its options go through, called with a substitute context.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonCommandsTests
{
    private GrifballContext _context = null!;
    private CommandLookups _lookups = null!;
    private IBracketService _brackets = null!;
    private SeasonCommandsHandler _handler = null!;
    private readonly CommandContextRecorder _discord = new();

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var seasons = new SeasonService(_context, new SeasonStatsService(_context));
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        _lookups = new CommandLookups(_context, seasons);
        _brackets = Substitute.For<IBracketService>();
        _handler = new SeasonCommandsHandler(_lookups, seasons, new TeamStandingsService(_context), new MatchPlannerService(_context),
            new GradesService(_context), new SignupsService(_context), _brackets, urls);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<Season> AddSeason(string name, int daysFromNow = 0)
    {
        var now = DateTime.UtcNow.AddDays(daysFromNow);
        var season = Season.Seed(seasonName: name).WithDates(publicAt: now.AddDays(-40), signupsOpen: now.AddDays(-40), signupsClose: now.AddDays(-30),
            draftStart: now.AddDays(-25), seasonStart: now.AddDays(-20), seasonEnd: now.AddDays(20));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private async Task<(Team Alpha, Team Beta)> AddTeams(Season season)
    {
        var alpha = Team.Seed(seasonID: season.SeasonID, teamName: "Alpha");
        var beta = Team.Seed(seasonID: season.SeasonID, teamName: "Beta");
        _context.Teams.AddRange(alpha, beta);
        await _context.SaveChangesAsync();
        return (alpha, beta);
    }

    [Test]
    public async Task Standings_Should_ListTheCurrentSeasonsTeams_When_NoSeasonIsGiven()
    {
        await AddSeason("Old", daysFromNow: -400);
        var current = await AddSeason("Current");
        await AddTeams(current);

        await _handler.Standings(_discord.Context, null);

        Assert.That(_discord.DeferredPublicly(), Is.True);
        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Current standings"));
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/season/{current.SeasonID}"));
            Assert.That(embed.Description, Does.Contain($"[Alpha](https://grif.test/season/{current.SeasonID}/team/").And.Contain("0-0"));
            Assert.That(embed.Description, Does.Contain("Beta"));
        }
    }

    [Test]
    public async Task Info_Should_FindTheSeasonByName_When_NoChoiceWasPicked()
    {
        await AddSeason("Season Twelve");
        var wanted = await AddSeason("Season Thirteen");

        await _handler.Info(_discord.Context, "thirteen");

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Season Thirteen"));
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/season/{wanted.SeasonID}"));
            Assert.That(embed.Fields!.Select(f => f.Name), Does.Contain("Teams").And.Contain("Signups").And.Contain("Dates"));
        }
    }

    [Test]
    public async Task Info_Should_SayThereIsNoSuchSeason_When_TheIDIsUnknown()
    {
        await AddSeason("Only");

        await _handler.Info(_discord.Context, "999999");

        Assert.That(_discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task List_Should_LinkEachSeason()
    {
        var season = await AddSeason("Listed");

        await _handler.List(_discord.Context);

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Url, Is.EqualTo("https://grif.test/seasons"));
            Assert.That(embed.Description, Does.Contain($"[Listed](https://grif.test/season/{season.SeasonID})"));
        }
    }

    [Test]
    public async Task Schedule_Should_ListUpcomingMatches_AndCountTheUnscheduled()
    {
        var season = await AddSeason("Scheduled");
        var (alpha, beta) = await AddTeams(season);
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(season.SeasonID, alpha.TeamID, beta.TeamID, scheduledTime: DateTime.UtcNow.AddDays(2)),
            SeasonMatch.Seed(season.SeasonID, beta.TeamID, alpha.TeamID));
        await _context.SaveChangesAsync();

        await _handler.Schedule(_discord.Context, season.SeasonID.ToString());

        var description = _discord.Embed().Description;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(description, Does.Contain("Alpha vs Beta").And.Contain("https://grif.test/seasonmatch/"));
            Assert.That(description, Does.Contain("1 more match is not scheduled yet."));
        }
    }

    [Test]
    public async Task Signups_Should_SayNobodySignedUp_When_ThereAreNone()
    {
        var season = await AddSeason("Empty");

        await _handler.Signups(_discord.Context, season.SeasonID.ToString());

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Empty signups: 0"));
            Assert.That(embed.Description, Is.EqualTo("Nobody has signed up yet."));
        }
    }

    [Test]
    public async Task Bracket_Should_ListTheWinnerRoundsAndTheFinal()
    {
        var season = await AddSeason("Playoffs");
        _brackets.GetBracketsAsync(season.SeasonID, Arg.Any<CancellationToken>()).Returns(new BracketDto
        {
            WinnerRounds = [new RoundDto { RoundNumber = 1, Matches = [new MatchDto { MatchNumber = "W1", HomeTeam = "Alpha", AwayTeam = "Beta" }] }],
            LoserRounds = [],
            GrandFinal = new MatchDto { MatchNumber = "GF", HomeTeam = "Alpha", AwayTeam = "Gamma" },
            GrandFinalSuddenDeath = null!,
        });

        await _handler.Bracket(_discord.Context, season.SeasonID.ToString());

        Assert.That(_discord.Embed().Description, Is.EqualTo("**Round 1**\nAlpha vs Beta\n**Grand final**\nAlpha vs Gamma"));
    }

    [Test]
    public async Task Stats_Grades_And_Draft_Should_SayWhenThereIsNothingYet()
    {
        var season = await AddSeason("Fresh");
        var id = season.SeasonID.ToString();

        await _handler.Stats(_discord.Context, id, SeasonStat.KD);
        await _handler.Grades(_discord.Context, id);
        await _handler.Draft(_discord.Context, id);

        var embeds = Enumerable.Range(0, 3).Select(i => _discord.Embed(i)).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embeds[0].Title, Is.EqualTo("Fresh: top KD"));
            Assert.That(embeds[0].Description, Is.EqualTo("No stats yet."));
            Assert.That(embeds[1].Url, Is.EqualTo($"https://grif.test/season/{id}/playergrades"));
            Assert.That(embeds[1].Description, Is.EqualTo("No grades yet."));
            Assert.That(embeds[2].Url, Is.EqualTo($"https://grif.test/season/{id}/draft"));
        }
    }

    [Test]
    public async Task Choices_Should_BeValuedByID_AndMatchPartOfTheName()
    {
        var season = await AddSeason("Season Seven");
        var (alpha, _) = await AddTeams(season);

        var seasons = await _lookups.SeasonChoices("seven");
        var teams = await _lookups.TeamChoices("alp");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Select(x => (x.Name, x.StringValue)), Is.EqualTo(new[] { ("Season Seven", CommandLookups.ChoiceValue(season.SeasonID)) }));
            Assert.That(teams.Select(x => (x.Name, x.StringValue)), Is.EqualTo(new[] { ("Alpha (Season Seven)", CommandLookups.ChoiceValue(alpha.TeamID)) }));
        }
    }

    [Test]
    public async Task Team_Should_PreferAnExactName_ThenTheLatestSeason()
    {
        var older = await AddSeason("Older", daysFromNow: -400);
        var newer = await AddSeason("Newer");
        _context.Teams.AddRange(Team.Seed(seasonID: older.SeasonID, teamName: "Alpha"), Team.Seed(seasonID: newer.SeasonID, teamName: "Alpha"),
            Team.Seed(seasonID: newer.SeasonID, teamName: "Alphabet"));
        await _context.SaveChangesAsync();

        var team = await _lookups.Team("alpha");
        var none = await _lookups.Team("zzz");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team?.SeasonID, Is.EqualTo(newer.SeasonID));
            Assert.That(none, Is.Null);
        }
    }

    [Test]
    public void ChoiceName_Should_CutLongNamesToDiscordsLimit()
    {
        Assert.That(CommandLookups.ChoiceName(new string('x', 150)), Has.Length.EqualTo(100));
    }

    [TestCase(SeasonStat.Kills)]
    [TestCase(SeasonStat.Wins)]
    [TestCase(SeasonStat.Goals)]
    public async Task Stats_Should_RankByTheStatAsked(SeasonStat stat)
    {
        var season = await AddSeason("Ranked");

        await _handler.Stats(_discord.Context, season.SeasonID.ToString(), stat);

        Assert.That(_discord.Embed().Title, Is.EqualTo($"Ranked: top {stat}"));
    }

    [Test]
    public async Task List_Should_SayHowManyMoreThereAre_PastTheFirstPage()
    {
        for (var i = 0; i < 17; i++)
            await AddSeason($"Many {i}");

        await _handler.List(_discord.Context);

        Assert.That(_discord.Embed().Description, Does.EndWith(CommandReplies.More(2)));
    }

    [Test]
    public void Lines_Should_CutALongListToFit_AndSayHowManyMore()
    {
        var lines = Enumerable.Range(0, 100).Select(i => new string((char)('a' + i % 26), 100)).ToList();

        var text = CommandReplies.Lines(lines, "none");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Has.Length.LessThanOrEqualTo(4096));
            Assert.That(text, Does.Match(@"…and \d+ more on the site$"));
        }
    }

    [TestCase("standings")]
    [TestCase("schedule")]
    [TestCase("stats")]
    [TestCase("grades")]
    [TestCase("signups")]
    [TestCase("bracket")]
    [TestCase("draft")]
    public async Task SeasonCommands_Should_SayThereIsNoSuchSeason_When_TheIDIsUnknown(string command)
    {
        Func<Task> run = command switch
        {
            "standings" => () => _handler.Standings(_discord.Context, "999999"),
            "schedule" => () => _handler.Schedule(_discord.Context, "999999"),
            "stats" => () => _handler.Stats(_discord.Context, "999999", SeasonStat.Goals),
            "grades" => () => _handler.Grades(_discord.Context, "999999"),
            "signups" => () => _handler.Signups(_discord.Context, "999999"),
            "bracket" => () => _handler.Bracket(_discord.Context, "999999"),
            _ => () => _handler.Draft(_discord.Context, "999999"),
        };

        await run();

        Assert.That(_discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task SeasonCommands_Should_ShowTheSeasonsStatsChampionsAndGrades()
    {
        var season = await AddSeason("Played");
        var (alpha, _) = await AddTeams(season);
        var player = await _context.AddUser("statted", gamertag: "Statted");
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "Statted", Goals = 9, Kills = 4, GamesPlayed = 2 },
            season.SeasonID, player.Id, gameVersionID: 1, teamID: alpha.TeamID, stintIndex: 1));
        _context.Set<PlayerChampionship>().Add(PlayerChampionship.Seed(season.SeasonID, alpha.TeamID, player.Id, tier: "Gold", gamertagUsed: "Statted", isCaptain: true));
        var seasonMatch = SeasonMatch.Seed(season.SeasonID, null, null);
        _context.SeasonMatches.Add(seasonMatch);
        await _context.SaveChangesAsync();
        await GradesTestData.AddLinkedMatch(_context, seasonMatch.SeasonMatchID, 1, TimeSpan.FromMinutes(10), new GradesTestData.P(8_800_001, 3, 5, 2, 0));
        var id = season.SeasonID.ToString();
        var info = new CommandContextRecorder();
        var grades = new CommandContextRecorder();

        await _handler.Stats(_discord.Context, id, SeasonStat.Goals);
        await _handler.Info(info.Context, id);
        await _handler.Grades(grades.Context, id);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_discord.Embed().Description, Does.StartWith($"1. [Statted](https://grif.test/profile/{player.Id}) · 9"));
            Assert.That(info.Embed().Fields!.Single(f => f.Name == "Gold champion").Value, Does.StartWith("Alpha: "));
            Assert.That(grades.Embed().Description, Does.StartWith("1. "));
        }
    }
}
