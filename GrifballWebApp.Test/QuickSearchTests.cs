using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Search;
using GrifballWebApp.Server.Historical;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QuickSearchTests
{
    private GrifballContext _context = null!;
    private SearchController _controller = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _controller = new SearchController(new QuickSearchService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private Task<QuickSearchDto> Search(string? q) => _controller.QuickSearch(q, CancellationToken.None);

    private async Task AddPastGamertag(User user, string gamertag)
    {
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory
        {
            XboxUserID = user.XboxUserID!.Value,
            Gamertag = gamertag,
            NormalizedGamertag = HistoricalKeys.Normalize(gamertag),
            Source = GamertagHistorySource.Manual,
        });
        await _context.SaveChangesAsync();
    }

    private async Task<Season> AddSeason(string name, DateTime start, DateTime end, DateTime? publicAt = null)
    {
        var season = new Season { SeasonName = name, SeasonStart = start, SeasonEnd = end, PublicAt = publicAt };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(" a ")]
    public async Task TooShortQuery_FindsNothing(string? q)
    {
        await _context.AddUser("a", gamertag: "a");
        await _context.AddSeason("a");

        var result = await Search(q);

        Assert.Multiple(() =>
        {
            Assert.That(result.Players, Is.Empty);
            Assert.That(result.Teams, Is.Empty);
            Assert.That(result.Seasons, Is.Empty);
        });
    }

    [Test]
    public async Task Players_ByGamertagDiscordOrDisplayName_IgnoringCase_PrefixFirst()
    {
        var sarge = await _context.AddUser("u1", gamertag: "Sarge");
        var bigSarge = await _context.AddUser("u2", gamertag: "BigSarge");
        var discord = await _context.AddUser("u3", discordName: "sargent_disc");
        var display = await _context.AddUser("u4", displayName: "Old Sarge");
        await _context.AddUser("u5", gamertag: "Grif");

        var result = await Search("SARGE");

        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.PastGamertag)), Is.EqualTo(new[]
        {
            (sarge.Id, "Sarge", (string?)null),
            (discord.Id, "sargent_disc", null),
            (bigSarge.Id, "BigSarge", null),
            (display.Id, "Old Sarge", null),
        }));
    }

    [Test]
    public async Task Players_OfOneName_SayWhichIsTheCurrentGamertag_AndWhichTheArchive_WithItsYears()
    {
        // The gamertag's holder today, and an archive player of the same name: maybe not the same person
        var holder = await _context.AddUser("u1", gamertag: "Hellmasker1");
        var archive = new User { UserName = "historical:Hellmasker1", NormalizedUserName = "HISTORICAL:HELLMASKER1", DisplayName = "Hellmasker1", IsDummyUser = true };
        var unplayed = new User { UserName = "historical:Hellmasker2", NormalizedUserName = "HISTORICAL:HELLMASKER2", DisplayName = "Hellmasker2", IsDummyUser = true };
        var discord = await _context.AddUser("u2", discordName: "hellmasker_disc");
        _context.Users.AddRange(archive, unplayed);
        await _context.SaveChangesAsync();
        var y2009 = await AddSeason("Winter League 2009", new DateTime(2009, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2009, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var y2011 = await AddSeason("Spring League 2011", new DateTime(2011, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2011, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        _context.PlayerSeasonStats.AddRange(
            new PlayerSeasonStat { UserID = archive.Id, SeasonID = y2009.SeasonID, GameVersionID = 1, Gamertag = "Hellmasker1" },
            new PlayerSeasonStat { UserID = archive.Id, SeasonID = y2011.SeasonID, GameVersionID = 1, Gamertag = "Hellmasker1" });
        await _context.SaveChangesAsync();

        var players = (await Search("Hellmasker")).Players.ToDictionary(p => p.UserID);

        Assert.Multiple(() =>
        {
            Assert.That((players[holder.Id].Kind, players[holder.Id].Years), Is.EqualTo(((QuickSearchPlayerKind?)QuickSearchPlayerKind.Xbox, (string?)null)));
            Assert.That((players[archive.Id].Kind, players[archive.Id].Years), Is.EqualTo(((QuickSearchPlayerKind?)QuickSearchPlayerKind.Archive, "2009–2011")));
            Assert.That((players[unplayed.Id].Kind, players[unplayed.Id].Years), Is.EqualTo(((QuickSearchPlayerKind?)QuickSearchPlayerKind.Archive, (string?)null)), "No seasons, no years");
            Assert.That(players[discord.Id].Kind, Is.Null, "A signed-up player known by a Discord name");
            Assert.That(QuickSearchService.YearSpan(2010, 2010), Is.EqualTo("2010"));
        });
    }

    [Test]
    public async Task Players_ByPastGamertag_ShowTheCurrentName_AndTheAliasMatched()
    {
        var simmons = await _context.AddUser("u1", gamertag: "Simmons");
        await AddPastGamertag(simmons, "KissAss");
        await AddPastGamertag(simmons, "KissAss2");
        var donut = await _context.AddUser("u2", gamertag: "Donut");
        await AddPastGamertag(donut, "PinkArmor");

        var result = await Search("kissass");

        // Two past gamertags of one player: listed once
        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.PastGamertag)), Is.EqualTo(new[]
        {
            (simmons.Id, "Simmons", (string?)"KissAss"),
        }));
    }

    [Test]
    public async Task Players_MatchedByNameAndPastGamertag_AreListedOnce_ByName()
    {
        var tucker = await _context.AddUser("u1", gamertag: "Tucker");
        await AddPastGamertag(tucker, "TuckerBowChicka");

        var result = await Search("tucker");

        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.PastGamertag)), Is.EqualTo(new[] { (tucker.Id, "Tucker", (string?)null) }));
    }

    [Test]
    public async Task Players_ByDiscordOrDisplayName_ShowTheNameMatched_AndRankByIt()
    {
        var zebra = await _context.AddUser("u1", gamertag: "Zebra", discordName: "griffo");
        var bigGrif = await _context.AddUser("u2", gamertag: "BigGrif");
        var display = await _context.AddUser("u3", gamertag: "Alpha", displayName: "Grifster");

        var result = await Search("grif");

        // Zebra's and Alpha's other names start with the query: they rank ahead of BigGrif, by the name matched
        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.PastGamertag, p.MatchedName)), Is.EqualTo(new[]
        {
            (zebra.Id, "Zebra", (string?)null, (string?)"griffo"),
            (display.Id, "Alpha", null, "Grifster"),
            (bigGrif.Id, "BigGrif", null, null),
        }));
    }

    [Test]
    public async Task Players_PastGamertagThatIsTheirCurrentOne_IsNoAlias()
    {
        // Their history holds their current gamertag, spaced differently (normalized, it is the same tag)
        await AddPastGamertag(await _context.AddUser("u1", gamertag: "Grif Ball"), "Grif  Ball");
        var renamed = await _context.AddUser("u2", gamertag: "Doc");
        await AddPastGamertag(renamed, "Grif  Ball Two");

        var result = await Search("grif  ball");

        Assert.That(result.Players.Select(p => (p.UserID, p.PastGamertag)), Is.EqualTo(new[] { (renamed.Id, (string?)"Grif  Ball Two") }));
    }

    [Test]
    public async Task Players_ByPastGamertag_AreNotCrowdedOut_ByNameHitsOrOneRenamersAliases()
    {
        // Name hits with many aliases that sort first, and one renamer with many aliases
        var named = new List<User>();
        for (var i = 0; i < QuickSearchService.Limit; i++)
        {
            var user = await _context.AddUser($"n{i}", gamertag: $"xCaboose{i}");
            named.Add(user);
            for (var j = 0; j < 3; j++)
                await AddPastGamertag(user, $"Caboose{i}{j}");
        }
        var renamer = await _context.AddUser("r", gamertag: "Renamer");
        for (var j = 0; j < QuickSearchService.Limit * 3; j++)
            await AddPastGamertag(renamer, $"Caboose_r{j:00}");
        var other = await _context.AddUser("o", gamertag: "Other");
        await AddPastGamertag(other, "CabooseOther");

        var result = await Search("caboose");

        // Past gamertags starting with the query outrank the names that only hold it
        Assert.That(result.Players.Select(p => (p.UserID, p.PastGamertag)), Is.EqualTo(
            new[] { (other.Id, (string?)"CabooseOther"), (renamer.Id, "Caboose_r00") }
                .Concat(named.Take(QuickSearchService.Limit - 2).Select(u => (u.Id, (string?)null)))));
    }

    [Test]
    public async Task Players_ShowTheFirstNameStartingWithTheQuery_ElseTheFirstHoldingIt()
    {
        var display = await _context.AddUser("u1", gamertag: "BigGrif", displayName: "Grifster");
        var discord = await _context.AddUser("u2", discordName: "BigGrif2", displayName: "OldGrif");
        var gamertag = await _context.AddUser("u3", gamertag: "MidGrif", discordName: "LateGrif");

        var result = await Search("grif");

        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.MatchedName)), Is.EqualTo(new[]
        {
            (display.Id, "BigGrif", (string?)"Grifster"),
            (discord.Id, "BigGrif2", null),
            (gamertag.Id, "MidGrif", null),
        }));
    }

    [Test]
    public async Task Players_TheFewTakenByName_AreTheFirstByTheNameMatched()
    {
        // More name hits than the limit; the one whose Discord name matched sorts first by it, though its gamertag is last
        var named = new List<User>();
        for (var i = 0; i < QuickSearchService.Limit; i++)
            named.Add(await _context.AddUser($"u{i}", gamertag: $"Grif{i}"));
        var zed = await _context.AddUser("z", gamertag: "Zed", discordName: "grif");

        var result = await Search("grif");

        Assert.That(result.Players.Select(p => (p.UserID, p.Name, p.MatchedName)), Is.EqualTo(
            new[] { (zed.Id, "Zed", (string?)"grif") }
                .Concat(named.Take(QuickSearchService.Limit - 1).Select(u => (u.Id, u.XboxUser!.Gamertag, (string?)null)))));
    }

    [Test]
    public async Task Players_MatchedByName_AreNeverListedByAPastGamertag_EvenBeyondTheLimit()
    {
        // More name hits than the limit; the last by name also has a past gamertag that would sort first
        for (var i = 0; i < QuickSearchService.Limit; i++)
            await _context.AddUser($"u{i}", gamertag: $"GrifZ{i}");
        var ninth = await _context.AddUser("n", gamertag: $"GrifZ{QuickSearchService.Limit}");
        await AddPastGamertag(ninth, "GrifAlpha");

        var result = await Search("grif");

        Assert.Multiple(() =>
        {
            Assert.That(result.Players.Select(p => p.Name), Is.EqualTo(Enumerable.Range(0, QuickSearchService.Limit).Select(i => $"GrifZ{i}")));
            Assert.That(result.Players.Select(p => p.PastGamertag), Is.All.Null);
        });
    }

    [Test]
    public async Task Players_PastGamertagThatIsTheirCurrentOne_IsNoAlias_HoweverTheCurrentOneIsSpaced()
    {
        // The current gamertag is stored with doubled and outer spaces; normalized, its history row is the same tag
        var spaced = await _context.AddUser("u1", gamertag: " Grif  Ball ");
        await AddPastGamertag(spaced, "Grif Ball");
        var renamed = await _context.AddUser("u2", gamertag: "Doc");
        await AddPastGamertag(renamed, "Elf Ballad");

        var result = await Search("f ball");

        Assert.That(result.Players.Select(p => (p.UserID, p.PastGamertag)), Is.EqualTo(new[] { (renamed.Id, (string?)"Elf Ballad") }));
    }

    [Test]
    public async Task Players_AreNeverFoundOrNamedByTheirLoginName()
    {
        await _context.AddUser("grifLogin");
        var named = await _context.AddUser("grifLogin2", displayName: "Shown");

        Assert.Multiple(async () =>
        {
            Assert.That((await Search("grifLogin")).Players, Is.Empty);
            Assert.That((await Search("shown")).Players.Select(p => (p.UserID, p.Name)), Is.EqualTo(new[] { (named.Id, "Shown") }));
        });
    }

    [Test]
    public async Task Players_AreCappedAtTheLimit()
    {
        for (var i = 0; i < QuickSearchService.Limit + 2; i++)
            await _context.AddUser($"u{i}", gamertag: $"Church{i:00}");

        var result = await Search("church");

        Assert.That(result.Players.Select(p => p.Name), Is.EqualTo(Enumerable.Range(0, QuickSearchService.Limit).Select(i => $"Church{i:00}")));
    }

    [Test]
    public async Task Teams_ByName_OfPublicSeasons_LatestSeasonFirst_WithTheirSeason()
    {
        var captains = 0;
        async Task<User> Captain() => await _context.AddUser($"cap{++captains}");
        var old = await AddSeason("Season 1", new DateTime(2020, 1, 1), new DateTime(2020, 3, 1));
        var recent = await AddSeason("Season 2", new DateTime(2024, 1, 1), new DateTime(2024, 3, 1));
        var hidden = await AddSeason("Season 3", DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(60));
        var announced = await AddSeason("Season 4", DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(60), publicAt: DateTime.UtcNow.AddDays(-1));
        var oldBlue = await _context.AddTeam(old, "Blue Team", await Captain(), 1);
        var recentBlue = await _context.AddTeam(recent, "Blue Team", await Captain(), 1);
        var navy = await _context.AddTeam(recent, "Navy Blue", await Captain(), 2);
        await _context.AddTeam(hidden, "Blue Team", await Captain(), 1);
        var announcedBlue = await _context.AddTeam(announced, "Blue Team", await Captain(), 1);
        await _context.AddTeam(recent, "Red Team", await Captain(), 3);

        var result = await Search("blue");

        Assert.That(result.Teams.Select(t => (t.TeamID, t.TeamName, t.SeasonID, t.SeasonName)), Is.EqualTo(new[]
        {
            (announcedBlue.TeamID, "Blue Team", announced.SeasonID, "Season 4"),
            (recentBlue.TeamID, "Blue Team", recent.SeasonID, "Season 2"),
            (oldBlue.TeamID, "Blue Team", old.SeasonID, "Season 1"),
            (navy.TeamID, "Navy Blue", recent.SeasonID, "Season 2"),
        }));
    }

    [Test]
    public async Task Seasons_ByName_PrefixFirst_ThenLatest_HidingUnannouncedOnes()
    {
        var summer19 = await AddSeason("Summer League 2019", new DateTime(2019, 6, 1), new DateTime(2019, 8, 1));
        var summer21 = await AddSeason("Summer League 2021", new DateTime(2021, 6, 1), new DateTime(2021, 8, 1));
        var proSummer = await AddSeason("Pro Summer Cup", new DateTime(2022, 6, 1), new DateTime(2022, 8, 1));
        await AddSeason("Summer League Next", DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(60));
        await AddSeason("Winter League", new DateTime(2022, 1, 1), new DateTime(2022, 3, 1));

        var result = await Search("summer");

        Assert.That(result.Seasons.Select(s => s.SeasonID), Is.EqualTo(new[] { summer21.SeasonID, summer19.SeasonID, proSummer.SeasonID }));
        Assert.That(result.Seasons[0].SeasonName, Is.EqualTo("Summer League 2021"));
        Assert.That(result.Seasons[0].Start, Is.EqualTo(new DateTime(2021, 6, 1)));
        Assert.That(result.Seasons[0].End, Is.EqualTo(new DateTime(2021, 8, 1)));
    }

    [Test]
    public async Task Seasons_ByYear_MatchTheirNameOrTheYearTheyRanIn()
    {
        var named = await AddSeason("League 2019", new DateTime(2020, 1, 1), new DateTime(2020, 2, 1));
        var spanning = await AddSeason("Winter League", new DateTime(2018, 12, 1), new DateTime(2019, 2, 1));
        var inYear = await AddSeason("Spring League", new DateTime(2019, 3, 1), new DateTime(2019, 5, 1));
        await AddSeason("Other League", new DateTime(2020, 3, 1), new DateTime(2020, 5, 1));

        var result = await Search("2019");

        Assert.That(result.Seasons.Select(s => s.SeasonID), Is.EquivalentTo(new[] { named.SeasonID, spanning.SeasonID, inYear.SeasonID }));
        // The latest first
        Assert.That(result.Seasons[0].SeasonID, Is.EqualTo(named.SeasonID));
    }

    [Test]
    public async Task LongQuery_IsCutToTheMaxLength()
    {
        var user = await _context.AddUser("u1", gamertag: "Sarge");

        // Past the cut there is only "zzz", which no one has: what is left is "Sarge"
        var result = await Search("Sarge" + new string(' ', QuickSearchService.MaxLength) + "zzz");

        Assert.That(result.Players.Select(p => p.UserID), Is.EqualTo(new[] { user.Id }));
    }

    [TestCase("2019", true, 2019)]
    [TestCase("1999", false, 0)]
    [TestCase("3000", false, 0)]
    [TestCase("201", false, 0)]
    [TestCase("20a9", false, 0)]
    [TestCase("+201", false, 0)]
    public void TryParseYear_TakesFourDigitYears(string q, bool expected, int expectedYear)
    {
        Assert.That(QuickSearchService.TryParseYear(q, out var year), Is.EqualTo(expected));
        if (expected)
            Assert.That(year, Is.EqualTo(expectedYear));
    }
}
