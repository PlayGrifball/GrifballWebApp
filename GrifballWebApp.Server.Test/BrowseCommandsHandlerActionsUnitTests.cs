using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// BrowseCommandsHandler with something to show, past the empty replies BrowseCommandsHandlerUnitTests covers: on a
/// temp-file SQLite GrifballContext with the real services (the leaderboards' cache substituted, a miss every time) and
/// a fixed clock.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BrowseCommandsHandlerActionsUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] RankFields = ["Rings", "Goals", "Kills", "Games", "Carrier stops"];

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(Now));

    private BrowseCommandsHandler Handler() => new(
        new LeaderboardsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db), Substitute.For<IDistributedCache>(), new RecordingLogger<LeaderboardsService>()),
        new MatchmakingStatsService(Db), new HomeEventsService(Db, new GrifballWebApp.Application.Seasons.SeasonVisibility(Db, _time), _time), new QuickSearchService(Db, new GrifballWebApp.Application.Seasons.SeasonVisibility(Db, _time)), new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)), Urls());

    [TestCase(LeaderboardBoard.Kills, LeaderboardFormat.Tournament, "All-time Kills (Tournament)")]
    [TestCase(LeaderboardBoard.Games, LeaderboardFormat.LeagueSeason, "All-time Games (League season)")]
    [TestCase(LeaderboardBoard.Goals, LeaderboardFormat.LateLeague, "All-time Goals (Late League)")]
    [TestCase(LeaderboardBoard.Rings, LeaderboardFormat.LeagueSeasonAndTournament, "All-time Rings (League season + Tournament)")]
    [TestCase(LeaderboardBoard.CarrierStops, LeaderboardFormat.All, "All-time CarrierStops")]
    public async Task Leaderboard_EachBoard_IsTitledForItsFormat(LeaderboardBoard board, LeaderboardFormat format, string title)
    {
        var discord = new CommandContextRecorder();

        await Handler().Leaderboard(discord.Context, board, format);

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((embed.Title, embed.Url), Is.EqualTo((title, "https://grif.test/leaderboards")));
            Assert.That(embed.Description, Is.EqualTo("Nobody is ranked yet."));
        }
    }

    [TestCase(LeaderboardFormat.All, new LeaderboardFilter[0])]
    [TestCase(LeaderboardFormat.LeagueSeason, new[] { LeaderboardFilter.LeagueSeason })]
    [TestCase(LeaderboardFormat.Tournament, new[] { LeaderboardFilter.Tournament })]
    [TestCase(LeaderboardFormat.LateLeague, new[] { LeaderboardFilter.LateLeague })]
    [TestCase(LeaderboardFormat.LeagueSeasonAndTournament, new[] { LeaderboardFilter.LeagueSeason, LeaderboardFilter.Tournament })]
    [TestCase(LeaderboardFormat.LeagueSeasonAndLateLeague, new[] { LeaderboardFilter.LeagueSeason, LeaderboardFilter.LateLeague })]
    [TestCase(LeaderboardFormat.TournamentAndLateLeague, new[] { LeaderboardFilter.Tournament, LeaderboardFilter.LateLeague })]
    public void FormatsOf_EachChoice_IsItsFormats(LeaderboardFormat choice, LeaderboardFilter[] formats)
        => Assert.That(BrowseCommandsHandler.FormatsOf(choice), Is.EqualTo(formats));

    [Test]
    public async Task Rank_APlayerWithNoGames_IsUnrankedOnEveryBoard()
    {
        var player = await AddUser(5, "Grifter1");
        var discord = new CommandContextRecorder();

        await Handler().Rank(discord.Context, "Grifter1", LeaderboardFormat.All);

        var embed = discord.Embed();
        var fields = embed.Fields!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((embed.Title, embed.Url), Is.EqualTo(("Grifter1: all-time ranks", $"https://grif.test/profile/{player.Id}")));
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(RankFields));
            Assert.That(fields.Select(f => f.Value), Is.All.EqualTo("Unranked"));
            Assert.That(fields.Select(f => f.Inline), Is.All.True);
        }
    }

    private async Task<MatchmakingSeason> AddLateLeagueSeason(string name, DateTime startsAt)
    {
        var season = MatchmakingSeason.Seed(name, startsAt, startsAt.AddDays(30));
        Db.MatchmakingSeasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task LateLeagueSeasons_ListsEach_NewestFirst()
    {
        var older = await AddLateLeagueSeason("Spring", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = await AddLateLeagueSeason("Autumn", new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var discord = new CommandContextRecorder();

        await Handler().LateLeagueSeasons(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo(
            $"[Autumn](https://grif.test/late-league/{newer.MatchmakingSeasonID}) · 0 games · 0 players\n" +
            $"[Spring](https://grif.test/late-league/{older.MatchmakingSeasonID}) · 0 games · 0 players"));
    }

    [TestCase(null, "Autumn")]
    [TestCase("spr", "Spring")]
    public async Task LateLeagueSeason_ShowsTheSeasonPicked_OrTheLatest(string? picked, string shown)
    {
        await AddLateLeagueSeason("Spring", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddLateLeagueSeason("Autumn", new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var discord = new CommandContextRecorder();

        await Handler().LateLeagueSeason(discord.Context, picked);

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo(shown));
            Assert.That(embed.Description, Does.EndWith(" · 0 games\n\nNo games yet."));
        }
    }

    [Test]
    public async Task LateLeagueSeason_ByItsNumber_ShowsIt()
    {
        var spring = await AddLateLeagueSeason("Spring", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        await AddLateLeagueSeason("Autumn", new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var discord = new CommandContextRecorder();

        await Handler().LateLeagueSeason(discord.Context, $" {spring.MatchmakingSeasonID} ");

        Assert.That(discord.Embed().Url, Is.EqualTo($"https://grif.test/late-league/{spring.MatchmakingSeasonID}"));
    }

    [Test]
    public async Task Events_ListsTheNextSeasonsSignupsDraftAndSeason()
    {
        var season = Season.Seed(seasonName: "Winter").WithDates(signupsOpen: Now.AddDays(1), signupsClose: Now.AddDays(5), draftStart: Now.AddDays(8),
            seasonStart: Now.AddDays(10), seasonEnd: Now.AddDays(60), publicAt: Now.AddDays(-1));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Handler().Events(discord.Context);

        var link = $"[Winter](https://grif.test/season/{season.SeasonID})";
        Assert.That(discord.Embed().Description!.Split('\n').Select(l => l[(l.IndexOf(" · ", StringComparison.Ordinal) + 3)..l.IndexOf(" until ", StringComparison.Ordinal)]),
            Is.EquivalentTo(new[] { $"{link} signups", $"{link} draft", $"{link} season" }));
    }

    [Test]
    public async Task Search_ListsThePlayersTeamsAndSeasonsFound()
    {
        var player = await AddUser(5, "Wintermute");
        var season = Season.Seed(seasonName: "Winter Cup").WithDates(signupsOpen: Now.AddDays(-60), signupsClose: Now.AddDays(-50), draftStart: Now.AddDays(-40),
            seasonStart: Now.AddDays(-30), seasonEnd: Now.AddDays(30));
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var team = await AddTeam(season, "Winter Wolves");
        var discord = new CommandContextRecorder();

        await Handler().Search(discord.Context, "Winter");

        var fields = discord.Embed().Fields!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields[0].Value, Is.EqualTo($"[Wintermute](https://grif.test/profile/{player.Id})"));
            Assert.That(fields[1].Value, Is.EqualTo($"[Winter Wolves](https://grif.test/season/{season.SeasonID}/team/{team.TeamID}) (Winter Cup)"));
            Assert.That(fields[2].Value, Does.StartWith($"[Winter Cup](https://grif.test/season/{season.SeasonID}) ("));
        }
    }
}
