using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Integration.Test;

/// <summary>/match (<see cref="MatchCommandsHandler"/>) and /leaderboard, /lateleague, /events and /search (<see cref="BrowseCommandsHandler"/>).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchAndBrowseCommandsTests
{
    private static readonly string[] RankBoards = ["Rings", "Goals", "Kills", "Games", "Carrier stops"];

    private GrifballContext _context = null!;
    private CommandLookups _lookups = null!;
    private MatchCommandsHandler _matches = null!;
    private BrowseCommandsHandler _browse = null!;
    private readonly CommandContextRecorder _discord = new();

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        var stats = new SeasonStatsService(_context);
        var lateLeague = new MatchmakingStatsService(_context);
        _lookups = new CommandLookups(_context, new SeasonService(_context, stats));
        var seasonMatches = new SeasonMatchService(_context, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(_context));
        _matches = new MatchCommandsHandler(_lookups, seasonMatches, new MatchDetailsService(_context), urls);
        var leaderboards = new LeaderboardsService(_context, stats, lateLeague,
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<LeaderboardsService>.Instance);
        _browse = new BrowseCommandsHandler(leaderboards, lateLeague, new HomeEventsService(_context), new QuickSearchService(_context), _lookups, urls);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<(Season Season, SeasonMatch Match)> AddMatch()
    {
        var season = await _context.AddSeason("Summer");
        var home = await _context.AddTeamWithCaptain(season, "HomeCap", 8_000_001);
        var away = await _context.AddTeamWithCaptain(season, "AwayCap", 8_000_002);
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        return (season, match);
    }

    [Test]
    public async Task MatchInfo_Should_ShowAnUnplayedMatch_FoundByATeamsName()
    {
        var (season, match) = await AddMatch();
        var homeName = (await _context.Teams.FindAsync(match.HomeTeamID))!.TeamName;

        await _matches.Info(_discord.Context, homeName.ToLower());

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/seasonmatch/{match.SeasonMatchID}"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Result").Value, Is.EqualTo("To be played"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Best of").Value, Is.EqualTo("3"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Games").Value, Is.EqualTo("None reported yet."));
            Assert.That(embed.Fields!.Single(f => f.Name == "Season").Value, Does.Contain($"/season/{season.SeasonID}"));
        }
    }

    [Test]
    public async Task MatchInfo_Should_SayThereIsNoSuchMatch_When_NoneMatches()
    {
        await _matches.Info(_discord.Context, "123456");

        Assert.That(_discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchMatch));
    }

    [TestCase("not-a-guid")]
    [TestCase("6f1d3c56-1c3b-4d3e-9a63-2a0b3f6a9e01")]
    public async Task MatchGame_Should_SayThereIsNoSuchGame_When_TheIDIsBadOrUnknown(string matchID)
    {
        await _matches.Game(_discord.Context, matchID);

        Assert.That(_discord.Content(), Is.EqualTo(MatchCommandsHandler.NoSuchGame));
    }

    [Test]
    public async Task SeasonMatchChoices_Should_NameBothTeamsAndTheSeason()
    {
        var (_, match) = await AddMatch();

        var choices = await _lookups.SeasonMatchChoices("");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choices.Single().StringValue, Is.EqualTo(CommandLookups.ChoiceValue(match.SeasonMatchID)));
            Assert.That(choices.Single().Name, Does.Contain(" vs ").And.EndWith("(Summer)"));
        }
    }

    [Test]
    public async Task Leaderboard_Should_SayNobodyIsRanked_OnAnEmptyBoard()
    {
        await _browse.Leaderboard(_discord.Context, LeaderboardBoard.Goals, LeaderboardFormat.Tournament);

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("All-time Goals (Tournament)"));
            Assert.That(embed.Url, Is.EqualTo("https://grif.test/leaderboards"));
            Assert.That(embed.Description, Is.EqualTo("Nobody is ranked yet."));
        }
    }

    [Test]
    public async Task Rank_Should_ShowEachBoard_ForAKnownPlayer()
    {
        var user = await _context.AddUser("ranked", gamertag: "RankedGT");

        await _browse.Rank(_discord.Context, "RankedGT", LeaderboardFormat.All);

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/profile/{user.Id}"));
            Assert.That(embed.Fields!.Select(f => f.Name), Is.EqualTo(RankBoards));
        }
    }

    [Test]
    public async Task LateLeague_Should_SayThereAreNoSeasons_When_ThereAreNone()
    {
        var seasons = new CommandContextRecorder();
        var season = new CommandContextRecorder();

        await _browse.LateLeagueSeasons(seasons.Context);
        await _browse.LateLeagueSeason(season.Context, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Embed().Description, Is.EqualTo("No Late League seasons yet."));
            Assert.That(season.Content(), Is.EqualTo(BrowseCommandsHandler.NoSuchLateLeagueSeason));
        }
    }

    [Test]
    public async Task Events_Should_ListACurrentSeason()
    {
        var season = await _context.AddCurrentSeason("Now Playing");

        await _browse.Events(_discord.Context);

        Assert.That(_discord.Embed().Description, Does.Contain($"[Now Playing](https://grif.test/season/{season.SeasonID}) season until"));
    }

    [Test]
    public async Task Search_Should_ListMatchingPlayersTeamsAndSeasons()
    {
        var user = await _context.AddUser("searchable", gamertag: "Searchable");

        await _browse.Search(_discord.Context, "Searchable");

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Fields!.Single(f => f.Name == "Players").Value, Does.Contain($"https://grif.test/profile/{user.Id}"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Teams").Value, Is.EqualTo("None"));
        }
    }

    [TestCase(LeaderboardBoard.Rings, LeaderboardFormat.All)]
    [TestCase(LeaderboardBoard.Kills, LeaderboardFormat.LeagueSeason)]
    [TestCase(LeaderboardBoard.Games, LeaderboardFormat.All)]
    [TestCase(LeaderboardBoard.CarrierStops, LeaderboardFormat.All)]
    public async Task Leaderboard_Should_ShowEachBoard(LeaderboardBoard board, LeaderboardFormat format)
    {
        await _browse.Leaderboard(_discord.Context, board, format);

        Assert.That(_discord.Embed().Title, Does.StartWith($"All-time {board}"));
    }

    [Test]
    public async Task Search_Should_LinkMatchingTeamsAndSeasons()
    {
        var now = DateTime.UtcNow;
        var season = Season.Seed(seasonName: "Findable Season").WithDates(seasonStart: now.AddDays(-30), seasonEnd: now.AddDays(-1), publicAt: now.AddDays(-60));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        _context.Teams.Add(Team.Seed(seasonID: season.SeasonID, teamName: "Findable Team"));
        await _context.SaveChangesAsync();

        await _browse.Search(_discord.Context, "Findable");

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Fields!.Single(f => f.Name == "Teams").Value, Does.Contain("Findable Team"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Seasons").Value, Does.Contain($"https://grif.test/season/{season.SeasonID}"));
        }
    }

    [Test]
    public async Task MatchGameAndInfo_Should_ShowAReportedGamesTeamsAndPlayers()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var game = await SeasonMatchTestData.AddMatchAsync(_context, new DateTime(2026, 10, 30, 23, 0, 0, DateTimeKind.Utc),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, SeasonMatchTestData.AwayXbox, Outcomes.Lost);
        _context.Set<SeriesGame>().Add(new SeriesGame { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1, MatchID = game.MatchID });
        await _context.SaveChangesAsync();
        var info = new CommandContextRecorder();

        await _matches.Game(_discord.Context, game.MatchID.ToString());
        await _matches.Info(info.Context, seeded.SeasonMatch.SeasonMatchID.ToString());

        var embed = _discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/match/{game.MatchID}"));
            Assert.That(embed.Fields!.Select(f => f.Name), Has.Some.EndsWith("(Won)").And.Some.EndsWith("(Lost)"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Duration").Value, Is.EqualTo("10:00"));
            Assert.That(info.Embed().Fields!.Single(f => f.Name == "Games").Value, Does.StartWith("Game 1: ").And.Contain($"/match/{game.MatchID}"));
        }
    }

    [Test]
    public async Task LateLeague_Should_ShowASeasonsTopPlayers_ByNameIDOrLatest()
    {
        var player = await _context.AddUser("llplayer", gamertag: "LatePlayer");
        var season = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.MatchmakingSeasons.Add(season);
        _context.Set<MatchmakingPlayerStat>().Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "LatePlayer", Kills = 4, GamesPlayed = 2, Wins = 2 },
            gameVersionID: 1, matchmakingSeason: season, user: player));
        await _context.SaveChangesAsync();
        var list = new CommandContextRecorder();
        var byName = new CommandContextRecorder();
        var byID = new CommandContextRecorder();
        var unknown = new CommandContextRecorder();

        await _browse.LateLeagueSeasons(list.Context);
        await _browse.LateLeagueSeason(_discord.Context, null);
        await _browse.LateLeagueSeason(byName.Context, "season 8");
        await _browse.LateLeagueSeason(byID.Context, season.MatchmakingSeasonID.ToString());
        await _browse.LateLeagueSeason(unknown.Context, "nope");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list.Embed().Description, Does.Contain($"https://grif.test/late-league/{season.MatchmakingSeasonID}"));
            Assert.That(_discord.Embed().Description, Does.Contain($"1. [LatePlayer](https://grif.test/profile/{player.Id})"));
            Assert.That(byName.Embed().Title, Is.EqualTo("Late League Season 8 (Halo 3)"));
            Assert.That(byID.Embed().Title, Is.EqualTo("Late League Season 8 (Halo 3)"));
            Assert.That(unknown.Content(), Is.EqualTo(BrowseCommandsHandler.NoSuchLateLeagueSeason));
        }
    }

    [Test]
    public async Task Choices_Should_ListEverything_When_NothingIsTyped()
    {
        await AddMatch();
        await _context.AddUser("chooser", gamertag: "Chooser");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _lookups.SeasonChoices(null), Is.Not.Empty);
            Assert.That(await _lookups.TeamChoices(null), Is.Not.Empty);
            Assert.That(await _lookups.PlayerChoices(null), Is.Not.Empty);
            Assert.That(await _lookups.SeasonMatchChoices(null), Is.Not.Empty);
        }
    }

    [Test]
    public async Task MatchInfoAndGame_Should_ShowAMatchWithoutTeams_AndAnUnreportedGame()
    {
        var season = await _context.AddSeason("Waiting");
        var waiting = SeasonMatch.Seed(season.SeasonID, null, null);
        _context.SeasonMatches.Add(waiting);
        await _context.SaveChangesAsync();
        // Its players have Xbox accounts but no season match: an unreported game
        var game = await GradesTestData.AddLinkedMatch(_context, null, 1, TimeSpan.FromMinutes(10), new GradesTestData.P(9_100_001, 3, 5, 2, 0));
        var info = new CommandContextRecorder();

        await _matches.Info(info.Context, waiting.SeasonMatchID.ToString());
        await _matches.Game(_discord.Context, game.MatchID.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(info.Embed().Title, Is.EqualTo("TBD vs TBD"));
            Assert.That(_discord.Embed().Title, Is.EqualTo("Halo Infinite match"));
            Assert.That(_discord.Embed().Fields!.Select(f => f.Name), Has.Some.StartsWith("Team 0: "));
        }
    }
}
