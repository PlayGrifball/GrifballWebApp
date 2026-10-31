using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportServiceTests
{
    private GrifballContext _context;
    private IDataPullService _dataPull;
    private HistoricalImportService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPull = Substitute.For<IDataPullService>();
        _service = new HistoricalImportService(_context, _dataPull, NullLogger<HistoricalImportService>.Instance);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Import_MakesOneSeasonPerLeagueAndName_AndOnePlaceholderPerPlayer()
    {
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10), Player("x0 pie x0", kills: 3)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("shadow  s16 ", kills: 2)),
            Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", false, Player("SHADOW S16", kills: 4))));

        var seasons = await _context.Seasons.AsNoTracking().OrderBy(s => s.HistoricalSourceID).ToListAsync();
        var users = await _context.Users.AsNoTracking().ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(seasons.Select(s => s.HistoricalSourceID), Is.EquivalentTo(new[] { "winter-league-2019/Stats", "late-league-season-8-halo-3/Stats" }));
            Assert.That(seasons.Select(s => s.IsHistorical), Is.All.True);
            Assert.That(seasons.Select(s => s.PublicAt), Is.All.Null);
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2019").SeasonStart, Is.EqualTo(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(users, Has.Count.EqualTo(2), "One per player, whatever the case and spacing of their gamertag");
            Assert.That(users.Select(u => u.IsDummyUser), Is.All.True);
            Assert.That(users.Select(u => u.UserName), Is.EquivalentTo(new[] { "historical:Shadow S16", "historical:x0 pie x0" }));
            Assert.That(users.Select(u => u.DisplayName), Is.EquivalentTo(new[] { "Shadow S16", "x0 pie x0" }));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(4));
            Assert.That(await _context.PlayerAliases.CountAsync(), Is.Zero, "An alias is a second Xbox account, never a spelling or rename");
            Assert.That(report.SeasonsCreated, Is.EqualTo(2));
            Assert.That(report.StatsInserted, Is.EqualTo(4));
            Assert.That(report.DummyUsersCreated, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Import_AgainWithTheSameArchive_ChangesNothing_AndChangedValuesUpdateInPlace()
    {
        var input = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10), Player("Traded", kills: 1, stint: 1), Player("Traded", kills: 2, stint: 2)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        await _service.Import(input);
        var before = await Counts();
        var ids = await _context.PlayerSeasonStats.AsNoTracking().OrderBy(s => s.PlayerSeasonStatID).Select(s => s.PlayerSeasonStatID).ToListAsync();

        var again = await _service.Import(input);
        Assert.That(await Counts(), Is.EqualTo(before));
        Assert.That((again.SeasonsCreated, again.StatsInserted, again.DummyUsersCreated, again.StatsUpdated), Is.EqualTo((0, 0, 0, 4)));

        var changed = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 99), Player("Traded", kills: 1, stint: 1), Player("Traded", kills: 2, stint: 2)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        await _service.Import(changed);

        _context.ChangeTracker.Clear();
        Assert.That(await Counts(), Is.EqualTo(before));
        Assert.That(await _context.PlayerSeasonStats.OrderBy(s => s.PlayerSeasonStatID).Select(s => s.PlayerSeasonStatID).ToListAsync(), Is.EqualTo(ids));
        Assert.That(await _context.PlayerSeasonStats.Where(s => s.Gamertag == "Shadow S16" && !s.IsPlayoffs).Select(s => s.Kills).SingleAsync(), Is.EqualTo(99));
    }

    [Test]
    public async Task Import_FindsRealAccountsByXboxGamertag_AndOtherXboxAccountsByAlias()
    {
        var byXbox = new User { UserName = "noah", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Shadow S16" } };
        var byAlias = new User { UserName = "pie" };
        _context.AddRange(byXbox, byAlias);
        await _context.SaveChangesAsync();
        _context.PlayerAliases.Add(new PlayerAlias { UserID = byAlias.Id, Alias = "x0 pie x0", NormalizedAlias = "X0 PIE X0" });
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("shadow s16"), Player("X0 Pie X0"))));

        Assert.That(report.DummyUsersCreated, Is.Zero);
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).ToListAsync(), Is.EquivalentTo(new[] { byXbox.Id, byAlias.Id }));
    }

    [Test]
    public async Task Import_AfterThePlaceholderWasMergedIntoAnAccount_StillFindsThatAccount()
    {
        var input = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Old Tag", kills: 5)),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Old Tag", kills: 6)));
        await _service.Import(input);
        var dummy = await _context.Users.SingleAsync(u => u.IsDummyUser);
        var real = new User { UserName = "real" };
        _context.Users.Add(real);
        await _context.SaveChangesAsync();
        await new UserMergeService(_context).Merge(real.Id, dummy.Id);
        _context.ChangeTracker.Clear();

        var again = await _service.Import(input);

        Assert.That(again.DummyUsersCreated, Is.Zero, "No second placeholder for the old gamertag");
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).Distinct().ToListAsync(), Is.EqualTo(new[] { real.Id }));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Import_FindsAPlaceholderWithoutRowsByItsUserName()
    {
        var dummy = new User { UserName = "historical:Old Tag", NormalizedUserName = "HISTORICAL:OLD TAG", IsDummyUser = true };
        _context.Users.Add(dummy);
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("old tag"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(dummy.Id));
        Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Import_Championships_GoToTheirSeasons_ByTierOrName_OrGetASeasonOfTheirOwn()
    {
        var champions = new ChampionsArchive
        {
            Champions =
            [
                new ArchiveChampionship { Season = "Summer League 2021", Game = "MCC Split", Tier = "Pro", Team = "Yeji", Captain = "xStarBlade", Roster = ["xStarBlade", "oDhmon"] },
                new ArchiveChampionship { Season = "Winter League 2019", Game = "MCC Halo 3", Tier = "Open", Team = "Rooster", Captain = "Silva", Roster = ["Silva", "silva ", "oDhmon"] },
                new ArchiveChampionship { Season = "Winter League 2011", Game = "Halo Reach", Tier = "Pro", Team = "Cabs", Captain = "halfbreed105", Roster = ["halfbreed105"] },
                new ArchiveChampionship { Season = "Winter league 2011", Game = "Halo Reach", Tier = "Amateur", Team = "Downstait", Captain = "Hoya16", Roster = ["Hoya16"] },
            ],
        };
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Summer League 2021 (Pro)", "halo3", "AGLA", false, Player("oDhmon")),
                    Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva")),
                ],
            },
            Championships = champions,
        };

        var report = await _service.Import(input);
        var again = await _service.Import(input);

        var titles = await _context.PlayerChampionships.AsNoTracking().Include(c => c.Season).Include(c => c.User).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.ChampionshipsInserted, Is.EqualTo(6));
            Assert.That(report.Skipped, Has.Some.Contains("listed twice"));
            Assert.That((again.ChampionshipsInserted, again.ChampionshipsUpdated, again.SeasonsCreated), Is.EqualTo((0, 6, 0)));
            Assert.That(titles, Has.Count.EqualTo(6));
            Assert.That(titles.Single(t => t.GamertagUsed == "xStarBlade").Season.SeasonName, Is.EqualTo("Summer League 2021 (Pro)"));
            Assert.That(titles.Single(t => t.GamertagUsed == "xStarBlade").IsCaptain, Is.True);
            Assert.That(titles.Single(t => t.GamertagUsed == "xStarBlade").GameVersionID, Is.Null, "MCC Split is no one game");
            Assert.That(titles.Single(t => t.GamertagUsed == "Silva").Season.SeasonName, Is.EqualTo("Winter League 2019"));
            Assert.That(titles.Where(t => t.GamertagUsed == "oDhmon").Select(t => t.UserID).Distinct().Count(), Is.EqualTo(1));
            var created = titles.Where(t => t.Tier is "Pro" or "Amateur" && t.Season.SeasonName.StartsWith("Winter")).Select(t => t.Season).ToList();
            Assert.That(created.Select(s => s.SeasonID).Distinct().Count(), Is.EqualTo(1), "Both tiers' titles in one season, whatever its name's case");
            Assert.That(created[0].HistoricalSourceID, Is.EqualTo("history-agla-champions/Winter League 2011"));
            Assert.That(created[0].GameVersionID, Is.EqualTo(2));
            Assert.That(titles.Select(t => t.LeagueID), Is.All.EqualTo(League.DefaultLeagueID));
            Assert.That(await _context.Seasons.CountAsync(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Import_SkipsASeasonWhoseNameTheLeagueAlreadyHas()
    {
        _context.Seasons.Add(new Season { SeasonName = "Winter League 2019", LeagueID = League.DefaultLeagueID });
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva"))));

        Assert.That(report.Skipped, Has.One.Contains("already has a season with that name"));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.Zero);
        Assert.That(await _context.Users.CountAsync(), Is.Zero, "The season's transaction rolled back");
    }

    [Test]
    public async Task Import_SkipsUnknownLeaguesAndGames_AndSecondRowsForAPlayer()
    {
        var report = await _service.Import(Input(
            Entry("A", "halo3", "Nope", false, Player("x")),
            Entry("B", "halo2", "AGLA", false, Player("x")),
            Entry("C 2020", "halo3", "agla", false, Player("Same"), Player("same"), Player(" "))));

        Assert.That(report.Skipped, Has.Some.Contains("unknown league"));
        Assert.That(report.Skipped, Has.Some.Contains("unknown game version"));
        Assert.That(report.Skipped, Has.Some.Contains("second row"));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Import_WaypointMatches_ArePulled_AndReplaceManualStats()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var playoff = Guid.NewGuid();
        // One match is in the database already; the others come through the data pull service
        await AddMatch(first, (100, "Hells Angel cx", Outcomes.Won, 30, 10), (200, "Inf3ktion", Outcomes.Lost, 10, 30));
        _dataPull.When(d => d.GetAndSaveMatch(second)).Do(_ => AddMatch(second, (100, "Hells Angel cx", Outcomes.Lost, 20, 20)).GetAwaiter().GetResult());
        _dataPull.When(d => d.GetAndSaveMatch(playoff)).Do(_ => AddMatch(playoff, (100, "Hells Angel cx", Outcomes.Won, 5, 1)).GetAwaiter().GetResult());
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1, team: "Ram Ranch"), Player("Manual Only", kills: 4)),
                ],
            },
            WaypointMatches = new()
            {
                ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches
                {
                    Regular = [first.ToString(), $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{second}"],
                    Playoffs = [playoff.ToString(), "not a match"],
                },
                ["agla/not-a-season"] = new WaypointSeasonMatches { Regular = [Guid.NewGuid().ToString()] },
            },
        };

        var report = await _service.Import(input);

        await _dataPull.DidNotReceive().GetAndSaveMatch(first);
        var stats = await _context.PlayerSeasonStats.AsNoTracking().Include(s => s.User).ToListAsync();
        var regular = stats.Single(s => s.Gamertag == "Hells Angel cx" && !s.IsPlayoffs);
        Assert.Multiple(() =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.WaypointMatchesPulled, Is.EqualTo(2));
            Assert.That(report.Skipped, Has.Some.Contains("not a match ID"));
            Assert.That(report.Skipped, Has.Some.Contains("agla/not-a-season"));
            Assert.That((regular.Source, regular.GamesPlayed, regular.Wins, regular.Losses, regular.Kills, regular.Deaths), Is.EqualTo((StatSource.Waypoint, 2, 1, 1, 50, 30)));
            Assert.That(regular.TeamName, Is.EqualTo("Ram Ranch"), "From the sheet");
            Assert.That(regular.KdRatio, Is.EqualTo(1.667m));
            Assert.That(regular.WinPercent, Is.EqualTo(50m));
            Assert.That(regular.User.XboxUserID, Is.EqualTo(100), "The placeholder is the Xbox account's, so the player claims it by setting their gamertag");
            Assert.That(stats.Single(s => s.Gamertag == "Hells Angel cx" && s.IsPlayoffs).Source, Is.EqualTo(StatSource.Waypoint));
            Assert.That(stats.Single(s => s.Gamertag == "Inf3ktion").Source, Is.EqualTo(StatSource.Waypoint));
            Assert.That(stats.Single(s => s.Gamertag == "Manual Only").Source, Is.EqualTo(StatSource.Manual), "No Waypoint matches: the sheet's totals");
            Assert.That(stats, Has.Count.EqualTo(4), "Never Manual and Waypoint for one player's season");
            Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(1));
        });

        var again = await _service.Import(input);
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(4));
        Assert.That((again.WaypointStatsInserted, again.WaypointStatsUpdated, again.StatsInserted), Is.EqualTo((0, 3, 0)));
    }

    [Test]
    public async Task Import_WaypointStats_ReplaceManualStatsImportedEarlier_AndGoToTheLinkedAccount()
    {
        var input = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1), Player("Hells Angel cx", kills: 2, stint: 2)));
        await _service.Import(input);
        var dummy = await _context.Users.SingleAsync();
        var linked = new User { UserName = "hells", XboxUser = new XboxUser { XboxUserID = 100, Gamertag = "Hells Angel cx" } };
        _context.Users.Add(linked);
        await _context.SaveChangesAsync();
        var match = Guid.NewGuid();
        await AddMatch(match, (100, "Hells Angel cx", Outcomes.Won, 30, 10));

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = input.Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] } },
        });

        var stats = await _context.PlayerSeasonStats.AsNoTracking().ToListAsync();
        Assert.That(stats.Select(s => (s.UserID, s.Source, s.Kills)), Is.EquivalentTo(new[]
        {
            (linked.Id, StatSource.Waypoint, 30),
            (dummy.Id, StatSource.Manual, 1),
            (dummy.Id, StatSource.Manual, 2),
        }), "The linked account has the Waypoint row; the placeholder's sheet rows wait for a merge");
        Assert.That(report.Errors, Is.Empty);
    }

    [Test]
    public async Task Import_APlaceholderFoundByItsGamertag_GetsTheXboxAccountItPlayedOn()
    {
        var input = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1)));
        await _service.Import(input);
        var match = Guid.NewGuid();
        await AddMatch(match, (100, "Hells Angel cx", Outcomes.Won, 30, 10));

        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = input.Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] } },
        });

        var user = await _context.Users.AsNoTracking().SingleAsync();
        Assert.That(user.XboxUserID, Is.EqualTo(100));
        var stat = await _context.PlayerSeasonStats.AsNoTracking().SingleAsync();
        Assert.That((stat.UserID, stat.Source, stat.Kills), Is.EqualTo((user.Id, StatSource.Waypoint, 30)));
    }

    [Test]
    public async Task Import_AMatchThatCannotBePulled_IsReported_AndTheRestImports()
    {
        var missing = Guid.NewGuid();
        _dataPull.When(d => d.GetAndSaveMatch(missing)).Do(_ => throw new HttpRequestException("rate limited"));

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Someone"))] },
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [missing.ToString()] } },
        });

        Assert.That(report.Skipped, Has.Some.Contains("could not be pulled"));
        Assert.That(report.Skipped, Has.Some.Contains("1 of 1 Waypoint matches are not in the database"));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.Source).SingleAsync(), Is.EqualTo(StatSource.Manual));
    }

    [Test]
    public async Task Import_ASeasonThatFails_IsRolledBack_AndTheOthersImport()
    {
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: new string('x', 101))),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Silva"))));

        Assert.That(report.Errors, Has.One.StartsWith("winter-league-2019/Stats"));
        Assert.That(await _context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Spring League 2019" }));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Import_WithoutTheWaypointMatches_KeepsTheWaypointStatsImportedEarlier()
    {
        var match = Guid.NewGuid();
        await AddMatch(match, (100, "Hells Angel cx", Outcomes.Won, 30, 10));
        var seasons = new UnifiedArchive
        {
            Seasons =
            [
                Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1)),
                Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva")),
            ],
        };
        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = seasons,
            WaypointMatches = new()
            {
                ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] },
                ["winter-league-2019/Stats"] = new WaypointSeasonMatches { Regular = [Guid.NewGuid().ToString()] },
            },
        });
        Assert.That(report.Skipped, Has.Some.Contains("winter-league-2019/Stats"), "Waypoint is Halo Infinite only");

        var again = await _service.Import(new HistoricalArchiveInput { Seasons = seasons });

        var stat = await _context.PlayerSeasonStats.AsNoTracking().SingleAsync(s => s.Gamertag == "Hells Angel cx");
        Assert.That((stat.Source, stat.Kills), Is.EqualTo((StatSource.Waypoint, 30)), "The sheet's totals never replace Waypoint stats");
        Assert.That(again.ManualReplacedByWaypoint, Is.EqualTo(1));
    }

    [Test]
    public async Task ImportedRows_LoadWithTheirRelations()
    {
        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva"))] },
            Championships = new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Open", Team = "T", Roster = ["Silva"] }] },
        });
        var user = await _context.Users.SingleAsync();
        _context.PlayerAliases.Add(new PlayerAlias { UserID = user.Id, GameVersionID = 1, Alias = "Silva Two", NormalizedAlias = "SILVA TWO", IsPrimary = false, Source = "test" });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var stat = await _context.PlayerSeasonStats.Include(s => s.Season).ThenInclude(s => s.GameVersion).Include(s => s.GameVersion).Include(s => s.User).SingleAsync();
        var title = await _context.PlayerChampionships.Include(c => c.User).Include(c => c.League).Include(c => c.GameVersion).SingleAsync();
        var alias = await _context.PlayerAliases.Include(a => a.User).Include(a => a.GameVersion).SingleAsync();
        var halo3 = await _context.GameVersions.Include(g => g.Seasons).SingleAsync(g => g.Slug == "halo3");
        Assert.Multiple(() =>
        {
            Assert.That(stat.Season.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(stat.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(title.PlayerChampionshipID, Is.GreaterThan(0));
            Assert.That((title.User.Id, title.League.Slug, title.GameVersion.Slug), Is.EqualTo((user.Id, "agla", "halo3")));
            Assert.That((alias.PlayerAliasID > 0, alias.User.Id, alias.GameVersion.Slug, alias.GameVersionID, alias.IsPrimary, alias.Source), Is.EqualTo((true, user.Id, "halo3", 1, false, "test")));
            Assert.That(halo3.Seasons.Select(s => s.SeasonName), Is.EqualTo(new[] { "Winter League 2019" }));
        });
    }

    [Test]
    public async Task Import_KeysASeasonByTheWorkbookAndSheetOfItsRegularSeason_AndStoresTheWorkbooksLink()
    {
        var regular = Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva"));
        var playoffs = Entry("Winter League 2019", "halo3", "AGLA", true, Player("Silva"));
        var input = new HistoricalArchiveInput
        {
            // The playoffs first: the regular season's sheet is still the key
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    With(playoffs, "2019-wl19-schedule-stats", "Playoff Stats"),
                    With(regular, "2019-wl19-schedule-stats", "Team and Player Stats "),
                ],
            },
            Catalog = new ArchiveCatalog
            {
                Sources =
                [
                    new CatalogSource { Id = "2019-wl19-schedule-stats", Url = "https://docs.google.com/spreadsheets/d/wl19" },
                    new CatalogSource { Id = "2018-sl18-pro-schedule", Url = "https://docs.google.com/spreadsheets/d/dead" },
                ],
            },
        };

        var report = await _service.Import(input);

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        Assert.That(season.HistoricalSourceID, Is.EqualTo("2019-wl19-schedule-stats/Team and Player Stats"));
        Assert.That(season.HistoricalSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/wl19"));
        Assert.That(report.Warnings, Is.Empty);
    }

    [Test]
    public async Task Import_AfterTheSeasonsNameIsCorrected_FindsTheSameSeason()
    {
        await _service.Import(Input(With(Entry("Winter Leauge 2019", "halo3", "AGLA", false, Player("Silva", kills: 1)), "2019-wl19-schedule-stats", "Stats")));
        var id = await _context.Seasons.Select(s => s.SeasonID).SingleAsync();

        var again = await _service.Import(Input(With(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", kills: 2)), "2019-wl19-schedule-stats", "Stats")));

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        Assert.That((season.SeasonID, season.SeasonName), Is.EqualTo((id, "Winter League 2019")));
        Assert.That((again.SeasonsCreated, again.SeasonsUpdated, again.StatsInserted), Is.EqualTo((0, 1, 0)));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.Kills).SingleAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task Import_AnEntryWithoutWorkbookAndSheet_IsKeyedByItsLeagueAndName_WithAWarning()
    {
        var report = await _service.Import(Input(With(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva")), null, null)));

        Assert.That(await _context.Seasons.Select(s => s.HistoricalSourceID).SingleAsync(), Is.EqualTo("agla/winter-league-2019"));
        Assert.That(report.Warnings, Has.One.Contains("no source workbook and sheet"));
    }

    [Test]
    public async Task Import_ASeasonOnlyATitleNames_GetsTheChampionsWorkbooksKeyAndLink()
    {
        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Championships = new ChampionsArchive
            {
                SourceId = "history-agla-champions",
                SourceUrl = "https://docs.google.com/spreadsheets/d/champs",
                Champions = [new ArchiveChampionship { Season = "Original 8 Tounament", Game = "Halo 3", Tier = "Open", Team = "SG1", Roster = ["Kroze"] }],
            },
        });

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        Assert.That((season.HistoricalSourceID, season.HistoricalSourceUrl, season.SeasonStart.Year), Is.EqualTo(("history-agla-champions/Original 8 Tounament", "https://docs.google.com/spreadsheets/d/champs", 2008)));
    }

    private static ArchiveSeason With(ArchiveSeason entry, string? workbook, string? sheet) => new()
    {
        SeasonName = entry.SeasonName,
        GameVersion = entry.GameVersion,
        League = entry.League,
        Division = entry.Division,
        IsPlayoffs = entry.IsPlayoffs,
        SourceWorkbook = workbook,
        SourceSheet = sheet,
        Players = entry.Players,
    };

    /// <summary>The compiled archive in the repository imports, and a second run changes no row counts.</summary>
    [Test]
    public async Task Import_TheCompiledArchive_IsIdempotent()
    {
        var compiled = Path.Combine(RepositoryRoot(), "data", "historical-archive", "compiled");
        HistoricalArchiveInput input;
        await using (var seasons = File.OpenRead(Path.Combine(compiled, "unified-historical-seasons.json")))
        await using (var champions = File.OpenRead(Path.Combine(compiled, "agla-champions.json")))
        await using (var catalog = File.OpenRead(Path.Combine(compiled, "catalog.json")))
        {
            input = new HistoricalArchiveInput
            {
                Seasons = await HistoricalArchiveInput.ReadAsync<UnifiedArchive>(seasons),
                Championships = await HistoricalArchiveInput.ReadAsync<ChampionsArchive>(champions),
                Catalog = await HistoricalArchiveInput.ReadAsync<ArchiveCatalog>(catalog),
            };
        }

        var report = await _service.Import(input);
        var before = await Counts();
        var again = await _service.Import(input);

        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.StatsInserted, Is.GreaterThan(2000));
            Assert.That(report.ChampionshipsInserted, Is.GreaterThan(350));
            Assert.That(await Counts(), Is.EqualTo(before));
            Assert.That((again.SeasonsCreated, again.StatsInserted, again.ChampionshipsInserted, again.DummyUsersCreated), Is.EqualTo((0, 0, 0, 0)));
            Assert.That(again.Errors, Is.Empty);
            Assert.That(report.Warnings, Is.Empty, "Every entry names its workbook and sheet");
            Assert.That(await _context.Seasons.CountAsync(s => s.HistoricalSourceUrl == null), Is.Zero, "Every season links its workbook");
        });
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "data", "historical-archive")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("data/historical-archive");
    }

    private async Task<(int Seasons, int Stats, int Titles, int Users)> Counts()
        => (await _context.Seasons.CountAsync(), await _context.PlayerSeasonStats.CountAsync(),
            await _context.PlayerChampionships.CountAsync(), await _context.Users.CountAsync());

    private async Task AddMatch(Guid matchId, params (long Xuid, string Gamertag, Outcomes Outcome, int Kills, int Deaths)[] players)
    {
        var match = new Match { MatchID = matchId, Duration = TimeSpan.FromMinutes(10) };
        var teams = new Dictionary<Outcomes, MatchTeam>();
        foreach (var player in players)
        {
            var xbox = await _context.XboxUsers.FindAsync(player.Xuid) ?? _context.XboxUsers.Add(new XboxUser { XboxUserID = player.Xuid, Gamertag = player.Gamertag }).Entity;
            if (!teams.TryGetValue(player.Outcome, out var team))
            {
                team = new MatchTeam { MatchID = matchId, TeamID = teams.Count, Outcome = player.Outcome };
                teams.Add(player.Outcome, team);
                match.MatchTeams.Add(team);
            }
            team.MatchParticipants.Add(new MatchParticipant
            {
                MatchID = matchId,
                TeamID = team.TeamID,
                XboxUser = xbox,
                Kills = player.Kills,
                Deaths = player.Deaths,
                PersonalScore = player.Kills * 100,
                TimePlayed = TimeSpan.FromMinutes(10),
                AverageLife = TimeSpan.FromSeconds(12),
                DamageDealt = 1000,
                DamageTaken = 900,
                FirstJoinedTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        }
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
    }

    private static HistoricalArchiveInput Input(params ArchiveSeason[] entries) => new() { Seasons = new UnifiedArchive { Seasons = [.. entries] } };

    private static ArchiveSeason Entry(string name, string game, string league, bool playoffs, params ArchivePlayer[] players) => new()
    {
        SeasonName = name,
        GameVersion = game,
        League = league,
        Division = "Open",
        IsPlayoffs = playoffs,
        SourceWorkbook = HistoricalKeys.Slug(name),
        SourceSheet = playoffs ? "Playoff Stats" : "Stats",
        Players = [.. players],
    };

    private static ArchivePlayer Player(string gamertag, int kills = 0, int stint = 1, string? team = null) => new()
    {
        Gamertag = gamertag,
        Kills = kills,
        Deaths = 1,
        GamesPlayed = 1,
        StintIndex = stint,
        TeamName = team,
        KdRatio = 1.23456m,
    };
}
