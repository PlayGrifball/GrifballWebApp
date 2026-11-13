using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Application.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// HistoricalImportService.Run/Import end to end, against a fake SQLite context seeded only with the model's own reference
/// data (Leagues and GameVersions, HasData, so AGLA and halo3 resolve as they do against the real database). Each test
/// exercises a slice of the pipeline (GroupEntries/KeySeasons, UpsertSeason/UpsertTeams, the guardrail, matchmaking
/// seasons, recorded games, players lists) through its two public methods, same as GrifballWebApp.Integration.Test's
/// HistoricalImportServiceTests - whose Waypoint-match and most-nested-collection scenarios need either a real IHaloInfiniteClient
/// behind the Halo client's own Polly policy, or an Infrastructure-only SQLite provider feature, and stay there.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportServiceUnitTests : ApplicationDbContextTests
{
    private IDataPullService _dataPull = null!;
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _dataPull = Substitute.For<IDataPullService>();
        _service = new HistoricalImportService(Context, _dataPull, Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    // HistoricalSourceID is "{workbook}/{sheet}" verbatim (HistoricalKeys.SeasonSourceId): the workbook is slugged
    // (Entry's SourceWorkbook), but the sheet is Entry's literal "Stats"/"Playoff Stats". A season's regular and playoff
    // entries make one season, keyed by its regular season's sheet (HistoricalImportService.KeySeasons).
    private static readonly string[] WinterRegularSourceId = ["winter-league-2019/Stats"];
    private static readonly string[] LateLeagueSeason8SourceId = ["late-league-season-8-halo-3/Stats"];
    private static readonly string[] HensThenRooster = ["Hens", "Rooster"];
    private static readonly (int?, int?)[] HensThenRoosterRecords = [(2, 8), (9, 1)];
    private static readonly string[] RoosterTitleRoster = ["Silva", "Pie", "Ringer"];
    private static readonly string[] RealTeamOnly = ["RealTeam"];
    private static readonly string[] WinterLeague2019Only = ["Winter League 2019"];

    [Test]
    public async Task Import_MakesOneSeasonPerLeagueAndName_AndOnePlaceholderPerPlayer()
    {
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10), Player("x0 pie x0", kills: 3)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("shadow  s16 ", kills: 2)),
            Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", false, Player("SHADOW S16", kills: 4))));

        var seasons = await Context.Seasons.AsNoTracking().OrderBy(s => s.HistoricalSourceID).ToListAsync();
        var users = await Context.Users.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(seasons.Select(s => s.HistoricalSourceID), Is.EqualTo(WinterRegularSourceId));
            Assert.That(await Context.MatchmakingSeasons.Select(s => s.HistoricalSourceID).ToListAsync(), Is.EqualTo(LateLeagueSeason8SourceId));
            Assert.That(users, Has.Count.EqualTo(2), "One per player, whatever the case and spacing of their gamertag");
            Assert.That(users.Select(u => u.IsDummyUser), Is.All.True);
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.EqualTo(3));
            Assert.That(await Context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(1));
            Assert.That((report.SeasonsCreated, report.MatchmakingSeasonsCreated), Is.EqualTo((1, 1)));
            Assert.That(report.StatsInserted, Is.EqualTo(4));
            Assert.That(report.DummyUsersCreated, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_AgainWithTheSameArchive_ChangesNothing()
    {
        var input = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        await _service.Import(input);
        var before = (await Context.Seasons.CountAsync(), await Context.PlayerSeasonStats.CountAsync(), await Context.Users.CountAsync());
        Context.ChangeTracker.Clear();

        var again = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Context.Seasons.CountAsync(), await Context.PlayerSeasonStats.CountAsync(), await Context.Users.CountAsync()), Is.EqualTo(before));
            Assert.That((again.SeasonsCreated, again.StatsInserted, again.DummyUsersCreated, again.StatsUpdated), Is.EqualTo((0, 0, 0, 2)));
        }
    }

    [Test]
    public async Task Import_FindsAccountsByTheirCurrentGamertag_AndRenamedOnesByTheirGamertagHistory()
    {
        var byXbox = User.Seed(userName: "noah", xboxUser: XboxUser.FromXbox(1, "Shadow S16"));
        var byAlias = User.Seed(userName: "pie", xboxUser: XboxUser.FromXbox(2, "Pie Now"));
        Context.AddRange(byXbox, byAlias);
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(2, "x0 pie x0", lastSeen: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), source: GamertagHistorySource.RenameDetected));
        await Context.SaveChangesAsync();

        // The current-gamertag match (PlayerResolver.Find) compares x.Gamertag.Replace(" ", "") against the sheet's
        // cleaned gamertag: on SQLite, a column wrapped in REPLACE() loses the column's collation (BINARY, not the
        // NOCASE this layer's string columns otherwise get), so only an exact-case gamertag matches here; a spelling
        // only differing by case needs Infrastructure's real SQLite setup, covered there. The alias below is matched
        // by its gamertag history instead (a stored, unwrapped column, unaffected), case and all.
        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16"), Player("X0 Pie X0"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.DummyUsersCreated, Is.Zero);
            Assert.That(await Context.PlayerSeasonStats.Select(s => s.UserID).ToListAsync(), Is.EquivalentTo(new[] { byXbox.Id, byAlias.Id }));
        }
    }

    [Test]
    public async Task Import_MakesTheSeasonsTeams_WithTheirRostersRecordsAndTitles()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Winter League 2019", "halo3", "AGLA", false,
                        Player("Silva", team: "Rooster", wins: 9, losses: 1), Player("Pie", team: "rooster ", wins: 8, losses: 2),
                        Player("Benchy", team: "Hens", wins: 2, losses: 8)),
                    Entry("Winter League 2019", "halo3", "AGLA", true, Player("Silva", team: "Rooster", wins: 3, losses: 0)),
                ],
            },
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Open", Team = "Rooster", Captain = "Silva", Roster = ["Silva", "Pie", "Ringer"] }],
            },
        };

        var report = await _service.Import(input);

        Context.ChangeTracker.Clear();
        var teams = await Context.Teams.AsNoTracking().Include(t => t.TeamPlayers).ThenInclude(tp => tp.User).Include(t => t.Captain).ThenInclude(c => c!.User)
            .OrderBy(t => t.TeamName).ToListAsync();
        var titles = await Context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.TeamsCreated, Is.EqualTo(2));
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(HensThenRooster), "One team whatever the case and spacing of its name");
            Assert.That(teams.Select(t => (t.RecordedWins, t.RecordedLosses)), Is.EqualTo(HensThenRoosterRecords), "The regular season's, from the sheet");
            Assert.That(teams[1].TeamPlayers.Select(tp => tp.User.DisplayName), Is.EquivalentTo(RoosterTitleRoster), "Its title's roster too");
            Assert.That(teams[1].Captain!.User.DisplayName, Is.EqualTo("Silva"), "The title's captain");
            Assert.That(titles.Select(t => t.Team.TeamName), Is.All.EqualTo("Rooster"));
        }
    }

    [Test]
    public async Task Import_ExcludesHeaderAndSubstituteLabels_FromCreatingTeams()
    {
        var input = Input(Entry("Fall League 2020 (PC)", "reach", "AGLA", false,
            Player("Player1", team: "RealTeam"),
            Player("Player2", team: "Subs"),
            Player("Player3", team: "Sub"),
            Player("Player4", team: "Team"),
            Player("Player5", team: "Teams"),
            Player("Player6", team: "Team Name"),
            Player("Player7", team: "Substitute"),
            Player("Player8", team: "Sub Pool")));

        await _service.Import(input);

        var teams = await Context.Teams.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(RealTeamOnly));
            var stats = await Context.PlayerSeasonStats.AsNoTracking().ToListAsync();
            Assert.That(stats.Single(s => s.Gamertag == "Player1").TeamID, Is.Not.Null);
            Assert.That(stats.Where(s => s.Gamertag != "Player1").Select(s => s.TeamID), Is.All.Null);
        }
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
        Context.ChangeTracker.Clear();
        var again = await _service.Import(input);

        var titles = await Context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.ChampionshipsInserted, Is.EqualTo(6));
            Assert.That((again.ChampionshipsInserted, again.ChampionshipsUpdated, again.SeasonsCreated), Is.EqualTo((0, 6, 0)));
            Assert.That(titles, Has.Count.EqualTo(6));
            Assert.That(titles.Single(t => t.GamertagUsed == "xStarBlade").Season.SeasonName, Is.EqualTo("Summer League 2021 (Pro)"));
            Assert.That(titles.Single(t => t.GamertagUsed == "xStarBlade").IsCaptain, Is.True);
            Assert.That(titles.Single(t => t.GamertagUsed == "Silva").Season.SeasonName, Is.EqualTo("Winter League 2019"));
            var created = titles.Where(t => t.Tier is "Pro" or "Amateur" && t.Season.SeasonName.StartsWith("Winter")).Select(t => t.Season).ToList();
            Assert.That(created.Select(s => s.SeasonID).Distinct().Count(), Is.EqualTo(1), "Both tiers' titles in one titles-only season, whatever its name's case");
            Assert.That(await Context.Seasons.CountAsync(), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task Import_SkipsUnknownLeaguesAndGameVersions()
    {
        var report = await _service.Import(Input(
            Entry("Bad League Season", "halo3", "Nonexistent League", false, Player("P1")),
            Entry("Bad Game Season", "nonexistent-game", "AGLA", false, Player("P2"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Count.EqualTo(2));
            Assert.That(await Context.Seasons.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task Import_LogsEachSkipAndWhatItDid()
    {
        var logger = new CapturingLogger<HistoricalImportService>();
        var service = new HistoricalImportService(Context, _dataPull, Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            logger, new GamertagMatching(Context), TimeProvider.System);

        var report = await service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10)),
            Entry("Bad League Season", "halo3", "Nonexistent League", false, Player("P1"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SeasonsCreated, Is.EqualTo(1));
            Assert.That(report.Skipped, Has.Count.EqualTo(1));
            Assert.That(logger.Messages, Has.Member($"Historical import skipped: {report.Skipped[0]}"));
            Assert.That(logger.Entries, Has.Member((LogLevel.Information,
                $"Historical import: 1 seasons created, 0 updated, {report.StatsInserted} stats added, 0 updated, {report.DummyUsersCreated} placeholder users, 1 skipped, 0 failed")));
            Assert.That(report.StatsInserted, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Run_DryRun_RollsBackEverySeason_AndReportsTheRemovals()
    {
        var report = await _service.Run(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10))),
            new HistoricalImportRunOptions { DryRun = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Seasons.CountAsync(), Is.Zero, "Rolled back: nothing was kept");
            Assert.That(report.SeasonsCreated, Is.EqualTo(1), "The report still says what it would do");
            Assert.That(report.SeasonRemovals, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Run_Guardrail_StopsASeasonThatWouldRemoveTooMuch_ForReview()
    {
        var input = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10), Player("Pie", kills: 3), Player("Gone Now", kills: 1)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        await _service.Import(input);
        Context.ChangeTracker.Clear();

        var shrunk = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        var report = await _service.Run(shrunk, new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions { MaxRemovedFraction = 0.1m } });

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.EqualTo(4), "Stopped: the season is untouched, the rows the shrunk import would have removed are still there");
            Assert.That(report.SeasonRemovals.Single().Stopped, Is.True);
            Assert.That(report.Warnings, Has.Some.Contains("stopped for review"));
        }
    }

    [Test]
    public async Task Run_PartialRun_OnlyImportsTheSeasonsAsked()
    {
        var input = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("P1")), Entry("Spring League 2019", "halo3", "AGLA", false, Player("P2")));

        await _service.Run(input, new HistoricalImportRunOptions { Seasons = new HashSet<string> { "winter-league-2019/Stats" } });

        var seasons = await Context.Seasons.Select(s => s.SeasonName).ToListAsync();
        Assert.That(seasons, Is.EqualTo(WinterLeague2019Only));
    }

    [Test]
    public async Task Import_ASeasonWithRecordedGames_LinksThemAsASeriesAndStats()
    {
        var waypoint = new ArchiveSeason
        {
            SeasonName = "MCC Cup 2016",
            GameVersion = "halo3",
            League = "AGLA",
            SourceWorkbook = "mcc-cup-2016",
            SourceSheet = "Stats",
            Players = [Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta")],
        };
        var recorded = new RecordedGameInput
        {
            SeasonSourceId = "mcc-cup-2016/Stats",
            SourceKey = "mcc-cup-2016/Stats#1",
            GameVersion = "halo3",
            PlayedAt = new DateTime(2016, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Teams = [new RecordedTeamInput { Side = 1, Name = "Alpha", Outcome = GrifballWebApp.Domain.Halo.Outcomes.Won, Score = 50 },
                     new RecordedTeamInput { Side = 2, Name = "Beta", Outcome = GrifballWebApp.Domain.Halo.Outcomes.Lost, Score = 30 }],
            Players = [new RecordedPlayerInput { Gamertag = "Alpha One", Side = 1, Kills = 20, Deaths = 5, Assists = 2 },
                       new RecordedPlayerInput { Gamertag = "Beta One", Side = 2, Kills = 10, Deaths = 15, Assists = 1 }],
        };
        var input = new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [waypoint] }, RecordedGames = [recorded] };

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.RecordedGamesImported, Is.EqualTo(1));
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1), "The game became a season match");
        }
    }

    [Test]
    public async Task Import_ALateLeagueSeasonsRecordedGame_BecomesAFinishedMatchedMatch()
    {
        var season = new ArchiveSeason
        {
            SeasonName = "Late League Season 1 (Halo 3)",
            GameVersion = "halo3",
            League = "Late League",
            SourceWorkbook = "late-league-season-1",
            SourceSheet = "Stats",
            Players = [Player("Alpha One"), Player("Beta One")],
        };
        var recorded = new RecordedGameInput
        {
            SeasonSourceId = "late-league-season-1/Stats",
            SourceKey = "late-league-season-1/Stats#1",
            GameVersion = "halo3",
            PlayedAt = new DateTime(2016, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Teams = [new RecordedTeamInput { Side = 1, Outcome = GrifballWebApp.Domain.Halo.Outcomes.Won },
                     new RecordedTeamInput { Side = 2, Outcome = GrifballWebApp.Domain.Halo.Outcomes.Lost }],
            Players = [new RecordedPlayerInput { Gamertag = "Alpha One", Side = 1, Kills = 20, Deaths = 5, Assists = 2 },
                       new RecordedPlayerInput { Gamertag = "Beta One", Side = 2, Kills = 10, Deaths = 15, Assists = 1 }],
        };
        var input = new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [season] }, RecordedGames = [recorded] };

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.MatchmakingSeasonsCreated, Is.EqualTo(1));
            Assert.That(report.RecordedGamesImported, Is.EqualTo(1));
            Assert.That(await Context.MatchedMatches.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.Set<Team>().CountAsync(), Is.Zero, "Matchmaking has no teams");
        }
    }

    [Test]
    public async Task Import_SkipsASeasonWhoseNameTheLeagueAlreadyHas()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "Winter League 2019").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(1)));
        await Context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("P1"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.Contains("already has a season"));
            Assert.That(await Context.Seasons.CountAsync(), Is.EqualTo(1));
        }
    }

    // A season whose save fails (e.g. a column too long) is rolled back while the rest of the run still imports
    // (Import_ASeasonThatFails_IsRolledBack_AndTheOthersImport): on SQLite, which never enforces column lengths, that
    // save doesn't actually fail (GrifballWebApp.Integration.Test's own copy of this test Assert.Ignore()s for the same
    // reason), so this file has nothing that reproduces it; GrifballWebApp.Integration.Test covers it against SQL
    // Server and Postgres.

    [Test]
    public async Task Import_PlayersList_LinksAnUnheldDiscordIdToThePlaceholder()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16"))));
        Context.ChangeTracker.Clear();

        var withPlayers = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Players = [new ArchivePlayerInput { Gamertag = "Shadow S16", DiscordId = "123456789012345678", Region = "East" }],
        };
        var report = await _service.Import(withPlayers);

        var placeholder = await Context.Users.AsNoTracking().SingleAsync(u => u.IsDummyUser);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.DiscordLinked, Is.EqualTo(1));
            Assert.That(placeholder.DiscordUserID, Is.EqualTo(123456789012345678));
            Assert.That(await Context.DiscordUsers.AnyAsync(d => d.DiscordUserID == 123456789012345678), Is.True);
        }
    }

    [Test]
    public async Task Import_PlayersList_MergesThePlaceholderIntoTheDiscordLinkedAccount()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16"))));
        Context.ChangeTracker.Clear();
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(42, "RealDiscord"));
        var real = User.Seed(userName: "real", discordUserID: 42, xboxUser: XboxUser.FromXbox(5, "Shadow S16"));
        Context.Users.Add(real);
        await Context.SaveChangesAsync();

        var withPlayers = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Players = [new ArchivePlayerInput { Gamertag = "Shadow S16", DiscordId = "42" }],
        };
        var report = await _service.Import(withPlayers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.DiscordMerged, Is.EqualTo(1));
            Assert.That(await Context.Users.CountAsync(u => u.IsDummyUser), Is.Zero, "The placeholder merged into the real account");
        }
    }

    [Test]
    public async Task Import_PlayersList_FillsARegion_ButNeverOverwritesOne()
    {
        var real = User.Seed(userName: "real", xboxUser: XboxUser.FromXbox(5, "Shadow S16"), regionID: 2, regionSource: RegionSource.User);
        Context.Users.Add(real);
        await Context.SaveChangesAsync();
        var withPlayers = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Players = [new ArchivePlayerInput { Gamertag = "Shadow S16", Region = "East" }],
        };

        var report = await _service.Import(withPlayers);

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.RegionsSet, Is.Zero, "The user already chose a region: the list never overwrites it");
            Assert.That((await Context.Users.SingleAsync()).RegionID, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_WaypointMatches_ArePulled_AndMissingOnesAreReported()
    {
        var matchId = Guid.NewGuid();
        var season = new ArchiveSeason
        {
            SeasonName = "Infinite Cup 2023",
            GameVersion = "infinite",
            League = "AGLA",
            SourceWorkbook = "infinite-cup-2023",
            SourceSheet = "Stats",
            Players = [Player("Alpha One")],
        };
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [season] },
            WaypointMatches = new() { ["infinite-cup-2023/Stats"] = new WaypointSeasonMatches { Regular = [matchId.ToString()] } },
        };
        _dataPull.GetAndSaveMatch(matchId).Returns(Task.CompletedTask);

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            await _dataPull.Received(1).GetAndSaveMatch(matchId);
            Assert.That(report.MissingMatches, Has.Count.EqualTo(1), "GetAndSaveMatch didn't actually save a match row: it's reported missing");
        }
    }

    [Test]
    public async Task Import_APulledWaypointMatch_BecomesTheSeasonsMatchAndItsPlayersStats()
    {
        var matchId = Guid.NewGuid();
        await AddMatch(Context, matchId, (1, "Alpha One", Outcomes.Won, 20, 5), (2, "Beta One", Outcomes.Lost, 10, 15));
        var season = new ArchiveSeason
        {
            SeasonName = "Infinite Cup 2024",
            GameVersion = "infinite",
            League = "AGLA",
            SourceWorkbook = "infinite-cup-2024",
            SourceSheet = "Stats",
            Players = [Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta")],
        };
        var input = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [season] },
            WaypointMatches = new() { ["infinite-cup-2024/Stats"] = new WaypointSeasonMatches { Regular = [matchId.ToString()] } },
        };

        var report = await _service.Import(input);

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.MissingMatches, Is.Empty, "The match is in the database: its phase is complete");
            Assert.That(report.WaypointMatchesPulled, Is.Zero, "Already there: nothing new to pull");
            Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(2), "The sheet's rows for both players are replaced by their game");
            Assert.That(report.GamesLinked, Is.EqualTo(1));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.Zero, "Their stats now come from the game itself, not a stored sheet row");
            var match = await Context.SeasonMatches.SingleAsync();
            Assert.That((match.HomeTeamID, match.AwayTeamID), Is.Not.EqualTo(((int?)null, (int?)null)), "Both sides resolved to a season team");
        }
    }

    [Test]
    public async Task Import_AWaypointMatchNoLongerListed_StopsBeingTheSeasonsMatch()
    {
        var matchId = Guid.NewGuid();
        await AddMatch(Context, matchId, (1, "Alpha One", Outcomes.Won, 20, 5), (2, "Beta One", Outcomes.Lost, 10, 15));
        var season = new ArchiveSeason
        {
            SeasonName = "Infinite Cup 2024",
            GameVersion = "infinite",
            League = "AGLA",
            SourceWorkbook = "infinite-cup-2024",
            SourceSheet = "Stats",
            Players = [Player("Alpha One", team: "Alpha"), Player("Beta One", team: "Beta")],
        };
        var withMatch = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [season] },
            WaypointMatches = new() { ["infinite-cup-2024/Stats"] = new WaypointSeasonMatches { Regular = [matchId.ToString()] } },
        };
        await _service.Import(withMatch);
        Context.ChangeTracker.Clear();

        var withoutMatch = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [season] },
            WaypointMatches = new() { ["infinite-cup-2024/Stats"] = new WaypointSeasonMatches() },
        };
        var report = await _service.Import(withoutMatch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SeasonMatchesRemoved, Is.EqualTo(1));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
        }
    }

    // ---------- Re-import reconciliation: a roster changed, a player dropped, a title moved ----------

    private static readonly string[] RoosterRosterAfterTrade = ["Silva", "New"];

    [Test]
    public async Task Import_ARosterChangedInTheArchive_IsChangedOnTheTeam()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("New", team: "Rooster"))));

        Context.ChangeTracker.Clear();
        var roster = await Context.Set<TeamPlayer>().AsNoTracking().Select(tp => tp.User.DisplayName).ToListAsync();
        Assert.That(roster, Is.EquivalentTo(RoosterRosterAfterTrade));
    }

    [Test]
    public async Task Import_APlayerTakenOffTheSheet_LosesTheRowAndRosterSpot()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"))));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(RoosterRosterAfterTradeSingle));
            Assert.That(await Context.Set<TeamPlayer>().Select(tp => tp.User.DisplayName).ToListAsync(), Is.EqualTo(RoosterRosterAfterTradeSingle));
        }
    }

    private static readonly string[] RoosterRosterAfterTradeSingle = ["Silva"];

    [Test]
    public async Task Import_ATierRemovedOrItsTitleMoved_IsReflected()
    {
        HistoricalArchiveInput Input2(params ArchiveChampionship[] titles) => new()
        {
            Seasons = Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Hen", team: "Hens"))).Seasons,
            Championships = new ChampionsArchive { Champions = [.. titles] },
        };
        await _service.Import(Input2(
            new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Pro", Team = "Rooster", Roster = ["Silva"] },
            new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Amateur", Team = "Hens", Roster = ["Hen"] }));

        // The Amateur title is gone from the record; the Pro one went to the Hens
        await _service.Import(Input2(new ArchiveChampionship { Season = "Winter League 2019", Game = "Halo 3", Tier = "Pro", Team = "Hens", Roster = ["Hen"] }));

        Context.ChangeTracker.Clear();
        var titles = await Context.Set<PlayerChampionship>().AsNoTracking().Select(c => new { c.Tier, c.Team.TeamName, c.GamertagUsed }).ToListAsync();
        Assert.That(titles.Select(t => (t.Tier, t.TeamName, t.GamertagUsed)), Is.EqualTo(TierMovedResult));
    }

    private static readonly (string, string, string)[] TierMovedResult = [("Pro", "Hens", "Hen")];

    // ---------- A season's dates, from its sheet's timestamps, its name's year or its source's ----------

    [Test]
    public async Task ASeasonsDates_AreWhenItsGamesWerePlayed_FromItsSheetsTimeStamps()
    {
        var entry = new ArchiveSeason
        {
            SeasonName = "GWL Tournament Series #1", GameVersion = "halo4", League = "AGLA", SourceWorkbook = "gwl", SourceSheet = "Stats", SourceYear = 2022,
            FirstPlayedAt = new DateTime(2022, 10, 17, 21, 15, 0, DateTimeKind.Utc), LastPlayedAt = new DateTime(2022, 10, 18, 8, 1, 46, DateTimeKind.Utc),
            Players = [Player("Galvann", team: "Silly Kids")],
        };

        await _service.Import(Input(entry));

        var season = await Context.Seasons.AsNoTracking().SingleAsync();
        Assert.That((season.SeasonStart, season.SeasonEnd), Is.EqualTo((new DateTime(2022, 10, 17, 0, 0, 0, DateTimeKind.Utc), new DateTime(2022, 10, 18, 23, 59, 59, DateTimeKind.Utc))));
    }

    [Test]
    public async Task ASeasonsDates_IgnoreTimeStampsFarFromTheYearItsNameGives_AndFallBackToThatYear()
    {
        var entry = new ArchiveSeason
        {
            SeasonName = "Winter League 2019", GameVersion = "halo3", League = "AGLA", SourceWorkbook = "wl19", SourceSheet = "Stats",
            FirstPlayedAt = new DateTime(2023, 5, 1, 0, 0, 0, DateTimeKind.Utc), LastPlayedAt = new DateTime(2023, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            Players = [Player("Silva")],
        };

        await _service.Import(Input(entry));

        var season = await Context.Seasons.AsNoTracking().SingleAsync();
        Assert.That((season.SeasonStart, season.SeasonEnd), Is.EqualTo((new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 12, 31, 23, 59, 59, DateTimeKind.Utc))),
            "A sheet rebuilt years later doesn't move the season");
    }

    [Test]
    public async Task AnUndatedSeason_WithoutAYearInItsName_IsItsSourcesYear()
    {
        var entry = new ArchiveSeason { SeasonName = "Respawn Fall Series 1", GameVersion = "halo4", League = "AGLA", SourceWorkbook = "respawn", SourceSheet = "Stats", SourceYear = 2021, Players = [Player("Galvann")] };

        await _service.Import(Input(entry));

        Assert.That((await Context.Seasons.AsNoTracking().SingleAsync()).SeasonStart, Is.EqualTo(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "Not 2008");
    }

    // ---------- The schedule names a team for players the sheet names none for (#216) ----------

    private const string ScheduleSeasonKey = "winter-league-2023-da/Stats";

    /// <summary>Three teams' games: Red (Red One, Red Two, and Red Sub in one game), Blue (Blue One, Blue Two) and Gold
    /// (Gold One, and Gold Two in one game), each pair once, as a schedule names them.</summary>
    private async Task<(Guid[] Ids, List<ScheduledGame> Schedule)> ThreeTeamsGames(string red = "Red")
    {
        var (redBlue, redGold, goldBlue) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await AddMatch(Context, redBlue, (1, "Red One", Outcomes.Won, 5, 1), (2, "Red Two", Outcomes.Won, 5, 1), (3, "Blue One", Outcomes.Lost, 1, 5), (4, "Blue Two", Outcomes.Lost, 1, 5));
        await AddMatch(Context, redGold, (1, "Red One", Outcomes.Won, 5, 1), (7, "Red Sub", Outcomes.Won, 5, 1), (5, "Gold One", Outcomes.Lost, 1, 5));
        await AddMatch(Context, goldBlue, (5, "Gold One", Outcomes.Won, 5, 1), (6, "Gold Two", Outcomes.Won, 5, 1), (3, "Blue One", Outcomes.Lost, 1, 5), (4, "Blue Two", Outcomes.Lost, 1, 5));
        return ([redBlue, redGold, goldBlue],
        [
            new ScheduledGame { MatchId = redBlue.ToString(), Home = red, Away = "Blue" },
            new ScheduledGame { MatchId = $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{redGold}", Home = red, Away = "Gold" },
            new ScheduledGame { MatchId = goldBlue.ToString(), Home = "Gold", Away = "Blue" },
        ]);
    }

    private async Task<Dictionary<string, string[]>> Rosters() => (await Context.Set<TeamPlayer>().AsNoTracking()
            .Select(tp => new { tp.Team.TeamName, tp.User.DisplayName }).ToListAsync())
        .GroupBy(tp => tp.TeamName).ToDictionary(g => g.Key, g => g.Select(tp => tp.DisplayName!).Order().ToArray());

    [Test]
    public async Task Import_PutsPlayersWithoutASheetTeam_OnTheTeamTheScheduleNamesInTheirGames()
    {
        var (ids, schedule) = await ThreeTeamsGames(red: "Red Captain");
        var input = new HistoricalArchiveInput
        {
            // Red One's sheet names his team (as the schedule names its captain): the rest of the sheet names none
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Red One", team: "Red Squad"), Player("Red Two"), Player("Blue One"))).Seasons,
            WaypointMatches = new() { [ScheduleSeasonKey] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString())], Schedule = schedule } },
        };

        var report = await _service.Import(input);

        var rosters = await Rosters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(rosters.ContainsKey("Red Squad"), Is.True, "'Red Captain' is the team its players' sheet names, not a second one");
            Assert.That(rosters["Red Squad"], Is.EqualTo(RedSquadRoster), "In all their games, or (one game) on the side the rest of their team is on");
            Assert.That(rosters["Blue"], Is.EqualTo(BlueRoster));
            Assert.That(rosters["Gold"], Is.EqualTo(GoldRoster));
            Assert.That(report.Warnings, Has.None.Contains("none of the season's teams"));
        }
    }

    private static readonly string[] RedSquadRoster = ["Red One", "Red Sub", "Red Two"];
    private static readonly string[] BlueRoster = ["Blue One", "Blue Two"];
    private static readonly string[] GoldRoster = ["Gold One", "Gold Two"];

    [Test]
    public async Task Import_PlaceholderNames_OnTheScheduleOrSheet_MakeNoTeam()
    {
        var (ids, schedule) = await ThreeTeamsGames(red: "Subs");
        var report = await _service.Import(new HistoricalArchiveInput
        {
            // Blue One's sheet names only a placeholder: the schedule still puts him on Blue
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Blue One", team: "Free Agent"), Player("Red One"))).Seasons,
            WaypointMatches = new() { [ScheduleSeasonKey] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString())], Schedule = schedule } },
        });

        var rosters = await Rosters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(rosters.ContainsKey("Subs"), Is.False, "No 'Subs' team");
            Assert.That(rosters.ContainsKey("Free Agent"), Is.False, "No 'Free Agent' team");
            Assert.That(rosters["Blue"], Is.EqualTo(BlueRoster));
            Assert.That(rosters["Gold"], Is.EqualTo(GoldRoster));
        }
    }
}
