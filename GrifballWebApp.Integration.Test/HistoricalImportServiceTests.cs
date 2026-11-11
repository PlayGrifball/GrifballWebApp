using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Sources;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Infrastructure.Historical.Parsing;
using GrifballWebApp.Infrastructure.Historical.Sources;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportServiceTests
{
    private static readonly string[] expected1 = ["Silva", "Pie", "Ringer"];
    private static readonly string[] expected2 = ["RealTeam"];
    private static readonly string[] expected3 = ["Free Ship"];
    private static readonly string[] expected4 = ["Silva", "New"];
    private static readonly string[] expected5 = ["Blue", "Gold", "Red Squad"];
    private static readonly string[] expected6 = ["Blue v Red Squad", "Gold v Red Squad", "Blue v Gold"];
    private static readonly string[] expected7 = ["Blue", "Gold"];
    private static readonly string[] expected8 = ["Blue", "Gold", "Octane Kings"];
    private static readonly string[] expected9 = ["Red One", "Red Sub", "Red Two"];
    private static readonly string[] expected10 = ["Blue", "Gold", "Red"];
    private static readonly string[] expected11 = ["Blue", "Red"];
    private static readonly string[] expected12 = ["Manual Only"];
    private static readonly string[] expected13 = ["Spring League 2019"];
    private static readonly string[] expected14 = ["Summer League 2025 (Classic Pro)"];
    private static readonly string[] expected15 = ["Winter League 2019"];
    private static readonly string[] strings = ["Pulling Waypoint matches 0/2", "Pulling Waypoint matches 1/2", "Importing Winter League 2023 (DA)", "Done"];
    private static readonly string[] expected16 = ["Late League Season 7 (Halo 5)"];
    private static readonly string[] expected17 = ["Four Guys One Ball"];
    private static readonly string[] expected18 = ["Silva"];
    private static readonly string[] expected19 = ["Silva"];
    private static readonly string[] expected20 = ["halo3", "reach"];
    private static readonly string[] expected21 = ["About73Puppies", "Veritas"];
    private GrifballContext _context;
    private IDataPullService _dataPull;
    private HistoricalImportService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        _dataPull = Substitute.For<IDataPullService>();
        _service = new HistoricalImportService(_context, _dataPull, Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }), NullLogger<HistoricalImportService>.Instance,
            new GamertagMatching(_context), TimeProvider.System);
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(seasons.Select(s => s.HistoricalSourceID), Is.EquivalentTo(expected));
            Assert.That(await _context.MatchmakingSeasons.Select(s => s.HistoricalSourceID).ToListAsync(), Is.EqualTo(expectedArray), "Late League is matchmaking");
            Assert.That(seasons.Select(s => s.PublicAt), Is.EqualTo(seasons.Select(s => (DateTime?)s.SeasonStart)), "Public since they began");
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2019").SeasonStart, Is.EqualTo(new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(users, Has.Count.EqualTo(2), "One per player, whatever the case and spacing of their gamertag");
            Assert.That(users.Select(u => u.IsDummyUser), Is.All.True);
            Assert.That(users.Select(u => u.UserName), Is.EquivalentTo(expectedArray0));
            Assert.That(users.Select(u => u.DisplayName), Is.EquivalentTo(expectedArray1));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(3));
            Assert.That(await _context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(1));
            Assert.That(await _context.Set<XboxGamertagHistory>().CountAsync(), Is.Zero, "No gamertag history from spellings");
            Assert.That((report.SeasonsCreated, report.MatchmakingSeasonsCreated), Is.EqualTo((1, 1)));
            Assert.That(report.StatsInserted, Is.EqualTo(4));
            Assert.That(report.DummyUsersCreated, Is.EqualTo(2));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Counts(), Is.EqualTo(before));
            Assert.That((again.SeasonsCreated, again.StatsInserted, again.DummyUsersCreated, again.StatsUpdated), Is.EqualTo((0, 0, 0, 4)));
        }

        var changed = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 99), Player("Traded", kills: 1, stint: 1), Player("Traded", kills: 2, stint: 2)),
            Entry("Winter League 2019", "halo3", "AGLA", true, Player("Shadow S16", kills: 2)));
        await _service.Import(changed);

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Counts(), Is.EqualTo(before));
            Assert.That(await _context.PlayerSeasonStats.OrderBy(s => s.PlayerSeasonStatID).Select(s => s.PlayerSeasonStatID).ToListAsync(), Is.EqualTo(ids));
            Assert.That(await _context.PlayerSeasonStats.Where(s => s.Gamertag == "Shadow S16" && !s.IsPlayoffs).Select(s => s.Kills).SingleAsync(), Is.EqualTo(99));
        }
    }

    [Test]
    public async Task Import_FindsAccountsByTheirCurrentGamertag_AndRenamedOnesByTheirGamertagHistory()
    {
        var byXbox = User.Seed(userName: "noah", xboxUser: XboxUser.FromXbox(1, "Shadow S16"));
        var byAlias = User.Seed(userName: "pie", xboxUser: XboxUser.FromXbox(2, "Pie Now"));
        _context.AddRange(byXbox, byAlias);
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(2, "x0 pie x0", lastSeen: Utc(2020), source: GamertagHistorySource.RenameDetected));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("shadow s16"), Player("X0 Pie X0"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.DummyUsersCreated, Is.Zero);
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).ToListAsync(), Is.EquivalentTo(new[] { byXbox.Id, byAlias.Id }));
        }
    }

    [Test]
    public async Task Import_AfterThePlaceholderWasMergedIntoAnAccount_StillFindsThatAccount()
    {
        var input = Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Old Tag", kills: 5)),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Old Tag", kills: 6)));
        await _service.Import(input);
        var dummy = await _context.Users.SingleAsync(u => u.IsDummyUser);
        var real = User.Seed(userName: "real");
        _context.Users.Add(real);
        await _context.SaveChangesAsync();
        await new UserMergeService(_context).Merge(real.Id, dummy.Id);
        _context.ChangeTracker.Clear();

        var again = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.DummyUsersCreated, Is.Zero, "No second placeholder for the old gamertag");
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).Distinct().ToListAsync(), Is.EqualTo(new[] { real.Id }));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_FindsAPlaceholderWithoutRowsByItsUserName()
    {
        var dummy = User.Seed(userName: "historical:Old Tag", normalizedUserName: "HISTORICAL:OLD TAG", isDummyUser: true);
        _context.Users.Add(dummy);
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("old tag"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(dummy.Id));
            Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
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
        var again = await _service.Import(input);

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).Include(c => c.User).ToListAsync();
        using (Assert.EnterMultipleScope())
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
        }
    }

    [Test]
    public async Task Import_WhenTieredStatsArrive_SupersedesUntieredChampionsPlaceholder()
    {
        // 1. Initial import with just champions creates an untiered placeholder "Winter League 2011"
        var champions = new ChampionsArchive
        {
            Champions =
            [
                new ArchiveChampionship { Season = "Winter League 2011", Game = "Halo Reach", Tier = "Pro", Team = "Cabs", Captain = "halfbreed105", Roster = ["halfbreed105"] },
                new ArchiveChampionship { Season = "Winter League 2011", Game = "Halo Reach", Tier = "Amateur", Team = "Downstait", Captain = "Hoya16", Roster = ["Hoya16"] },
                new ArchiveChampionship { Season = "Winter League 2012", Game = "Halo Reach", Tier = "Open", Team = "Lake Greats", Captain = "Kal", Roster = ["Kal"] },
            ],
        };
        var initial = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive(),
            Championships = champions,
        });
        Assert.That(initial.SeasonsCreated, Is.EqualTo(2));
        var placeholder11 = await _context.Seasons.SingleOrDefaultAsync(s => s.SeasonName == "Winter League 2011");
        var placeholder12 = await _context.Seasons.SingleOrDefaultAsync(s => s.SeasonName == "Winter League 2012");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(placeholder11, Is.Not.Null);
            Assert.That(placeholder12, Is.Not.Null);
        }

        // 2. Second import brings real stats:
        // - "Winter League 2011 (Pro)" & "Winter League 2011 (Amateur)" (tiered split)
        // - "Winter League 2012" (exact name match)
        var statsInput = new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive
            {
                Seasons =
                [
                    Entry("Winter League 2011 (Pro)", "reach", "AGLA", false, Player("halfbreed105", team: "Cabs")),
                    Entry("Winter League 2011 (Amateur)", "reach", "AGLA", false, Player("Hoya16", team: "Downstait")),
                    Entry("Winter League 2012", "reach", "AGLA", false, Player("Kal", team: "Lake Greats")),
                ],
            },
            Championships = champions,
        };

        var second = await _service.Import(statsInput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Errors, Is.Empty);
            // Untiered 2011 placeholder retired, Pro & Amateur created, 2012 placeholder adopted (updated)
            Assert.That(second.SeasonsCreated, Is.EqualTo(2), "Created Pro and Amateur");
            Assert.That(second.SeasonsUpdated, Is.EqualTo(1), "Adopted 2012 placeholder");
        }

        var seasons = await _context.Seasons.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasons.Any(s => s.SeasonName == "Winter League 2011"), Is.False, "Untiered placeholder cleanly retired");
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2011 (Pro)").HistoricalSourceID, Does.Not.StartWith("history-agla-champions"));
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2011 (Amateur)").HistoricalSourceID, Does.Not.StartWith("history-agla-champions"));
            Assert.That(seasons.Single(s => s.SeasonName == "Winter League 2012").HistoricalSourceID, Does.Not.StartWith("history-agla-champions"));
        }

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).Include(c => c.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(titles.Single(t => t.GamertagUsed == "halfbreed105").Season.SeasonName, Is.EqualTo("Winter League 2011 (Pro)"));
            Assert.That(titles.Single(t => t.GamertagUsed == "Hoya16").Season.SeasonName, Is.EqualTo("Winter League 2011 (Amateur)"));
            Assert.That(titles.Single(t => t.GamertagUsed == "Kal").Season.SeasonName, Is.EqualTo("Winter League 2012"));
        }
    }

    private async Task XboxAccountThatHad(long xuid, string current, string earlier)
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(xuid, current));
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(xuid, earlier, source: GamertagHistorySource.HistoricalImport));
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task AGamertagAnAccountNoUserHoldsHad_LinksItsPlaceholderToTheAccount()
    {
        var placeholder = User.Seed(userName: HistoricalKeys.DummyUserName("Old Tag"), normalizedUserName: HistoricalKeys.DummyUserName("Old Tag").ToUpperInvariant(), displayName: "Old Tag", isDummyUser: true);
        _context.Users.Add(placeholder);
        await XboxAccountThatHad(55, "New Tag", "Old Tag");

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2015", "halo3", "AGLA", false, Player("Old Tag", team: "Wolves"))] },
        });

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await _context.Users.Where(u => u.Id == placeholder.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(55));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(placeholder.Id));
        }
    }

    [Test]
    public async Task AGamertagAnAccountNoUserHoldsHad_WithoutAPlaceholder_GetsOneForTheAccount()
    {
        await XboxAccountThatHad(56, "Newer Tag", "Older Tag");

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2015", "halo3", "AGLA", false, Player("Older Tag", team: "Wolves"))] },
        });

        _context.ChangeTracker.Clear();
        var userId = await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync();
        var made = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((made.IsDummyUser, made.XboxUserID), Is.EqualTo((true, (long?)56)));
        }
    }

    [Test]
    public async Task AChampionshipNamingNoTeam_IsSkipped_AndTheSeasonStillImports()
    {
        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2022", "halo3", "AGLA", false, Player("Alpha", team: "Wolves"))] },
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Winter League 2022", Game = "Halo 3", Tier = "Open", Team = " ", Captain = "Alpha", Roster = ["Alpha"] }],
            },
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.Skipped, Has.Some.Contains("Open champions: no team named"));
            Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonName == "Winter League 2022"), Is.True);
            Assert.That(await _context.Set<PlayerChampionship>().AnyAsync(), Is.False);
        }
    }

    private static ChampionsArchive LegacyAndRookieChampions() => new()
    {
        Champions =
        [
            new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Legacy", Team = "Wolves", Captain = "Alpha", Roster = ["Alpha"] },
            new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Rookie", Team = "Cubs", Captain = "Beta", Roster = ["Beta"] },
        ],
    };

    private static HistoricalArchiveInput LegacyAndRookieStats(ChampionsArchive champions) => new()
    {
        Seasons = new UnifiedArchive
        {
            Seasons =
            [
                Entry("Winter League 2021 (Legacy)", "halo3", "AGLA", false, Player("Alpha", team: "Wolves")),
                Entry("Winter League 2021 (Rookie)", "halo3", "AGLA", false, Player("Beta", team: "Cubs")),
            ],
        },
        Championships = champions,
    };

    [Test]
    public async Task Import_ATitlesOnlySeason_IsRetiredWhateverTheTiersAreCalled_OnlyOnceEveryTitleMoved_AndARunAgainChangesNothing()
    {
        var champions = LegacyAndRookieChampions();
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Championships = champions });
        Assert.That(await _context.Seasons.CountAsync(s => s.SeasonName == "Winter League 2021"), Is.EqualTo(1), "One titles-only season holds both tiers' titles");

        // Only the Legacy tier has stats: the Rookie title still needs the titles-only season, so it stays
        var legacyOnly = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = new UnifiedArchive { Seasons = [Entry("Winter League 2021 (Legacy)", "halo3", "AGLA", false, Player("Alpha", team: "Wolves"))] },
            Championships = champions,
        });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(legacyOnly.Errors, Is.Empty);
            Assert.That(legacyOnly.SeasonsRetired, Is.Zero);
        }
        var rookie = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).SingleAsync(c => c.GamertagUsed == "Beta");
        Assert.That(rookie.Season.SeasonName, Is.EqualTo("Winter League 2021"), "The Rookie title isn't lost");

        // Both tiers have stats now: the titles-only season has nothing left and goes
        var both = LegacyAndRookieStats(champions);
        var tiered = await _service.Import(both);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tiered.Errors, Is.Empty);
            Assert.That(tiered.SeasonsRetired, Is.EqualTo(1));
            Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonName == "Winter League 2021"), Is.False);
        }
        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).ToListAsync();
        Assert.That(titles.Select(t => (t.GamertagUsed, t.Season.SeasonName)),
            Is.EquivalentTo(new[] { ("Alpha", "Winter League 2021 (Legacy)"), ("Beta", "Winter League 2021 (Rookie)") }));

        // The same files again: nothing is made or retired
        var again = await _service.Import(both);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.SeasonsCreated, Is.Zero);
            Assert.That(again.SeasonsRetired, Is.Zero);
            Assert.That(again.ChampionshipsRemoved, Is.Zero);
        }
    }

    [Test]
    public async Task Import_ATitlesOnlySeasonSomethingElsePointsAt_IsKept_APartialRunLeavesIt_AndTheGuardrailStopsIt()
    {
        var champions = LegacyAndRookieChampions();
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Championships = champions });
        var titlesOnly = await _context.Seasons.AsNoTracking().SingleAsync(s => s.SeasonName == "Winter League 2021");
        var tiers = LegacyAndRookieStats(champions);

        // A partial run that wasn't asked for the titles-only season leaves it
        var partial = await _service.Run(tiers, new HistoricalImportRunOptions { Seasons = new HashSet<string> { "nothing-asked/for" } });
        Assert.That(partial.SeasonsRetired, Is.Zero);

        // A VOD linked to it keeps it, with a warning saying why
        _context.Vods.Add(Vod.Seed("wl21video00", "WL21 Final", new DateTime(2021, 3, 1), VodMatchStatus.Confirmed, titlesOnly.SeasonID));
        await _context.SaveChangesAsync();
        var linked = await _service.Import(tiers);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(linked.Errors, Is.Empty);
            Assert.That(linked.SeasonsRetired, Is.Zero);
            Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.True);
            Assert.That(linked.Warnings, Has.Some.Contains(titlesOnly.HistoricalSourceID).And.Contains("VODs"));
        }

        // Without the VOD, an automatic run with the guardrail stops it for review instead of removing titles
        await _context.Vods.ExecuteDeleteAsync();
        _context.ChangeTracker.Clear();
        var guarded = await _service.Run(tiers, new HistoricalImportRunOptions { Guardrail = new HistoricalGuardrailOptions { StopOnAnyChampionshipRemoved = true } });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(guarded.SeasonsRetired, Is.Zero);
            Assert.That(guarded.SeasonRemovals.Any(r => r.SeasonKey == titlesOnly.HistoricalSourceID && r.Stopped), Is.True);
            Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.True);
        }
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
        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That((report.TeamsCreated, again.TeamsCreated), Is.EqualTo((2, 0)), "Found again by name on a re-run");
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(expectedArray2), "One team whatever the case and spacing of its name");
            Assert.That(teams.Select(t => (t.RecordedWins, t.RecordedLosses)), Is.EqualTo(new (int?, int?)[] { (2, 8), (9, 1) }), "The regular season's, from the sheet");
            Assert.That(teams[0].TeamPlayers.Select(tp => tp.User.DisplayName), Is.EquivalentTo(expectedArray3));
            Assert.That(teams[1].TeamPlayers.Select(tp => tp.User.DisplayName), Is.EquivalentTo(expectedArray4), "Its title's roster too");
            Assert.That(teams[1].Captain!.User.DisplayName, Is.EqualTo("Silva"), "The title's captain");
            Assert.That(teams[0].CaptainID, Is.Null);
            Assert.That(stats.Where(s => s.Gamertag == "Traded").OrderBy(s => s.StintIndex).Select(s => s.Team!.TeamName), Is.EqualTo(expectedArray5));
            Assert.That(stats.Single(s => s.Gamertag == "Free Agent").TeamID, Is.Null);
            Assert.That(stats.Single(s => s.Gamertag == "Silva" && s.IsPlayoffs).Team!.TeamName, Is.EqualTo("Rooster"));
            Assert.That(titles.Select(t => t.Team.TeamName), Is.All.EqualTo("Rooster"));
            Assert.That(titles.Select(t => t.GamertagUsed), Is.EquivalentTo(expected1));
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

        var teams = await _context.Teams.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(expected2));
            var stats = await _context.PlayerSeasonStats.AsNoTracking().ToListAsync();
            Assert.That(stats.Single(s => s.Gamertag == "Player1").TeamID, Is.Not.Null);
            Assert.That(stats.Where(s => s.Gamertag != "Player1").Select(s => s.TeamID), Is.All.Null);
        }
    }

    [Test]
    public async Task Import_SpringLeague2020_ReachPC_OnlyAwardsOpenTitle_AndLeavesConsoleTitleInTitlesSeason()
    {
        var champions = new ChampionsArchive
        {
            Champions =
            [
                new ArchiveChampionship { Season = "Spring League 2020", Game = "MCC Halo Reach", Tier = "Open", Team = "Free Ship", Roster = ["Regresser"] },
                new ArchiveChampionship { Season = "Spring League 2020", Game = "MCC Halo Reach", Tier = "Console", Team = "Zero Gravity", Roster = ["xStarBlade"] },
            ]
        };
        var reachPc = new ArchiveSeason
        {
            SeasonName = "Spring League 2020 (Reach PC)", GameVersion = "reach", League = "AGLA", Division = "Open", Titles = "Spring League 2020 (Open)",
            SourceWorkbook = "side-reach-pc-stats", SourceSheet = "Team & Player Stats", Players = [Player("Regresser", team: "Free Ship")],
        };

        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [reachPc] }, Championships = champions });

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).Include(c => c.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            var openTitle = titles.Single(t => t.GamertagUsed == "Regresser");
            Assert.That(openTitle.Season.SeasonName, Is.EqualTo("Spring League 2020 (Reach PC)"));
            Assert.That(openTitle.Team.TeamName, Is.EqualTo("Free Ship"));

            var consoleTitle = titles.Single(t => t.GamertagUsed == "xStarBlade");
            Assert.That(consoleTitle.Season.SeasonName, Is.EqualTo("Spring League 2020"));
            Assert.That(consoleTitle.Team.TeamName, Is.EqualTo("Zero Gravity"));

            var pcTeams = await _context.Teams.AsNoTracking().Where(t => t.Season.SeasonName == "Spring League 2020 (Reach PC)").Select(t => t.TeamName).ToListAsync();
            Assert.That(pcTeams, Is.EqualTo(expected3));
        }
    }

    [Test]
    public async Task Import_ARosterChangedInTheArchive_IsChangedOnTheTeam()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("New", team: "Rooster"))));

        var roster = await _context.Set<TeamPlayer>().AsNoTracking().Select(tp => tp.User.DisplayName).ToListAsync();
        Assert.That(roster, Is.EquivalentTo(expected4));
    }

    private const string ScheduleSeason = "winter-league-2023-da/Stats";
    private static readonly string[] expected = new[] { "winter-league-2019/Stats" };
    private static readonly string[] expectedArray = new[] { "late-league-season-8-halo-3/Stats" };
    private static readonly string[] expectedArray0 = new[] { "historical:Shadow S16", "historical:x0 pie x0" };
    private static readonly string[] expectedArray1 = new[] { "Shadow S16", "x0 pie x0" };
    private static readonly string[] expectedArray2 = new[] { "Hens", "Rooster" };
    private static readonly string[] expectedArray3 = new[] { "Traded", "Benchy" };
    private static readonly string[] expectedArray4 = new[] { "Silva", "Pie", "Traded", "Ringer" };
    private static readonly string[] expectedArray5 = new[] { "Rooster", "Hens" };

    /// <summary>Three teams' games: Red (Red One, Red Two, and Red Sub in one game), Blue (Blue One, Blue Two) and Gold
    /// (Gold One, and Gold Two in one game), each pair once, as a schedule names them.</summary>
    private async Task<(Guid[] Ids, List<ScheduledGame> Schedule)> ThreeTeamsGames(string red = "Red")
    {
        var (redBlue, redGold, goldBlue) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await AddMatch(redBlue, (1, "Red One", Outcomes.Won, 5, 1), (2, "Red Two", Outcomes.Won, 5, 1), (3, "Blue One", Outcomes.Lost, 1, 5), (4, "Blue Two", Outcomes.Lost, 1, 5));
        await AddMatch(redGold, (1, "Red One", Outcomes.Won, 5, 1), (7, "Red Sub", Outcomes.Won, 5, 1), (5, "Gold One", Outcomes.Lost, 1, 5));
        await AddMatch(goldBlue, (5, "Gold One", Outcomes.Won, 5, 1), (6, "Gold Two", Outcomes.Won, 5, 1), (3, "Blue One", Outcomes.Lost, 1, 5), (4, "Blue Two", Outcomes.Lost, 1, 5));
        return ([redBlue, redGold, goldBlue],
        [
            new ScheduledGame { MatchId = redBlue.ToString(), Home = red, Away = "Blue" },
            new ScheduledGame { MatchId = $"https://www.halowaypoint.com/halo-infinite/players/x/matches/{redGold}", Home = red, Away = "Gold" },
            new ScheduledGame { MatchId = goldBlue.ToString(), Home = "Gold", Away = "Blue" },
        ]);
    }

    private async Task<Dictionary<string, string[]>> Rosters() => (await _context.Set<TeamPlayer>().AsNoTracking()
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
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString())], Schedule = schedule } },
        };

        var report = await _service.Import(input);

        var teams = await _context.Teams.AsNoTracking().OrderBy(t => t.TeamName).ToListAsync();
        var series = await _context.SeasonMatches.AsNoTracking().Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName }).ToListAsync();
        var again = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(teams.Select(t => t.TeamName), Is.EqualTo(expected5), "'Red Captain' is the team its players' sheet names, not a second one");
            Assert.That(teams.Select(t => t.RecordedWins), Is.EqualTo(new int?[] { null, null, 0 }), "The schedule's teams' records come from their series; Red Squad's is its sheet row's");
            Assert.That(await Rosters(), Is.EqualTo(new Dictionary<string, string[]>
            {
                ["Blue"] = ["Blue One", "Blue Two"],
                ["Gold"] = ["Gold One", "Gold Two"],
                ["Red Squad"] = ["Red One", "Red Sub", "Red Two"],
            }), "In all their games, or (one game) on the side the rest of their team is on");
            Assert.That(series.Select(s => string.Join(" v ", new[] { s.Home, s.Away }.Order())), Is.EquivalentTo(expected6));
            Assert.That(report.Warnings, Has.None.Contains("none of the season's teams"));
            Assert.That(again.TeamsCreated, Is.Zero);
            Assert.That(await Rosters(), Has.Count.EqualTo(3), "A second run keeps the rosters");
        }
    }

    [Test]
    public async Task Import_PlaceholderNames_OnTheScheduleOrSheet_MakeNoTeam()
    {
        var (ids, schedule) = await ThreeTeamsGames(red: "Subs");
        var report = await _service.Import(new HistoricalArchiveInput
        {
            // Blue One's sheet names only a placeholder: the schedule still puts him on Blue
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Blue One", team: "Free Agent"), Player("Red One"))).Seasons,
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString())], Schedule = schedule } },
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await _context.Teams.Select(t => t.TeamName).OrderBy(n => n).ToListAsync(), Is.EqualTo(expected7), "No 'Subs' or 'Free Agent' team");
            Assert.That(await Rosters(), Is.EqualTo(new Dictionary<string, string[]>
            {
                ["Blue"] = ["Blue One", "Blue Two"],
                ["Gold"] = ["Gold One", "Gold Two"],
            }));
        }
    }

    [Test]
    public async Task Import_AScheduleNamingAChampionsCaptain_PutsTheirPlayersOnTheTitlesTeam()
    {
        var (ids, schedule) = await ThreeTeamsGames(red: "Red One");
        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Gold One"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions = [new ArchiveChampionship { Season = "Winter League 2023 (DA)", Game = "Halo Infinite", Tier = "Open", Team = "Octane Kings", Captain = "Red One", Roster = ["Red One", "Red Two"] }],
            },
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString())], Schedule = schedule } },
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await _context.Teams.Select(t => t.TeamName).OrderBy(n => n).ToListAsync(), Is.EqualTo(expected8), "No empty 'Red One' team");
            Assert.That((await Rosters())["Octane Kings"], Is.EqualTo(expected9));
        }
    }

    [Test]
    public async Task Import_APlayerOfOneGameAlone_TakesTheNameTheOtherSideDoesNotHave()
    {
        var (ids, schedule) = await ThreeTeamsGames();
        var (rookieBlue, rookieGold, strangers, drifter) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await AddMatch(rookieBlue, (1, "Red One", Outcomes.Won, 5, 1), (10, "Blue Rookie", Outcomes.Lost, 1, 5));
        await AddMatch(rookieGold, (11, "Gold Rookie", Outcomes.Won, 5, 1), (3, "Blue One", Outcomes.Lost, 1, 5));
        await AddMatch(strangers, (12, "Drifter", Outcomes.Won, 5, 1), (13, "Stranger", Outcomes.Lost, 1, 5));
        await AddMatch(drifter, (12, "Drifter", Outcomes.Won, 5, 1), (14, "Other Stranger", Outcomes.Lost, 1, 5));
        schedule.AddRange(
        [
            new ScheduledGame { MatchId = rookieBlue.ToString(), Home = "Red", Away = "Blue" },
            new ScheduledGame { MatchId = rookieGold.ToString(), Home = "Gold", Away = "Blue" },
            new ScheduledGame { MatchId = strangers.ToString(), Home = "Red", Away = "Gold" },
            new ScheduledGame { MatchId = drifter.ToString(), Home = "Blue", Away = "Purple" },
        ]);

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Gold One"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions =
                [
                    new ArchiveChampionship { Season = "Winter League 2023 (DA)", Game = "Halo Infinite", Tier = "Open", Team = "Gold", Roster = ["Gold One"] },
                    new ArchiveChampionship { Season = "Winter League 2023 (DA)", Game = "Halo Infinite", Tier = "Pro", Team = " ", Captain = "Red", Roster = ["Red One"] },
                ],
            },
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString()), rookieBlue.ToString(), rookieGold.ToString(), strangers.ToString(), drifter.ToString()], Schedule = schedule } },
        });

        var rosters = await Rosters();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(rosters["Blue"], Does.Contain("Blue Rookie"), "Alone on his side, opposite Red: Blue");
            Assert.That(rosters["Gold"], Does.Contain("Gold Rookie"), "Alone on his side, opposite Blue: Gold");
            Assert.That(rosters.Values.SelectMany(r => r), Has.None.AnyOf("Drifter", "Stranger", "Other Stranger"), "No name in most of their games, and no side known");
            Assert.That(await _context.Teams.Select(t => t.TeamName).OrderBy(n => n).ToListAsync(), Is.EqualTo(expected10), "No team for a name no player is decided for");
            Assert.That(report.Warnings, Has.One.Contains("2 Waypoint match(es)"));
        }
    }

    [Test]
    public async Task Import_APhaseWithAMissingMatch_StillPutsItsSheetPlayersOnTheirScheduleTeams()
    {
        var (ids, schedule) = await ThreeTeamsGames();
        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Red One", kills: 3), Player("Blue Two", kills: 4))).Seasons,
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [.. ids.Select(i => i.ToString()), Guid.NewGuid().ToString()], Schedule = schedule } },
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.MissingMatches, Has.Count.EqualTo(1));
            Assert.That(await Rosters(), Is.EqualTo(new Dictionary<string, string[]> { ["Blue"] = ["Blue Two"], ["Red"] = ["Red One"] }),
                "The sheet's rows (kept: the phase isn't complete) are on their teams by gamertag");
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Team!.TeamName).OrderBy(n => n).ToListAsync(), Is.EqualTo(expected11));
        }
    }

    [Test]
    public async Task Import_WaypointGamesWithASideOfNoTeam_AreReportedOnceForTheSeason()
    {
        var (first, second) = (Guid.NewGuid(), Guid.NewGuid());
        await AddMatch(first, (1, "Hen", Outcomes.Won, 5, 1), (2, "Stranger", Outcomes.Lost, 1, 5));
        await AddMatch(second, (1, "Hen", Outcomes.Won, 5, 1), (3, "Other Stranger", Outcomes.Lost, 1, 5));

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hen", team: "Hens"))).Seasons,
            WaypointMatches = new() { [ScheduleSeason] = new WaypointSeasonMatches { Regular = [first.ToString(), second.ToString()] } },
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings.Where(w => w.Contains("none of the season's teams")), Has.Exactly(1).Contains("2 Waypoint match(es)").And.Contains(first.ToString()).And.Contains(second.ToString()));
            Assert.That(report.Warnings, Has.None.Contains($"Waypoint match {first}:"));
        }
    }

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

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
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

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        Assert.That((season.SeasonStart, season.SeasonEnd), Is.EqualTo((new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 12, 31, 23, 59, 59, DateTimeKind.Utc))),
            "A sheet rebuilt years later doesn't move the season");
    }

    [Test]
    public async Task AnUndatedSeason_WithoutAYearInItsName_IsItsSourcesYear()
    {
        var entry = new ArchiveSeason { SeasonName = "Respawn Fall Series 1", GameVersion = "halo4", League = "AGLA", SourceWorkbook = "respawn", SourceSheet = "Stats", SourceYear = 2021, Players = [Player("Galvann")] };

        await _service.Import(Input(entry));

        Assert.That((await _context.Seasons.AsNoTracking().SingleAsync()).SeasonStart, Is.EqualTo(new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)), "Not 2008");
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.SeasonStart, Is.EqualTo(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(ended, Is.InRange(before, DateTime.UtcNow));
            Assert.That((await _context.Seasons.AsNoTracking().SingleAsync()).SeasonEnd, Is.EqualTo(ended), "A re-run keeps it");
            Assert.That(await new GrifballWebApp.Application.Seasons.SeasonService(_context, new SeasonStatsService(_context)).GetCurrentSeasonID(), Is.Zero, "Not a current season");
        }
    }

    [Test]
    public async Task Import_ASeasonDatedAfterTheImport_BeganAndEndedByIt_InOrder()
    {
        // A sheet's timestamps after the import are its mistake: the season has been played, so it began and ended by now,
        // its dates in order (CK_Event_Seasons_DatesInOrder) rather than ending before it began
        var before = DateTime.UtcNow;
        var entry = new ArchiveSeason
        {
            SeasonName = "Respawn Future Series", GameVersion = "halo4", League = "AGLA", SourceWorkbook = "respawn-future", SourceSheet = "Stats",
            SourceYear = before.Year, FirstPlayedAt = before.AddDays(5), LastPlayedAt = before.AddDays(6), Players = [Player("Galvann")],
        };

        await _service.Import(Input(entry));

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.SeasonStart, Is.InRange(before, DateTime.UtcNow));
            Assert.That(season.SeasonEnd, Is.EqualTo(season.SeasonStart));
            Assert.That((season.SignupsOpen, season.SignupsClose, season.DraftStart, season.PublicAt), Is.EqualTo((season.SeasonStart, season.SeasonStart, season.SeasonStart, (DateTime?)season.SeasonStart)));
        }
    }

    [Test]
    public async Task Import_SkipsASeasonWhoseNameTheLeagueAlreadyHas()
    {
        _context.Seasons.Add(Season.Seed(seasonName: "Winter League 2019", leagueID: League.DefaultLeagueID));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.One.Contains("already has a season with that name"));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.Zero);
            Assert.That(await _context.Users.CountAsync(), Is.Zero, "The season's transaction rolled back");
        }
    }

    [Test]
    public async Task Import_AdoptsTheSeasonItMadeFromAnotherTabOfTheSameWorkbook_WhenItsConfigNowReadsADifferentOne()
    {
        var before = new ArchiveSeason
        {
            SeasonName = "Spring League 2024", GameVersion = "infinite", League = "AGLA", Division = "Open",
            SourceWorkbook = HistoricalKeys.Slug("Spring League 2024"), SourceSheet = "Aggregate", Players = [Player("Silva", kills: 5)],
        };
        await _service.Import(Input(before));
        var seasonId = (await _context.Seasons.AsNoTracking().SingleAsync()).SeasonID;

        var report = await _service.Import(Input(Entry("Spring League 2024", "infinite", "AGLA", false, Player("Silva", kills: 9))));

        _context.ChangeTracker.Clear();
        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Is.Empty);
            Assert.That((season.SeasonID, season.HistoricalSourceID), Is.EqualTo((seasonId, $"{HistoricalKeys.Slug("Spring League 2024")}/Stats")));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Kills).SingleAsync(), Is.EqualTo(9));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.Contains("second row"));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
        }
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
        var matches = await _context.SeasonMatches.AsNoTracking().Include(m => m.SeriesGames).Include(m => m.BracketMatch).Include(m => m.HomeTeam).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.WaypointMatchesPulled, Is.EqualTo(2));
            Assert.That(report.Skipped, Has.Some.Contains("not a match ID"));
            Assert.That(report.Skipped, Has.Some.Contains("agla/not-a-season"));
            Assert.That((report.SeasonMatchesCreated, report.GamesLinked), Is.EqualTo((3, 3)), "Games with no start time are a series each");
            Assert.That(matches.SelectMany(m => m.SeriesGames).Select(l => l.MatchID), Is.EquivalentTo(new[] { first, second, playoff }));
            Assert.That(matches.Select(m => m.HomeTeam!.TeamName), Is.All.EqualTo("Ram Ranch"), "The side of the sheet's team");
            Assert.That(matches.Count(m => m.BracketMatch != null), Is.EqualTo(1), "The playoff game is a bracket match");
            Assert.That((regular.FromMatches, regular.GamesPlayed, regular.Wins, regular.Losses, regular.Kills, regular.Deaths), Is.EqualTo((true, 2, 1, 1, 50, 30)));
            Assert.That(regular.TeamName, Is.EqualTo("Ram Ranch"), "From the sheet");
            Assert.That(regular.KdRatio, Is.EqualTo(1.67m));
            Assert.That(regular.WinPercent, Is.EqualTo(50m));
            Assert.That(regular.UserID, Is.EqualTo(hells.Id), "The placeholder is the Xbox account's, so the player claims it by setting their gamertag");
            Assert.That(lines.Single(s => s.Gamertag == "Hells Angel cx" && s.IsPlayoffs).FromMatches, Is.True);
            Assert.That(lines.Single(s => s.Gamertag == "Inf3ktion").FromMatches, Is.True);
            Assert.That(lines.Single(s => s.Gamertag == "Manual Only").FromMatches, Is.False, "No Waypoint matches: the sheet's totals");
            Assert.That(lines, Has.Count.EqualTo(4), "Never the games and the sheet for one player's season");
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(expected12), "No stored totals for players with games");
            Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(1));
        }

        var again = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
            Assert.That(await _context.SeasonMatches.CountAsync(), Is.EqualTo(3), "Linked games are kept as they are");
            Assert.That((again.SeasonMatchesCreated, again.StatsInserted, again.StatsUpdated), Is.EqualTo((0, 0, 1)));
        }
    }

    [Test]
    public async Task Import_WaypointGames_ReplaceSheetTotalsImportedEarlier_AndGoToTheLinkedAccount()
    {
        var input = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Hells Angel cx", kills: 1), Player("Hells Angel cx", kills: 2, stint: 2)));
        await _service.Import(input);
        var dummy = await _context.Users.SingleAsync();
        var linked = User.Seed(userName: "hells", xboxUser: XboxUser.FromXbox(100, "Hells Angel cx"));
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines.Select(s => (s.UserID, s.FromMatches, s.Kills)), Is.EquivalentTo(new[]
                {
            (linked.Id, true, 30),
            (dummy.Id, false, 1),
            (dummy.Id, false, 2),
        }), "The linked account has the games; the placeholder's sheet rows wait for a merge");
            Assert.That(report.Errors, Is.Empty);
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That((line.UserID, line.FromMatches, line.Kills), Is.EqualTo((user.Id, true, 30)));
            Assert.That(await _context.PlayerSeasonStats.AnyAsync(), Is.False);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Has.Some.Contains("could not be pulled"));
            Assert.That(report.MissingMatches, Has.One.Contains("regular season: 1 of 1 matches missing"));
        }
        await _dataPull.Received(3).GetAndSaveMatch(missing); // tried again with backoff
        Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1), "The sheet's totals stay");
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
        if (TestDatabase.Provider == DatabaseProvider.Sqlite)
            Assert.Ignore("SQLite doesn't enforce column lengths, so the long trade note doesn't fail the save (docs/sqlite.md).");
        var report = await _service.Import(Input(
            // A trade note longer than its column fails the season's save
            Entry("Winter League 2019", "halo3", "AGLA", false, new ArchivePlayer { Gamertag = "Silva", TradeNote = new string('x', 101) }),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Silva"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Has.One.StartsWith("winter-league-2019/Stats"));
            Assert.That(await _context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(expected13));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That((line.FromMatches, line.Kills), Is.EqualTo((true, 30)), "The sheet's totals never replace the games");
            Assert.That(again.ManualReplacedByWaypoint, Is.EqualTo(1));
            Assert.That(await _context.PlayerSeasonStats.AnyAsync(s => s.Gamertag == "Hells Angel cx"), Is.False);
        }
    }

    [TestCase("AGLA")]
    [TestCase("IGL")]
    public async Task ATitleTheRecordNamesOtherwise_GoesToTheSeasonTheCatalogSaysHoldsIt_AndItsTitlesOnlySeasonIsRetired(string league)
    {
        var champions = new ChampionsArchive { Champions = [new ArchiveChampionship { Season = "Summer League 2025", Game = "Halo Infinite", Tier = "Pro", Team = "Hells Angel cx", Roster = ["Silva"] }] };
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [] }, Championships = champions });
        var classicPro = new ArchiveSeason
        {
            SeasonName = "Summer League 2025 (Classic Pro)", GameVersion = "infinite", League = league, Division = "Pro", Titles = "Summer League 2025 (Pro)",
            SourceWorkbook = "classic-pro", SourceSheet = "Stats", Players = [Player("Silva", team: "Hells Angel cx")],
        };

        var report = await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [classicPro] }, Championships = champions });

        _context.ChangeTracker.Clear();
        var title = await _context.Set<PlayerChampionship>().Include(c => c.Season).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(title.Season.SeasonName, Is.EqualTo("Summer League 2025 (Classic Pro)"));
            Assert.That(await _context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(expected14));
            Assert.That(report.SeasonsRetired, Is.EqualTo(1), "The titles-only season the record made first");
        }
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
        _context.XboxUsers.Add(XboxUser.FromXbox(5, "Silva Now"));
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(5, "Silva", source: GamertagHistorySource.Manual));
        _context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 5, "Silva", "SILVA", user.Id, seasonId));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var stat = await _context.PlayerSeasonStats.Include(s => s.Season).ThenInclude(s => s.GameVersion).Include(s => s.GameVersion).Include(s => s.User).SingleAsync();
        var title = await _context.Set<PlayerChampionship>().Include(c => c.User).Include(c => c.Season).ThenInclude(s => s.League).Include(c => c.GameVersion).SingleAsync();
        var history = await _context.Set<XboxGamertagHistory>().Include(h => h.XboxUser).SingleAsync();
        var claim = await _context.GamertagClaims.Include(c => c.XboxUser).Include(c => c.PlaceholderUser).Include(c => c.Season).Include(c => c.Team).SingleAsync();
        var halo3 = await _context.GameVersions.Include(g => g.Seasons).SingleAsync(g => g.Slug == "halo3");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stat.Season.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(stat.GameVersion.Slug, Is.EqualTo("halo3"));
            Assert.That(title.PlayerChampionshipID, Is.GreaterThan(0));
            Assert.That((title.User.Id, title.Season.League.Slug, title.GameVersion.Slug), Is.EqualTo((user.Id, "agla", "halo3")));
            Assert.That((history.XboxGamertagHistoryID > 0, history.XboxUser.Gamertag, history.FirstSeen, history.LastSeen), Is.EqualTo((true, "Silva Now", (DateTime?)null, (DateTime?)null)));
            Assert.That((claim.GamertagClaimID > 0, claim.XboxUser!.Gamertag, claim.PlaceholderUser!.Id, claim.Season!.SeasonName, claim.Team, claim.Status), Is.EqualTo((true, "Silva Now", user.Id, "Winter League 2019", (Team?)null, GamertagClaimStatus.Pending)));
            Assert.That(halo3.Seasons.Select(s => s.SeasonName), Is.EqualTo(expected15));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.HistoricalSourceID, Is.EqualTo("2019-wl19-schedule-stats/Team and Player Stats"));
            Assert.That(season.HistoricalSourceUrl, Is.EqualTo("https://docs.google.com/spreadsheets/d/wl19"));
            Assert.That(report.Warnings, Is.Empty);
        }
    }

    [Test]
    public async Task Import_AfterTheSeasonsNameIsCorrected_FindsTheSameSeason()
    {
        await _service.Import(Input(With(Entry("Winter Leauge 2019", "halo3", "AGLA", false, Player("Silva", kills: 1)), "2019-wl19-schedule-stats", "Stats")));
        var id = await _context.Seasons.Select(s => s.SeasonID).SingleAsync();

        var again = await _service.Import(Input(With(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", kills: 2)), "2019-wl19-schedule-stats", "Stats")));

        var season = await _context.Seasons.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((season.SeasonID, season.SeasonName), Is.EqualTo((id, "Winter League 2019")));
            Assert.That((again.SeasonsCreated, again.SeasonsUpdated, again.StatsInserted), Is.EqualTo((0, 1, 0)));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Kills).SingleAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_AnEntryWithoutWorkbookAndSheet_IsKeyedByItsLeagueAndName_WithAWarning()
    {
        var report = await _service.Import(Input(With(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva")), null, null)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Seasons.Select(s => s.HistoricalSourceID).SingleAsync(), Is.EqualTo("agla/winter-league-2019"));
            Assert.That(report.Warnings, Has.One.Contains("no source workbook and sheet"));
        }
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
        var now = User.Seed(userName: "now", xboxUser: XboxUser.FromXbox(1, "Shadow S16"));
        _context.Users.Add(now);
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(1, "Rookie", firstSeen: Utc(2020), lastSeen: Utc(2021, 3), source: GamertagHistorySource.RenameDetected));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16"), Player("Rookie")),
            Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Shadow S16"))));

        var rows = await _context.PlayerSeasonStats.AsNoTracking().Include(s => s.Season).Include(s => s.User).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(rows.Single(r => r.Gamertag == "Rookie").UserID, Is.EqualTo(now.Id),
                "Its history still had it after 2019 began; FirstSeen (2020) is only when the app first saw it, not a lower bound");
            Assert.That(rows.Single(r => r.Gamertag == "Shadow S16" && r.Season.SeasonName == "Winter League 2019").User.UserName, Is.EqualTo("historical:Shadow S16"),
                "Someone else's 2019 gamertag: the account only took it in 2021");
        }
        // In 2023 the account had it
        Assert.That(rows.Single(r => r.Season.SeasonName == "Winter League 2023 (DA)").UserID, Is.EqualTo(now.Id));
    }

    [Test]
    public async Task Import_GivesARecentSeasonsGamertag_ToTheAccountThatHadItThen()
    {
        var now = User.Seed(userName: "now", xboxUser: XboxUser.FromXbox(1, "Shadow S16"));
        _context.Users.Add(now);
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(1, "Rookie", lastSeen: Utc(2021, 3), source: GamertagHistorySource.RenameDetected));
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Shadow S16"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(now.Id), "Renamed before the season");
    }

    [Test]
    public async Task Import_AGamertagTwoAccountsHistoriesClaim_IsLeftToItsPlaceholder_WithAWarning()
    {
        var a = User.Seed(userName: "a", xboxUser: XboxUser.FromXbox(1, "A Now"));
        var b = User.Seed(userName: "b", xboxUser: XboxUser.FromXbox(2, "B Now"));
        _context.Users.AddRange(a, b);
        _context.Set<XboxGamertagHistory>().AddRange(
            new XboxGamertagHistory(1, "Shared", source: GamertagHistorySource.Manual),
            new XboxGamertagHistory(2, "Shared", source: GamertagHistorySource.Manual));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.One.Contains("more than one Xbox account"));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.User.UserName).SingleAsync(), Is.EqualTo("historical:Shared"));
        }
    }

    [Test]
    public async Task Import_AnAccountThatLetGoOfTheGamertagBeforeTheSeason_DoesntCompete()
    {
        var a = User.Seed(userName: "a", xboxUser: XboxUser.FromXbox(1, "A Now"));
        var b = User.Seed(userName: "b", xboxUser: XboxUser.FromXbox(2, "B Now"));
        _context.Users.AddRange(a, b);
        _context.Set<XboxGamertagHistory>().AddRange(
            new XboxGamertagHistory(1, "Shared", firstSeen: Utc(2025), lastSeen: Utc(2026), source: GamertagHistorySource.Manual),
            new XboxGamertagHistory(2, "Shared", lastSeen: Utc(2015), source: GamertagHistorySource.Manual));
        await _context.SaveChangesAsync();

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(a.Id));
    }

    [Test]
    public async Task Import_ARenamedPlayersSheetRowInAWaypointSeason_IsNotCountedTwice()
    {
        var real = User.Seed(userName: "real", xboxUser: XboxUser.FromXbox(100, "New Tag"));
        _context.Users.Add(real);
        _context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(100, "Old Tag", firstSeen: Utc(2022), lastSeen: Utc(2024), source: GamertagHistorySource.RenameDetected));
        await _context.SaveChangesAsync();
        var match = Guid.NewGuid();
        await AddMatch(match, (100, "New Tag", Outcomes.Won, 30, 10));

        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2023 (DA)", "infinite", "AGLA", false, Player("Old Tag", kills: 1))).Seasons,
            WaypointMatches = new() { ["winter-league-2023-da/Stats"] = new WaypointSeasonMatches { Regular = [match.ToString()] } },
        });

        var line = (await Lines()).Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((line.UserID, line.FromMatches), Is.EqualTo((real.Id, true)));
            Assert.That(report.ManualReplacedByWaypoint, Is.EqualTo(1));
            Assert.That(report.Warnings, Is.Empty, "Every sheet row matched");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.Single(s => !s.IsPlayoffs).FromMatches, Is.True);
            Assert.That((stats.Single(s => s.IsPlayoffs).FromMatches, stats.Single(s => s.IsPlayoffs).Kills), Is.EqualTo((false, 2)), "A playoff match is missing: the playoffs are not linked");
            Assert.That(report.Skipped, Has.Some.Contains($"Waypoint match {lost}: not found"));
            Assert.That(report.MissingMatches, Has.One.Contains("playoffs: 1 of 2 matches missing").And.Contains(lost.ToString()));
            Assert.That(report.WaypointMatchesPulled, Is.Zero);
        }
    }

    [Test]
    public async Task Import_SuggestsRosterMatches_AndApprovingOneMergesThePlaceholder_WithoutCountingThePlayerTwice()
    {
        var teammate = User.Seed(userName: "teammate", xboxUser: XboxUser.FromXbox(1, "Teammate"));
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SuggestionsCreated, Is.EqualTo(1));
            Assert.That(report.Warnings, Has.One.Contains("Old B matches no Xbox account"));
            Assert.That((claim.Kind, claim.XboxUserID, claim.Gamertag, claim.Team!.TeamName, claim.Status), Is.EqualTo((GamertagClaimKind.RosterMatch, 2L, "Old B", "Ram Ranch", GamertagClaimStatus.Pending)));
        }
        var again = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SuggestionsCreated, await _context.GamertagClaims.CountAsync()), Is.EqualTo((0, 1)), "Not suggested twice");
            Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.Gamertag == "Old B"), Is.EqualTo(1), "Not merged automatically");
        }

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);
        _context.ChangeTracker.Clear();

        var newB = await _context.Users.SingleAsync(u => u.XboxUserID == 2);
        var history = await _context.Set<XboxGamertagHistory>().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((history.XboxUserID, history.Gamertag, history.Source, history.FirstSeen, history.LastSeen), Is.EqualTo((2L, "Old B", GamertagHistorySource.RosterMatch, (DateTime?)Utc(2023), (DateTime?)Utc(2024).AddSeconds(-1))));
            Assert.That((await Lines()).Where(s => s.UserID == newB.Id).Select(s => (s.FromMatches, s.Kills)), Is.EqualTo(new[] { (true, 20) }),
                "The placeholder's sheet row went with the merge: the games are the stats");
            Assert.That(await _context.Users.AnyAsync(u => u.UserName == "historical:Old B"), Is.False);
        }

        // And a re-run finds "Old B" through the history now
        var third = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(third.Warnings, Has.None.Contains("Old B"));
            Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.UserID == newB.Id), Is.Zero, "No stored totals for a player with games");
        }
    }

    [Test]
    public async Task Import_AGamertagTwoAccountsStillHadAfterTheSeason_IsLeftToItsPlaceholder()
    {
        // Reused: both accounts' histories can include 2019, whatever their FirstSeen
        var a = User.Seed(userName: "a", xboxUser: XboxUser.FromXbox(1, "A Now"));
        var b = User.Seed(userName: "b", xboxUser: XboxUser.FromXbox(2, "B Now"));
        _context.Users.AddRange(a, b);
        _context.Set<XboxGamertagHistory>().AddRange(
            new XboxGamertagHistory(1, "Shared", firstSeen: Utc(2022), lastSeen: Utc(2023), source: GamertagHistorySource.Manual),
            new XboxGamertagHistory(2, "Shared", firstSeen: Utc(2024), lastSeen: Utc(2025), source: GamertagHistorySource.Manual));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shared"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Warnings, Has.One.Contains("more than one Xbox account"));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.User.UserName).SingleAsync(), Is.EqualTo("historical:Shared"));
        }
    }

    [Test]
    public async Task Import_ComparesCurrentGamertagsNormalised()
    {
        var spaced = User.Seed(userName: "spaced", xboxUser: XboxUser.FromXbox(1, " Shadow   S16 "));
        var other = User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(2, "ShadowS16"));
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

        Assert.That(stages, Is.SupersetOf(strings));
        Assert.That(stages, Has.One.StartsWith("Pulling Waypoint matches: retrying in"));
    }

    /// <summary>A few seasons of the real compiled archive (Fixtures/HistoricalArchive: a rename-heavy Halo 3 season and
    /// its playoffs, a Waypoint season and its playoffs, a combined sheet, a Late League sheet (a matchmaking season), every championship) import,
    /// and a second run changes no row counts.</summary>
    [Test]
    public async Task Import_TheCompiledArchive_IsIdempotent()
    {
        var compiled = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "HistoricalArchive");
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.StatsInserted, Is.GreaterThan(150));
            Assert.That(report.ChampionshipsInserted, Is.GreaterThan(300));
            Assert.That(await Counts(), Is.EqualTo(before));
            Assert.That((again.SeasonsCreated, again.StatsInserted, again.ChampionshipsInserted, again.DummyUsersCreated), Is.EqualTo((0, 0, 0, 0)));
            Assert.That(again.Errors, Is.Empty);
            Assert.That(report.Warnings, Is.Empty, "Every entry names its workbook and sheet");
            Assert.That(await _context.Seasons.CountAsync(s => s.HistoricalSourceUrl == null), Is.Zero, "Every season links its workbook");
            Assert.That(await _context.MatchmakingSeasons.Select(s => s.Name).ToListAsync(), Is.EqualTo(expected16), "The Late League sheet is matchmaking");
            Assert.That(await _context.Seasons.AnyAsync(s => s.SeasonName.StartsWith("Late League")), Is.False);
        }
    }

    [Test]
    public async Task Import_GrifballInfoArchive_ImportsReachAndHalo4Seasons_AndTiesTitlesToTiers()
    {
        var fixtureDir = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "HistoricalArchive");
        HistoricalArchiveInput input;
        await using (var seasons = File.OpenRead(Path.Combine(fixtureDir, "grifball-info-sample.json")))
        await using (var champions = File.OpenRead(Path.Combine(fixtureDir, "agla-champions.json")))
        {
            input = new HistoricalArchiveInput
            {
                Seasons = await HistoricalArchiveInput.ReadAsync<UnifiedArchive>(seasons),
                Championships = await HistoricalArchiveInput.ReadAsync<ChampionsArchive>(champions),
            };
        }

        var report = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.StatsInserted, Is.GreaterThan(40));
        }

        var seasonsInDb = await _context.Seasons.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(seasonsInDb.Any(s => s.SeasonName == "Winter League 2011 (Pro)"), Is.True);
            Assert.That(seasonsInDb.Any(s => s.SeasonName == "Winter League 2011 (Amateur)"), Is.True);
            Assert.That(seasonsInDb.Any(s => s.SeasonName == "Fall League 2011"), Is.True);
            Assert.That(seasonsInDb.Any(s => s.SeasonName == "Winter League 2012"), Is.True);
            Assert.That(seasonsInDb.Any(s => s.SeasonName == "Winter League 2013"), Is.True);
        }

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).Include(c => c.Team).ToListAsync();
        // Cabs Are Here in Winter League 2011 Pro
        var cabsTitles = titles.Where(t => t.Team.TeamName == "Cabs Are Here").ToList();
        Assert.That(cabsTitles, Is.Not.Empty);
        Assert.That(cabsTitles.All(t => t.Season.SeasonName == "Winter League 2011 (Pro)"), Is.True);

        // Downstait in Winter League 2011 Amateur
        var downstaitTitles = titles.Where(t => t.Team.TeamName == "Downstait").ToList();
        Assert.That(downstaitTitles, Is.Not.Empty);
        Assert.That(downstaitTitles.All(t => t.Season.SeasonName == "Winter League 2011 (Amateur)"), Is.True);

        // Lake Greats in Winter League 2012
        var lakeTitles = titles.Where(t => t.Team.TeamName == "Lake Greats").ToList();
        Assert.That(lakeTitles, Is.Not.Empty);
        Assert.That(lakeTitles.All(t => t.Season.SeasonName == "Winter League 2012"), Is.True);

        // Idempotency: second run changes nothing
        var beforeCounts = await Counts();
        var again = await _service.Import(input);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.Errors, Is.Empty);
            Assert.That(await Counts(), Is.EqualTo(beforeCounts));
        }
    }

    [Test]
    public async Task Import_LeavesNoSeasonsRowsTracked_OnceEachSeasonIsSaved()
    {
        // Every season's rows staying tracked made an import of the whole archive grow past the backend's memory limit
        var report = await _service.Import(Input(
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", kills: 5, team: "Rooster"), Player("Taylor", kills: 7, team: "Owls")),
            Entry("Spring League 2019", "halo3", "AGLA", false, Player("Silva", kills: 9, team: "Rooster"))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await _context.PlayerSeasonStats.AsNoTracking().CountAsync(), Is.EqualTo(3), "Both seasons were saved");
            Assert.That(_context.ChangeTracker.Entries<PlayerSeasonStat>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<Team>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<TeamPlayer>(), Is.Empty);
            Assert.That(_context.ChangeTracker.Entries<Season>(), Is.Empty);
        }
    }

    [Test]
    public async Task Import_GrifballInfoWorkbook_ThroughItsCatalogEntry_ImportsItsSeasons_TiesTitlesToTiers_AndAgainChangesNothing()
    {
        // What the daily import does with the grifball.info backup: its catalog entry's config, a small workbook shaped like it
        var entry = HistoricalSourceCatalog.Sources.Single(s => s.HistoricalSourceID == "history-grifball-info-db");
        var build = HistoricalArchiveBuilder.Build([new HistoricalSourceContent(
            HistoricalTestData.Source(entry.HistoricalSourceID, entry.Url, entry.Config!.ToJson()), 1, HistoricalXlsx.GrifballInfo())]);
        var championsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "HistoricalArchive", "agla-champions.json");
        HistoricalArchiveInput input;
        await using (var champions = File.OpenRead(championsPath))
            input = new HistoricalArchiveInput { Seasons = build.Input.Seasons, Catalog = build.Input.Catalog, Championships = await HistoricalArchiveInput.ReadAsync<ChampionsArchive>(champions) };

        var report = await _service.Import(input);

        _context.ChangeTracker.Clear();
        var seasons = await _context.Seasons.AsNoTracking().Where(s => s.HistoricalSourceID!.StartsWith("history-grifball-info-db/")).ToListAsync();
        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.Season).Include(c => c.Team).ToListAsync();
        var crusader = await _context.PlayerSeasonStats.AsNoTracking().Where(s => s.Gamertag == "Crusader0102").OrderBy(s => s.StintIndex).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(seasons.Select(s => (s.SeasonName, s.HistoricalSourceID, s.HistoricalSourceUrl)).OrderBy(s => s.SeasonName), Is.EqualTo(new[]
            {
                ("Spring League 2011", "history-grifball-info-db/hr_pstats#s13", entry.Url),
                ("Winter League 2011 (Amateur)", "history-grifball-info-db/hr_pstats#s12_Amateur", entry.Url),
                ("Winter League 2011 (Pro)", "history-grifball-info-db/hr_pstats#s12_Pro", entry.Url),
                ("Winter League 2013", "history-grifball-info-db/h4_pstats#s20", entry.Url),
            }), "A season per season and division; the playoffs are the season's own");
            Assert.That(titles.Where(t => t.Team.TeamName == "Cabs Are Here").Select(t => (t.Season.SeasonName, t.Tier)).Distinct(), Is.EqualTo(new[] { ("Winter League 2011 (Pro)", "Pro") }));
            Assert.That(titles.Where(t => t.Team.TeamName == "Downstait").Select(t => (t.Season.SeasonName, t.Tier)).Distinct(), Is.EqualTo(new[] { ("Winter League 2011 (Amateur)", "Amateur") }));
            Assert.That(titles.Where(t => t.Season.SeasonName == "Spring League 2011").Select(t => (t.Team.TeamName, t.Tier)).Distinct().OrderBy(t => t.Tier),
                Is.EqualTo(new[] { ("Red Ray", "Amateur"), ("Way to Fail", "Pro") }), "The sheet has no Pro/Amateur split here: both tiers' titles are on the one season");
            Assert.That(titles.Where(t => t.Season.SeasonName == "Winter League 2013").Select(t => t.Team.TeamName).Distinct(), Is.EqualTo(expected17));
            Assert.That(crusader.Select(s => (s.StintIndex, s.GamesPlayed, s.Kills)), Is.EqualTo(new[] { (1, 2, 55), (2, 1, 28) }), "Halo 4's games added up, the trade a second stint");
        }

        var before = await Counts();
        var written = await StatsWritten();
        var again = await _service.Import(input);
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.Errors, Is.Empty);
            Assert.That((again.SeasonsCreated, again.StatsInserted, again.StatsRemoved, again.ChampionshipsInserted, again.DummyUsersCreated), Is.EqualTo((0, 0, 0, 0, 0)));
            Assert.That(await Counts(), Is.EqualTo(before));
            // StatsUpdated counts the rows matched; only a changed value is written, so no row is
            Assert.That(await StatsWritten(), Is.EqualTo(written), "No stat row written again");
        }

        async Task<List<(int, DateTime)>> StatsWritten() => (await _context.PlayerSeasonStats.AsNoTracking()
            .Select(s => new { s.PlayerSeasonStatID, s.ModifiedAt }).ToListAsync()).Select(s => (s.PlayerSeasonStatID, s.ModifiedAt)).OrderBy(s => s.PlayerSeasonStatID).ToList();
    }

    [Test]
    public async Task Import_AGamertagCorrectedInTheArchive_LeavesNoRowForTheOldOne()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silvaa", kills: 5, team: "Rooster"))));

        var report = await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", kills: 5, team: "Rooster"))));

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(expected18));
            Assert.That(await _context.Set<TeamPlayer>().Select(tp => tp.User.DisplayName).ToListAsync(), Is.EqualTo(new List<string> { "Silva" }));
            Assert.That(report.StatsRemoved, Is.EqualTo(1));
        }
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
        // A roster match suggested on the old team
        var silva = await _context.Users.SingleAsync(u => u.DisplayName == "Silva");
        var oldTeam = await _context.Teams.SingleAsync();
        _context.XboxUsers.Add(XboxUser.FromXbox(5, "Silva Now"));
        _context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 5, "Silva", "SILVA", silva.Id, oldTeam.SeasonID, oldTeam.TeamID));
        await _context.SaveChangesAsync();

        var report = await _service.Import(Archive("Rooster"));

        _context.ChangeTracker.Clear();
        var team = await _context.Teams.AsNoTracking().Include(t => t.TeamPlayers).SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((team.TeamName, team.RecordedWins, team.TeamPlayers.Count), Is.EqualTo(("Rooster", (int?)9, 1)), "The old team, its roster and record are gone");
            Assert.That(await _context.Set<PlayerChampionship>().Select(c => c.TeamID).ToListAsync(), Is.EqualTo(new[] { team.TeamID }));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.TeamID).ToListAsync(), Is.EqualTo(new int?[] { team.TeamID }));
            Assert.That(report.TeamsRemoved, Is.EqualTo(1));
            Assert.That(await _context.GamertagClaims.Select(c => c.TeamID).SingleAsync(), Is.Null, "The claim stays, without the team");
        }
    }

    [Test]
    public async Task Import_APlayerTakenOffTheSheet_LosesTheRowAndRosterSpot()
    {
        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"), Player("Gone", team: "Rooster"))));

        await _service.Import(Input(Entry("Winter League 2019", "halo3", "AGLA", false, Player("Silva", team: "Rooster"))));

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.Gamertag).ToListAsync(), Is.EqualTo(expected19));
            Assert.That(await _context.Set<TeamPlayer>().Select(tp => tp.User.DisplayName).ToListAsync(), Is.EqualTo(new List<string> { "Silva" }));
        }
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
        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Select(c => new { c.Tier, c.Team.TeamName, c.GamertagUsed }).ToListAsync();
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(season.GameVersion!.Slug, Is.EqualTo("halo3"));
            Assert.That(rows.Select(r => r.GameVersion.Slug), Is.EqualTo(expected20));
            Assert.That(report.Warnings, Has.One.Contains("more than one game"));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Set<SeriesGame>().Select(l => l.MatchID).ToListAsync(), Is.EqualTo(new[] { kept }));
            Assert.That(report.SeasonMatchesRemoved, Is.EqualTo(1));
            Assert.That((await Lines()).Single().Kills, Is.EqualTo(30));
        }
    }

    [Test]
    public async Task Import_PastGamertags_ResolvesToXboxUser_AndPreservesGamertagUsed()
    {
        var regresser = User.Seed(userName: "regresser", displayName: "Regresser", xboxUser: XboxUser.FromXbox(101, "Regresser"));
        var cxrnchip = User.Seed(userName: "cxrnchip", displayName: "cxrnchip", xboxUser: XboxUser.FromXbox(102, "cxrnchip"));
        _context.AddRange(regresser, cxrnchip);
        _context.Set<XboxGamertagHistory>().AddRange(
            new XboxGamertagHistory(101, "Oh My Godlxss", source: GamertagHistorySource.Manual, note: "known rename"),
            new XboxGamertagHistory(102, "DI Cell4225", source: GamertagHistorySource.Manual, note: "known rename"));
        await _context.SaveChangesAsync();

        var input = new HistoricalArchiveInput
        {
            Seasons = Input(
                Entry("Summer League 2014", "halo4", "AGLA", false, Player("Oh My Godlxss", team: "Next Dimenstion")),
                Entry("Summer League 2020", "reach", "AGLA", false, Player("DI Cell4225", team: "FTN"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions =
                [
                    new ArchiveChampionship { Season = "Summer League 2014", Game = "Halo 4", Tier = "Pro", Team = "Next Dimenstion", Roster = ["Oh My Godlxss"] },
                    new ArchiveChampionship { Season = "Summer League 2020", Game = "Halo Reach", Tier = "PC", Team = "FTN", Roster = ["DI Cell4225"] }
                ]
            }
        };

        var report = await _service.Import(input);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(report.DummyUsersCreated, Is.Zero, "Resolved through Xbox.PastGamertags");
        }

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.User).OrderBy(c => c.GamertagUsed).ToListAsync();
        Assert.That(titles, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(titles[0].GamertagUsed, Is.EqualTo("DI Cell4225"));
            Assert.That(titles[0].User.DisplayName, Is.EqualTo("cxrnchip"));
            Assert.That(titles[1].GamertagUsed, Is.EqualTo("Oh My Godlxss"));
            Assert.That(titles[1].User.DisplayName, Is.EqualTo("Regresser"));
        }
    }

    [Test]
    public async Task Import_RosterDuplicates_AreDeduplicatedPerUser()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = Input(
                Entry("Fall League 2014", "halo4", "AGLA", false,
                    Player("About73Puppies", team: "Aftershock")),
                Entry("Summer League 2010", "halo3", "AGLA", false,
                    Player("Veritas", team: "Relentless"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions =
                [
                    new ArchiveChampionship { Season = "Fall League 2014", Game = "Halo 4", Tier = "Amateur", Team = "Aftershock", Captain = "About73Puppies", Roster = ["About73Puppies", "ABOUT73PUPPIES"] },
                    new ArchiveChampionship { Season = "Summer League 2010", Game = "Halo 3", Tier = "Amateur", Team = "Relentless", Captain = "oDime", Roster = ["Veritas", "VERITAS"] }
                ]
            }
        };

        var report = await _service.Import(input);

        Assert.That(report.Errors, Is.Empty);
        var users = await _context.Users.AsNoTracking().ToListAsync();
        Assert.That(users.Select(u => u.DisplayName), Is.EquivalentTo(expected21));

        var teamPlayers = await _context.Set<TeamPlayer>().AsNoTracking().Include(tp => tp.User).Include(tp => tp.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(teamPlayers.Count(tp => tp.Team.TeamName == "Aftershock"), Is.EqualTo(1));
            Assert.That(teamPlayers.Count(tp => tp.Team.TeamName == "Relentless"), Is.EqualTo(1));
        }

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.User).Include(c => c.Team).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(titles.Count(c => c.Team.TeamName == "Aftershock"), Is.EqualTo(1));
            Assert.That(titles.Count(c => c.Team.TeamName == "Relentless"), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_WinterLeague2021_8thPlace_AwardsHalo3Championship()
    {
        var input = new HistoricalArchiveInput
        {
            Seasons = Input(Entry("Winter League 2021", "halo3", "AGLA", false, Player("Fox Fahrenheit", team: "8th Place"))).Seasons,
            Championships = new ChampionsArchive
            {
                Champions =
                [
                    new ArchiveChampionship
                    {
                        Season = "Winter League 2021",
                        Game = "MCC Halo 3",
                        Tier = "Amateur",
                        Team = "8th Place",
                        Captain = "Fox Fahrenheit",
                        Roster = ["Fox Fahrenheit", "Sammisaibot", "MuNgO PuNgO", "Chibifoxkit1", "VinnyHaw"]
                    }
                ]
            }
        };

        var report = await _service.Import(input);

        Assert.That(report.Errors, Is.Empty);
        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().Include(c => c.GameVersion).Include(c => c.Season).ToListAsync();
        Assert.That(titles, Has.Count.EqualTo(5));
        Assert.That(titles.Select(t => t.GameVersion?.Slug), Is.All.EqualTo("halo3"));
    }

    private Task<List<SeasonStatLine>> Lines() => new SeasonStatsService(_context).GetAsync();

    private async Task<(int Seasons, int Stats, int Titles, int Users)> Counts()
        => (await _context.Seasons.CountAsync(), await _context.PlayerSeasonStats.CountAsync(),
            await _context.Set<PlayerChampionship>().CountAsync(), await _context.Users.CountAsync());

    private async Task AddMatch(Guid matchId, params (long Xuid, string Gamertag, Outcomes Outcome, int Kills, int Deaths)[] players)
    {
        var match = Match.Seed(matchId, duration: TimeSpan.FromMinutes(10));
        var teams = new Dictionary<Outcomes, int>();
        foreach (var player in players)
        {
            var xbox = await _context.XboxUsers.FindAsync(player.Xuid) ?? _context.XboxUsers.Add(XboxUser.FromXbox(player.Xuid, player.Gamertag)).Entity;
            if (!teams.TryGetValue(player.Outcome, out var teamID))
            {
                teamID = teams.Count;
                teams.Add(player.Outcome, teamID);
                match.AddTeam(teamID, 0, player.Outcome);
            }
            match.AddParticipant(teamID, player.Xuid, new MatchParticipantStats
            {
                Kills = player.Kills,
                Deaths = player.Deaths,
                PersonalScore = player.Kills * 100,
                TimePlayed = TimeSpan.FromMinutes(10),
                AverageLife = TimeSpan.FromSeconds(12),
                DamageDealt = 1000,
                DamageTaken = 900,
                FirstJoinedTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            }, xboxUser: xbox);
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
