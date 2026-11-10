using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ChampionshipServiceTests
{
    private GrifballContext _context;
    private ChampionshipService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new ChampionshipService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task RecordChampions_GivesTheTeamsRosterTheSeasonsTier_CaptainFlagged_AndIsIdempotent()
    {
        var season = await _context.AddSeason("Season 9");
        season.Edited(division: "Pro", gameVersionID: 6);
        await _context.SaveChangesAsync();
        var captain = await _context.AddUser("cap", gamertag: "Cap");
        var player = await _context.AddUser("player", displayName: "No Xbox");
        var team = await _context.AddTeam(season, "Winners", captain, 1, player);

        var first = await _service.RecordChampionsAsync(season.SeasonID, team.TeamID);
        var again = await _service.RecordChampionsAsync(season.SeasonID, team.TeamID);

        var titles = await _context.Set<PlayerChampionship>().AsNoTracking().OrderBy(c => c.UserID).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(new ChampionshipChanges(2, 0, 0)));
            Assert.That(again, Is.EqualTo(new ChampionshipChanges(0, 2, 0)));
            Assert.That(titles.Select(t => (t.UserID, t.TeamID, t.Tier, t.IsCaptain, t.GamertagUsed, t.GameVersionID)), Is.EqualTo(new[]
            {
                (captain.Id, team.TeamID, "Pro", true, "Cap", (int?)6),
                (player.Id, team.TeamID, "Pro", false, "No Xbox", (int?)6),
            }));
        }
    }

    [Test]
    public async Task RecordChampions_AnotherTeam_MovesTheTitle_AndNoTeamTakesItAway_OtherTiersKept()
    {
        var season = await _context.AddSeason("Season 9");
        var a = await _context.AddTeam(season, "A", await _context.AddUser("a"), 1);
        var b = await _context.AddTeam(season, "B", await _context.AddUser("b"), 2);
        await _context.SeedChampionsAsync(season.SeasonID, "Amateur", b.TeamID);
        season.Edited(division: "Pro");
        await _context.SaveChangesAsync();

        await _service.RecordChampionsAsync(season.SeasonID, a.TeamID);
        season.Edited(division: "pro ");
        await _context.SaveChangesAsync();
        var moved = await _service.RecordChampionsAsync(season.SeasonID, b.TeamID);
        Assert.That(moved, Is.EqualTo(new ChampionshipChanges(1, 0, 1)), "The tier is matched whatever its case and spacing");
        Assert.That(await _context.Set<PlayerChampionship>().Where(c => c.Tier == "Pro").Select(c => c.TeamID).ToListAsync(), Is.EqualTo(new[] { b.TeamID }));

        var removed = await _service.RecordChampionsAsync(season.SeasonID, null);
        Assert.That(removed, Is.EqualTo(new ChampionshipChanges(0, 0, 1)));
        Assert.That(await _context.Set<PlayerChampionship>().Select(c => c.Tier).ToListAsync(), Is.EqualTo(new List<string> { "Amateur" }));
    }

    [Test]
    public async Task RecordChampions_TheGivenRoster_OncePerPlayer()
    {
        var season = await _context.AddSeason("Season 9");
        var team = await _context.AddTeam(season, "A", await _context.AddUser("cap"), 1);
        var other = await _context.AddUser("other");

        var changes = season.RecordChampions(team, [new ChampionMember(other.Id, "Other", true), new ChampionMember(other.Id, "Other again", false)]);
        await _context.SaveChangesAsync();

        Assert.That(changes, Is.EqualTo(new ChampionshipChanges(1, 0, 0)));
        Assert.That(await _context.Set<PlayerChampionship>().Select(c => new { c.UserID, c.IsCaptain, c.GamertagUsed, c.Tier }).SingleAsync(),
            Is.EqualTo(new { UserID = other.Id, IsCaptain = true, GamertagUsed = "Other", Tier = ChampionshipService.DefaultTier }));
    }

    [Test]
    public async Task AwardFromBracket_WithoutABracket_TakesTheTitleBack_ButLeavesAnImportedSeasonsAlone()
    {
        var season = await _context.AddSeason("Season 9");
        var team = await _context.AddTeam(season, "A", await _context.AddUser("cap"), 1);
        await _service.RecordChampionsAsync(season.SeasonID, team.TeamID);

        Assert.That(await _service.AwardFromBracketAsync(season.SeasonID), Is.EqualTo(new ChampionshipChanges(0, 0, 1)), "No bracket (deleted): undecided");
        Assert.That(await _context.Set<PlayerChampionship>().AnyAsync(), Is.False);

        var imported = await _context.AddSeason("Summer League 2010");
        _context.MarkImported(imported, "wb/sml10");
        await _context.SaveChangesAsync();
        var champs = await _context.AddTeam(imported, "Champs", await _context.AddUser("old"), 1);
        await _context.SeedChampionsAsync(imported.SeasonID, "Pro", champs.TeamID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _service.AwardFromBracketAsync(imported.SeasonID), Is.Null, "Its titles are its record's");
            Assert.That(await _service.RecordChampionsAsync(imported.SeasonID, null), Is.Null, "Nor are they the app's to take away");
            Assert.That(await _context.Set<PlayerChampionship>().CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task RecordChampions_NeverStoresAPlaceholdersInternalName()
    {
        var season = await _context.AddSeason("Season 9");
        var placeholder = User.Seed(userName: "historical:Old Timer", isDummyUser: true);
        _context.Users.Add(placeholder);
        await _context.SaveChangesAsync();
        var team = await _context.AddTeam(season, "A", placeholder, 1);

        await _service.RecordChampionsAsync(season.SeasonID, team.TeamID);

        Assert.That(await _context.Set<PlayerChampionship>().Select(c => c.GamertagUsed).SingleAsync(), Is.EqualTo("Old Timer"));
    }
}

/// <summary>A season's title follows its playoff final's result as the app reports, forfeits and undoes it.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchChampionshipTests
{
    private GrifballContext _context;
    private SeasonMatchService _service;
    private BracketService _bracketService;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context), new GrifballWebApp.Application.Championships.ChampionshipService(_context));
        _bracketService = bracketService;
        _service = new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService, new ChampionshipService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>A single-elimination bracket of one match: its final.</summary>
    private async Task<SeasonMatchTestData.Seeded> SeedFinal(string? division = null)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        seeded.Season.WithDivision(division);
        _context.Add(MatchBracketInfo.Seed(seasonMatchID: seeded.SeasonMatch.SeasonMatchID, roundNumber: 1, matchNumber: 1, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2, bracket: Bracket.Winner));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return seeded;
    }

    private async Task<List<(int TeamID, string Tier)>> Titles()
        => (await _context.Set<PlayerChampionship>().AsNoTracking().Select(c => new { c.TeamID, c.Tier }).ToListAsync())
            .Select(c => (c.TeamID, c.Tier)).ToList();

    [Test]
    public async Task DecidingTheFinal_AwardsTheWinnersRoster_AndUndoingItTakesTheTitleBack()
    {
        var seeded = await SeedFinal(division: "Pro");

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        var titles = await Titles();
        Assert.That(titles, Has.Count.EqualTo(SeasonMatchTestData.AwayXbox.Length), "The away team won by the home team's forfeit");
        Assert.That(titles, Is.All.EqualTo((seeded.Away.TeamID, "Pro")), "In the season's division");

        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        Assert.That(await Titles(), Is.Empty);
    }

    [Test]
    public async Task ACorrectedResult_MovesTheTitle_WithoutDuplicates()
    {
        var seeded = await SeedFinal();

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        await _service.UndoResult(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        await _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        // Awarding again from the same result changes nothing
        var again = await new ChampionshipService(_context).AwardFromBracketAsync(seeded.Season.SeasonID);

        var titles = await Titles();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(titles, Has.Count.EqualTo(SeasonMatchTestData.HomeXbox.Length));
            Assert.That(titles, Is.All.EqualTo((seeded.Home.TeamID, ChampionshipService.DefaultTier)), "A season without a division has one open tier");
            Assert.That(again, Is.EqualTo(new ChampionshipChanges(0, SeasonMatchTestData.HomeXbox.Length, 0)));
        }
    }

    [Test]
    public async Task MakingTheBracketAgain_TakesTheTitleBack()
    {
        var seeded = await SeedFinal();
        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);
        _context.ChangeTracker.Clear();
        Assert.That(await Titles(), Is.Not.Empty);

        await _bracketService.CreateBracket(2, seeded.Season.SeasonID, false, 1);

        _context.ChangeTracker.Clear();
        Assert.That(await Titles(), Is.Empty, "The new bracket has decided nobody");
    }

    [Test]
    public async Task ARegularSeasonMatch_AwardsNothing()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await _context.Set<PlayerChampionship>().AnyAsync(), Is.False);
    }
}
