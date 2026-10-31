using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        _service = new HistoricalImportService(_context, _dataPull, Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }), NullLogger<HistoricalImportService>.Instance,
            new GrifballWebApp.Server.Championships.ChampionshipService(_context), new GamertagMatching(_context), TimeProvider.System);
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
            Assert.That(seasons.Select(s => s.PublicAt), Is.EqualTo(seasons.Select(s => (DateTime?)s.SeasonStart)), "Public since they began");
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2019").SeasonStart, Is.EqualTo(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(users, Has.Count.EqualTo(2), "One per player, whatever the case and spacing of their gamertag");
            Assert.That(users.Select(u => u.IsDummyUser), Is.All.True);
            Assert.That(users.Select(u => u.UserName), Is.EquivalentTo(new[] { "historical:Shadow S16", "historical:x0 pie x0" }));
            Assert.That(users.Select(u => u.DisplayName), Is.EquivalentTo(new[] { "Shadow S16", "x0 pie x0" }));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(4));
            Assert.That(await _context.XboxGamertagHistory.CountAsync(), Is.Zero, "No gamertag history from spellings");
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
    public async Task Import_FindsAccountsByTheirCurrentGamertag_AndRenamedOnesByTheirGamertagHistory()
    {
        var byXbox = new User { UserName = "noah", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Shadow S16" } };
        var byAlias = new User { UserName = "pie", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "Pie Now" } };
        _context.AddRange(byXbox, byAlias);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 2, Gamertag = "x0 pie x0", NormalizedGamertag = "X0 PIE X0", LastSeen = Utc(2020), Source = GamertagHistorySource.RenameDetected });
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
            Assert.That(titles.Select(t => t.Season.LeagueID), Is.All.EqualTo(League.DefaultLeagueID));
            Assert.That(await _context.Seasons.CountAsync(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Import_MakesTheSeasonsTeams_WithTheirRostersRecordsAndTitles_LinkedFromStatsAndChampionships()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Winter League 2019", "halo3", "AGLA", false,
                        Player("Silva", team: "Rooster", wins: 9, losses: 1), Player("Pie", team: "rooster ", wins: 8, losses: 2),
                        Player("Traded", stint: 1, team: "Rooster", wins: 2, losses: 0), Player("Traded", stint: 2, team: "Hens", wins: 1, losses: 7),
                        Player("Benchy", team: "Hens", wins: 2, losses: 8), Player("Free Agent")),
                    Entry("Winter League 2019", "halo3", "AGLA", true, Player("Silva", team: "Rooster", wins: 3, losses: 0)),
                ],
            },
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Open", Team = "Rooster", Captain = "Silva", Roster = ["Silva", "Pie", "Ringer"] }],
            },
        };

        var report = await _service.Import(input);
        var again = await _service.Import(input);

        _context.ChangeTracker.Clear();
        var teams = await _context.Teams.AsNoTracking().Include(t => t.TeamPlayers).ThenInclude(tp => tp.User).Include(t => t.Captain).ThenInclude(c => c!.User)
            .OrderBy(t => t.TeamName).ToListAsync();
        var stats = await _context.PlayerSeasonStats.AsNoTracking().Include(s => s.Team).ToListAsync();
        var titles = await _context.PlayerChampionships.AsNoTracking().Include(c => c.Team).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((report.TeamsCreated, again.TeamsCreated), Is.EqualTo((2, 0)), "Found again by name on a re-run");
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(new[] { "Hens", "Rooster" }), "One team whatever the case and spacing of its name");
            Assert.That(teams.Select(t => (t.RecordedWins, t.RecordedLosses)), Is.EqualTo(new (int?, int?)[] { (2, 8), (9, 1) }), "The regular season's, from the sheet");
            Assert.That(teams[0].TeamPlayers.Select(tp => tp.User.DisplayName), Is.EquivalentTo(new[] { "Traded", "Benchy" }));
            Assert.That(teams[1].TeamPlayers.Select(tp => tp.User.DisplayName), Is.EquivalentTo(new[] { "Silva", "Pie", "Traded", "Ringer" }), "Its title's roster too");
            Assert.That(teams[1].Captain!.User.DisplayName, Is.EqualTo("Silva"), "The title's captain");
            Assert.That(teams[0].CaptainID, Is.Null);
            Assert.That(stats.Where(s => s.Gamertag == "Traded").OrderBy(s => s.StintIndex).Select(s => s.Team!.TeamName), Is.EqualTo(new[] { "Rooster", "Hens" }));
            Assert.That(stats.Single(s => s.Gamertag == "Free Agent").TeamID, Is.Null);
            Assert.That(stats.Single(s => s.Gamertag == "Silva" && s.IsPlayoffs).Team!.TeamName, Is.EqualTo("Rooster"));
            Assert.That(titles.Select(t => t.Team.TeamName), Is.All.EqualTo("Rooster"));
            Assert.That(titles.Select(t => t.GamertagUsed), Is.EquivalentTo(new[] { "Silva", "Pie", "Ringer" }));
        });
    }

    [Test]
    public async Task Import_ARosterChangedInTheArchive_IsChangedOnTheTeam()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("New", team: "Rooster"))));

        var roster = await _context.TeamPlayers.AsNoTracking().Select(tp => tp.User.DisplayName).ToListAsync();
        Assert.That(roster, Is.EquivalentTo(new[] { "Silva", "New" }));
    }

    [Test]
    public async Task Import_ThisYearsSeason_EndsByTheImport_SoItIsAPastSeason()
    {
        var year = DateTime.UtcNow.Year;
        var before = DateTime.UtcNow;
        await _service.Import(Input(Entry($"Summer League {year}", "infinite", "AGLA", false, Player("Silva"))));
        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        var ended = season.SeasonEnd;

        await _service.Import(Input(Entry($"Summer League {year}", "infinite", "AGLA", false, Player("Silva"))));

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(season.SeasonStart, Is.EqualTo(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(ended, Is.InRange(before, DateTime.UtcNow));
            Assert.That((await _context.Seasons.AsNoTracking().SingleAsync()).SeasonEnd, Is.EqualTo(ended), "A re-run keeps it");
            Assert.That(await new GrifballWebApp.Server.Seasons.SeasonService(_context).GetCurrentSeasonID(), Is.Zero, "Not a current season");
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
    public async Task Import_WaypointMatches_ArePulled_LinkedAsSeasonMatches_AndAreThePlayersStats()
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
        var lines = await Lines();
        var regular = lines.Single(s => s.Gamertag == "Hells Angel cx" && !s.IsPlayoffs);
        var hells = await _context.Users.AsNoTracking().SingleAsync(u => u.XboxUserID == 100);
        var matches = await _context.SeasonMatches.AsNoTracking().Include(m => m.MatchLinks).Include(m => m.BracketMatch).Include(m => m.HomeTeam).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.WaypointMatchesPulled, Is.EqualTo(2));
            Assert.That(report.Skipped, Has.Some.Contains("not a match ID"));
            Assert.That(report.Skipped, Has.Some.Contains("agla/not-a-season"));
            Assert.That((report.SeasonMatchesCreated, report.GamesLinked), Is.EqualTo((3, 3)), "Games with no start time are a series each");
            Assert.That(matches.SelectMany(m => m.MatchLinks).Select(l => l.MatchID), Is.EquivalentTo(new[] { first, second, playoff }));
            Assert.That(matches.Select(m => m.HomeTeam!.TeamName), Is.All.EqualTo("Ram Ranch"), "The side of the sheet's team");
            Assert.That(matches.Count(m => m.BracketMatch != null), Is.EqualTo(1), "The playoff game is a bracket match");
            Assert.That((regular.FromMatches, regular.GamesPlayed, regular.Wins, regular.Losses, regular.Kills, regular.Deaths), Is.EqualTo((true, 2, 1, 1, 50, 30)));
            Assert.That(regular.TeamName, Is.EqualTo("Ram Ranch"), "From the sheet");
            Assert.That(regular.KdRatio, Is.EqualTo(1.667m));
            Assert.That(regular.WinPercent, Is.EqualTo(50m));
            Assert.That(regular.UserID, Is.EqualTo(hells.Id), "The placeholder is the Xbox account's, so the player claims it by setting their gamertag");
            Assert.That(lines.Single(s => s.Gamertag == "Hells Angel cx" && s.IsPlayoffs).FromMatches, Is.True);
            Assert.That(lines.Single(s => s.Gamertag == "Inf3ktion").FromMatches, Is.True);
            Assert.That(lines.Single(s => s.Gamertag == "Manual Only").FromMatches, Is.False, "No Waypoint matches: the sheet's totals");
            Assert.That(lines, Has.Count.EqualTo(4), "Never the games and the sheet for one player's season");
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(new[] { "Manual Only" }), "No stored totals for players with games");
            Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(1));
        });

        var again = await _service.Import(input);
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.SeasonMatches.CountAsync(), Is.EqualTo(3), "Linked games are kept as they are");
        Assert.That((again.SeasonMatchesCreated, again.StatsInserted, again.StatsUpdated), Is.EqualTo((0, 0, 1)));
    }

    [Test]
    public async Task Import_WaypointGames_ReplaceSheetTotalsImportedEarlier_AndGoToTheLinkedAccount()
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

        var lines = await Lines();
        Assert.That(lines.Select(s => (s.UserID, s.FromMatches, s.Kills)), Is.EquivalentTo(new[]
        {
            (linked.Id, true, 30),
            (dummy.Id, false, 1),
            (dummy.Id, false, 2),
        }), "The linked account has the games; the placeholder's sheet rows wait for a merge");
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
        var line = (await Lines()).Single();
        Assert.That((line.UserID, line.FromMatches, line.Kills), Is.EqualTo((user.Id, true, 30)));
        Assert.That(await _context.PlayerSeasonStats.AnyAsync(), Is.False);
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
        Assert.That(report.MissingMatches, Has.One.Contains("regular season: 1 of 1 matches missing"));
        await _dataPull.Received(3).GetAndSaveMatch(missing); // tried again with backoff
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.Source).SingleAsync(), Is.EqualTo(StatSource.Manual));
    }

    [Test]
    public async Task Import_ATeamNameLongerThanItsColumn_IsCut()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: new string('x', 60)))));

        Assert.That(await _context.Teams.Select(t => t.TeamName).SingleAsync(), Is.EqualTo(new string('x', Team.TeamNameColumnLength)));
    }

    [Test]
    public async Task Import_ASeasonThatFails_IsRolledBack_AndTheOthersImport()
    {
        var report = await _service.Import(Input(
            // A trade note longer than its column fails the season's save
            Entry("Winter League 2019", "halo3", "AGLA", false, new ArchivePlayer { Gamertag = "Silva", TradeNote = new string('x', 101) }),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Silva"))));

        Assert.That(report.Errors, Has.One.StartsWith("winter-league-2019/Stats"));
        Assert.That(await _context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(new[] { "Spring League 2019" }));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Import_WithoutTheWaypointMatches_KeepsTheGamesLinkedEarlier()
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

        var line = (await Lines()).Single(s => s.Gamertag == "Hells Angel cx");
        Assert.That((line.FromMatches, line.Kills), Is.EqualTo((true, 30)), "The sheet's totals never replace the games");
        Assert.That(again.ManualReplacedByWaypoint, Is.EqualTo(1));
        Assert.That(await _context.PlayerSeasonStats.AnyAsync(s => s.Gamertag == "Hells Angel cx"), Is.False);
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
        var seasonId = await _context.Seasons.Select(s => s.SeasonID).SingleAsync();
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 5, Gamertag = "Silva Now" });
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 5, Gamertag = "Silva", NormalizedGamertag = "SILVA", Source = GamertagHistorySource.Manual });
        _context.GamertagClaims.Add(new GamertagClaim { Kind = GamertagClaimKind.RosterMatch, XboxUserID = 5, Gamertag = "Silva", NormalizedGamertag = "SILVA", PlaceholderUserID = user.Id, SeasonID = seasonId });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var stat = await _context.PlayerSeasonStats.Include(s => s.Season).ThenInclude(s => s.GameVersion).Include(s => s.GameVersion).Include(s => s.User).SingleAsync();
        var title = await _context.PlayerChampionships.Include(c => c.User).Include(c => c.Season).ThenInclude(s => s.League).Include(c => c.GameVersion).SingleAsync();
        var history = await _context.XboxGamertagHistory.Include(h => h.XboxUser).SingleAsync();
        var claim = await _context.GamertagClaims.Include(c => c.XboxUser).Include(c => c.PlaceholderUser).Include(c => c.Season).Include(c => c.Team).SingleAsync();
        var halo3 = await _context.GameVersions.Include(g => g.Seasons).SingleAsync(g => g.Slug == "halo3");
        Assert.Multiple(() =>
        {
            Assert.That(stat.Season.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(stat.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(title.PlayerChampionshipID, Is.GreaterThan(0));
            Assert.That((title.User.Id, title.Season.League.Slug, title.GameVersion.Slug), Is.EqualTo((user.Id, "agla", "halo3")));
            Assert.That((history.XboxGamertagHistoryID > 0, history.XboxUser.Gamertag, history.FirstSeen, history.LastSeen), Is.EqualTo((true, "Silva Now", (DateTime?)null, (DateTime?)null)));
            Assert.That((claim.GamertagClaimID > 0, claim.XboxUser.Gamertag, claim.PlaceholderUser.Id, claim.Season.SeasonName, claim.Team, claim.Status), Is.EqualTo((true, "Silva Now", user.Id, "Winter League 2019", (Team?)null, GamertagClaimStatus.Pending)));
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

    private static DateTime Utc(int year, int month = 1, int day = 1) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Import_NeverGivesAnOlderSeasonsGamertag_ToTheAccountThatOnlyHoldsItNow()
    {
        // The account took "Shadow S16" in 2021; before that it was "Rookie"
        var now = new User { UserName = "now", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Shadow S16" } };
        _context.Users.Add(now);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Rookie", NormalizedGamertag = "ROOKIE", FirstSeen = Utc(2020), LastSeen = Utc(2021, 3), Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16"), Player("Rookie")),
            Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Shadow S16"))));

        var rows = await _context.PlayerSeasonStats.AsNoTracking().Include(s => s.Season).Include(s => s.User).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(rows.Single(r => r.Gamertag == "Rookie").UserID, Is.EqualTo(now.Id),
                "Its history still had it after 2019 began; FirstSeen (2020) is only when the app first saw it, not a lower bound");
            Assert.That(rows.Single(r => r.Gamertag == "Shadow S16" && r.Season.SeasonName == "Winter League 2019").User.UserName, Is.EqualTo("historical:Shadow S16"),
                "Someone else's 2019 gamertag: the account only took it in 2021");
        });
        // In 2023 the account had it
        Assert.That(rows.Single(r => r.Season.SeasonName == "Winter League 2023 (DA)").UserID, Is.EqualTo(now.Id));
    }

    [Test]
    public async Task Import_GivesARecentSeasonsGamertag_ToTheAccountThatHadItThen()
    {
        var now = new User { UserName = "now", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Shadow S16" } };
        _context.Users.Add(now);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Rookie", NormalizedGamertag = "ROOKIE", LastSeen = Utc(2021, 3), Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Shadow S16"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(now.Id), "Renamed before the season");
    }

    [Test]
    public async Task Import_AGamertagTwoAccountsHistoriesClaim_IsLeftToItsPlaceholder_WithAWarning()
    {
        var a = new User { UserName = "a", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "A Now" } };
        var b = new User { UserName = "b", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "B Now" } };
        _context.Users.AddRange(a, b);
        _context.XboxGamertagHistory.AddRange(
            new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Shared", NormalizedGamertag = "SHARED", Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 2, Gamertag = "Shared", NormalizedGamertag = "SHARED", Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        Assert.That(report.Warnings, Has.One.Contains("more than one Xbox account"));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.User.UserName).SingleAsync(), Is.EqualTo("historical:Shared"));
    }

    [Test]
    public async Task Import_AnAccountThatLetGoOfTheGamertagBeforeTheSeason_DoesntCompete()
    {
        var a = new User { UserName = "a", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "A Now" } };
        var b = new User { UserName = "b", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "B Now" } };
        _context.Users.AddRange(a, b);
        _context.XboxGamertagHistory.AddRange(
            new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Shared", NormalizedGamertag = "SHARED", FirstSeen = Utc(2025), LastSeen = Utc(2026), Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 2, Gamertag = "Shared", NormalizedGamertag = "SHARED", LastSeen = Utc(2015), Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(a.Id));
    }

    [Test]
    public async Task Import_ARenamedPlayersSheetRowInAWaypointSeason_IsNotCountedTwice()
    {
        var real = new User { UserName = "real", XboxUser = new XboxUser { XboxUserID = 100, Gamertag = "New Tag" } };
        _context.Users.Add(real);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 100, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", FirstSeen = Utc(2022), LastSeen = Utc(2024), Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();
        var match = Guid.NewGuid();
        await AddMatch(match, (100, "New Tag", Outcomes.Won, 30, 10));

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Old Tag", kills: 1))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] } },
        });

        var line = (await Lines()).Single();
        Assert.That((line.UserID, line.FromMatches), Is.EqualTo((real.Id, true)));
        Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(1));
        Assert.That(report.Warnings, Is.Empty, "Every sheet row matched");
    }

    [Test]
    public async Task Import_WaypointReplacesManual_OnlyForAPhaseWithEveryMatchPulled()
    {
        var regular = Guid.NewGuid();
        var playoff = Guid.NewGuid();
        var lost = Guid.NewGuid();
        await AddMatch(regular, (100, "Hells Angel cx", Outcomes.Won, 30, 10));
        await AddMatch(playoff, (100, "Hells Angel cx", Outcomes.Won, 5, 1));
        // The API doesn't return it: GetAndSaveMatch saves nothing

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(
                Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1)),
                Entry("Winter League 2023 (DA)", "infinite", "AGLA", true, Player("Hells Angel cx", kills: 2))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [regular.ToString()], Playoffs = [playoff.ToString(), lost.ToString()] } },
        });

        var stats = await Lines();
        Assert.Multiple(() =>
        {
            Assert.That(stats.Single(s => !s.IsPlayoffs).FromMatches, Is.True);
            Assert.That((stats.Single(s => s.IsPlayoffs).FromMatches, stats.Single(s => s.IsPlayoffs).Kills), Is.EqualTo((false, 2)), "A playoff match is missing: the playoffs are not linked");
            Assert.That(report.Skipped, Has.Some.Contains($"Waypoint match {lost}: not found"));
            Assert.That(report.MissingMatches, Has.One.Contains("playoffs: 1 of 2 matches missing").And.Contains(lost.ToString()));
            Assert.That(report.WaypointMatchesPulled, Is.Zero);
        });
    }

    [Test]
    public async Task Import_SuggestsRosterMatches_AndApprovingOneMergesThePlaceholder_WithoutCountingThePlayerTwice()
    {
        var teammate = new User { UserName = "teammate", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "Teammate" } };
        _context.Users.Add(teammate);
        await _context.SaveChangesAsync();
        var match = Guid.NewGuid();
        // XUID 2 played as "New B" on Teammate's team; the sheet lists that player as "Old B"
        await AddMatch(match, (1, "Teammate", Outcomes.Won, 10, 5), (2, "New B", Outcomes.Won, 20, 5), (3, "Opponent", Outcomes.Lost, 5, 30));
        var input = new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false,
                Player("Teammate", team: "Ram Ranch"), Player("Old B", kills: 99, team: "Ram Ranch"), Player("Opponent", team: "Others"))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] } },
        };

        var report = await _service.Import(input);

        var claim = await _context.GamertagClaims.AsNoTracking().Include(c => c.Team).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(report.SuggestionsCreated, Is.EqualTo(1));
            Assert.That(report.Warnings, Has.One.Contains("Old B matches no Xbox account"));
            Assert.That((claim.Kind, claim.XboxUserID, claim.Gamertag, claim.Team!.TeamName, claim.Status), Is.EqualTo((GamertagClaimKind.RosterMatch, 2L, "Old B", "Ram Ranch", GamertagClaimStatus.Pending)));
        });
        var again = await _service.Import(input);
        Assert.That((again.SuggestionsCreated, await _context.GamertagClaims.CountAsync()), Is.EqualTo((0, 1)), "Not suggested twice");
        Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.Gamertag == "Old B"), Is.EqualTo(1), "Not merged automatically");

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);
        _context.ChangeTracker.Clear();

        var newB = await _context.Users.SingleAsync(u => u.XboxUserID == 2);
        var history = await _context.XboxGamertagHistory.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((history.XboxUserID, history.Gamertag, history.Source, history.FirstSeen, history.LastSeen), Is.EqualTo((2L, "Old B", GamertagHistorySource.RosterMatch, (DateTime?)Utc(2023), (DateTime?)Utc(2024).AddSeconds(-1))));
            Assert.That((await Lines()).Where(s => s.UserID == newB.Id).Select(s => (s.FromMatches, s.Kills)), Is.EqualTo(new[] { (true, 20) }),
                "The placeholder's sheet row went with the merge: the games are the stats");
            Assert.That(await _context.Users.AnyAsync(u => u.UserName == "historical:Old B"), Is.False);
        });

        // And a re-run finds "Old B" through the history now
        var third = await _service.Import(input);
        Assert.That(third.Warnings, Has.None.Contains("Old B"));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.UserID == newB.Id), Is.Zero, "No stored totals for a player with games");
    }

    [Test]
    public async Task Import_AGamertagTwoAccountsStillHadAfterTheSeason_IsLeftToItsPlaceholder()
    {
        // Reused: both accounts' histories can include 2019, whatever their FirstSeen
        var a = new User { UserName = "a", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = "A Now" } };
        var b = new User { UserName = "b", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "B Now" } };
        _context.Users.AddRange(a, b);
        _context.XboxGamertagHistory.AddRange(
            new XboxGamertagHistory { XboxUserID = 1, Gamertag = "Shared", NormalizedGamertag = "SHARED", FirstSeen = Utc(2022), LastSeen = Utc(2023), Source = GamertagHistorySource.Manual },
            new XboxGamertagHistory { XboxUserID = 2, Gamertag = "Shared", NormalizedGamertag = "SHARED", FirstSeen = Utc(2024), LastSeen = Utc(2025), Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        Assert.That(report.Warnings, Has.One.Contains("more than one Xbox account"));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.User.UserName).SingleAsync(), Is.EqualTo("historical:Shared"));
    }

    [Test]
    public async Task Import_ComparesCurrentGamertagsNormalised()
    {
        var spaced = new User { UserName = "spaced", XboxUser = new XboxUser { XboxUserID = 1, Gamertag = " Shadow   S16 " } };
        var other = new User { UserName = "other", XboxUser = new XboxUser { XboxUserID = 2, Gamertag = "ShadowS16" } };
        _context.Users.AddRange(spaced, other);
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("shadow s16"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(spaced.Id), "Not the account without the space");
    }

    [Test]
    public async Task Import_ReportsProgressBeforeEachMatchPull_AndEachRetry()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var calls = 0;
        _dataPull.When(d => d.GetAndSaveMatch(first)).Do(_ => { if (calls++ == 0) throw new HttpRequestException("blip"); });
        var stages = new List<string>();

        await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Someone"))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [first.ToString(), second.ToString()] } },
        }, p => { stages.Add(p.Stage); return Task.CompletedTask; });

        Assert.That(stages, Is.SupersetOf(new[] { "Pulling Waypoint matches 0/2", "Pulling Waypoint matches 1/2", "Importing Winter League 2023 (DA)", "Done" }));
        Assert.That(stages, Has.One.StartsWith("Pulling Waypoint matches: retrying in"));
    }

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

    [Test]
    public async Task Import_AGamertagCorrectedInTheArchive_LeavesNoRowForTheOldOne()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silvaa", kills: 5, team: "Rooster"))));

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", kills: 5, team: "Rooster"))));

        _context.ChangeTracker.Clear();
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(new[] { "Silva" }));
        Assert.That(await _context.TeamPlayers.Select(tp => tp.User.DisplayName).ToListAsync(), Is.EqualTo(new[] { "Silva" }));
        Assert.That(report.StatsRemoved, Is.EqualTo(1));
    }

    [Test]
    public async Task Import_ATeamRenamedInTheArchive_IsReplaced_WithItsRosterRecordAndTitle()
    {
        var champions = (string team) => new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Open", Team = team, Roster = ["Silva"] }] };
        HistoricalArchiveInput Archive(string team) => new()
        {
            Seasons = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: team, wins: 9, losses: 1))).Seasons,
            Championships = champions(team),
        };
        await _service.Import(Archive("Roostr"));

        var report = await _service.Import(Archive("Rooster"));

        _context.ChangeTracker.Clear();
        var team = await _context.Teams.AsNoTracking().Include(t => t.TeamPlayers).SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((team.TeamName, team.RecordedWins, team.TeamPlayers.Count), Is.EqualTo(("Rooster", (int?)9, 1)), "The old team, its roster and record are gone");
            Assert.That(await _context.PlayerChampionships.Select(c => c.TeamID).ToListAsync(), Is.EqualTo(new[] { team.TeamID }));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.TeamID).ToListAsync(), Is.EqualTo(new int?[] { team.TeamID }));
            Assert.That(report.TeamsRemoved, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Import_APlayerTakenOffTheSheet_LosesTheRowAndRosterSpot()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"))));

        _context.ChangeTracker.Clear();
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(new[] { "Silva" }));
        Assert.That(await _context.TeamPlayers.Select(tp => tp.User.DisplayName).ToListAsync(), Is.EqualTo(new[] { "Silva" }));
    }

    [Test]
    public async Task Import_ATierRemovedOrItsTitleMoved_IsReflected()
    {
        var input = (params ArchiveChampionship[] titles) => new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Hen", team: "Hens"))).Seasons,
            Championships = new ChampionsArchive { Champions = [.. titles] },
        };
        await _service.Import(input(
            new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Pro", Team = "Rooster", Roster = ["Silva"] },
            new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Amateur", Team = "Hens", Roster = ["Hen"] }));

        // The Amateur title is gone from the record; the Pro one went to the Hens
        await _service.Import(input(new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Pro", Team = "Hens", Roster = ["Hen"] }));

        _context.ChangeTracker.Clear();
        var titles = await _context.PlayerChampionships.AsNoTracking().Select(c => new { c.Tier, c.Team.TeamName, c.GamertagUsed }).ToListAsync();
        Assert.That(titles.Select(t => (t.Tier, t.TeamName, t.GamertagUsed)), Is.EqualTo(new[] { ("Pro", "Hens", "Hen") }));
    }

    [Test]
    public async Task Import_ASeasonOfEntriesOfMoreThanOneGame_IsItsRegularSeasonsGame_EachRowItsEntrys_WithAWarning()
    {
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva")),
            Entry("Winter League 2019", "reach", "AGLA", true, Player("Silva"))));

        var season = await _context.Seasons.AsNoTracking().Include(s => s.GameVersion).SingleAsync();
        var rows = await _context.PlayerSeasonStats.AsNoTracking().Include(s => s.GameVersion).OrderBy(s => s.IsPlayoffs).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(season.GameVersion!.Slug, Is.EqualTo("halo3"));
            Assert.That(rows.Select(r => r.GameVersion.Slug), Is.EqualTo(new[] { "halo3", "reach" }));
            Assert.That(report.Warnings, Has.One.Contains("more than one game"));
        });
    }

    [Test]
    public async Task Import_WaypointGamesNoLongerListed_StopBeingTheSeasonsMatches()
    {
        var kept = Guid.NewGuid();
        var dropped = Guid.NewGuid();
        await AddMatch(kept, (100, "Hells Angel cx", Outcomes.Won, 30, 10));
        await AddMatch(dropped, (100, "Hells Angel cx", Outcomes.Won, 10, 10));
        HistoricalArchiveInput Archive(params Guid[] games) => new()
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", team: "Ram Ranch"))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [.. games.Select(g => g.ToString())] } },
        };
        await _service.Import(Archive(kept, dropped));

        var report = await _service.Import(Archive(kept));

        Assert.That(await _context.MatchLinks.Select(l => l.MatchID).ToListAsync(), Is.EqualTo(new[] { kept }));
        Assert.That(report.SeasonMatchesRemoved, Is.EqualTo(1));
        Assert.That((await Lines()).Single().Kills, Is.EqualTo(30));
    }

    private Task<List<SeasonStatLine>> Lines() => new SeasonStatsService(_context).GetAsync();

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

    private static ArchivePlayer Player(string gamertag, int kills = 0, int stint = 1, string? team = null, int wins = 0, int losses = 0) => new()
    {
        Gamertag = gamertag,
        Kills = kills,
        Wins = wins,
        Losses = losses,
        Deaths = 1,
        GamesPlayed = 1,
        StintIndex = stint,
        TeamName = team,
        KdRatio = 1.23456m,
    };
}
