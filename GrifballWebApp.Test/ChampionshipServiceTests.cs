using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static GrifballWebApp.Server.Championships.ChampionshipService;

namespace GrifballWebApp.Test;

[TestFixture]
public class ChampionOfTests
{
    private static BracketResult M(Bracket bracket, int round, int? home, SeasonMatchResult? homeResult, int? away, SeasonMatchResult? awayResult)
        => new(bracket, round, home, homeResult, away, awayResult);

    private const SeasonMatchResult W = SeasonMatchResult.Won;
    private const SeasonMatchResult L = SeasonMatchResult.Loss;

    [Test]
    public void SingleElimination_TheLastRoundsWinner()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChampionOf([M(Bracket.Winner, 1, 1, W, 2, L), M(Bracket.Winner, 1, 3, L, 4, W), M(Bracket.Winner, 2, 1, L, 4, W)]), Is.EqualTo(4));
            Assert.That(ChampionOf([M(Bracket.Winner, 1, 1, W, 2, L), M(Bracket.Winner, 2, 1, null, 4, null)]), Is.Null, "The final is not played yet");
            Assert.That(ChampionOf([]), Is.Null);
        });
    }

    [Test]
    public void DoubleElimination_TheGrandFinal_OrItsSuddenDeath()
    {
        var winnerBracket = M(Bracket.Winner, 3, 1, W, 2, L);
        Assert.Multiple(() =>
        {
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, W, 3, L), M(Bracket.GrandFinalSuddenDeath, 5, null, null, null, null)]), Is.EqualTo(1),
                "The winner bracket's champion wins the grand final: no sudden death");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, L, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, 3, null, 1, null)]), Is.Null,
                "The sudden death is still to be played");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, L, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, 3, L, 1, W)]), Is.EqualTo(1));
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, null, SeasonMatchResult.Bye, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, null, null, null, null)]), Is.EqualTo(3),
                "A grand final decided by a BYE ends the bracket");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, null, 3, null)]), Is.Null);
        });
    }
}

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
    public async Task SetChampions_GivesTheTeamsRosterTheTitle_CaptainFlagged_AndIsIdempotent()
    {
        var season = await _context.AddSeason("Season 9");
        var captain = await _context.AddUser("cap", gamertag: "Cap");
        var player = await _context.AddUser("player", displayName: "No Xbox");
        var team = await _context.AddTeam(season, "Winners", captain, 1, player);

        var first = await _service.SetChampionsAsync(season.SeasonID, "Pro", team.TeamID, null, 6);
        var again = await _service.SetChampionsAsync(season.SeasonID, "Pro", team.TeamID, null, 6);

        var titles = await _context.PlayerChampionships.AsNoTracking().OrderBy(c => c.UserID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(new ChampionshipChanges(2, 0, 0)));
            Assert.That(again, Is.EqualTo(new ChampionshipChanges(0, 2, 0)));
            Assert.That(titles.Select(t => (t.UserID, t.TeamID, t.Tier, t.IsCaptain, t.GamertagUsed, t.GameVersionID, t.LeagueID)), Is.EqualTo(new[]
            {
                (captain.Id, team.TeamID, "Pro", true, "Cap", (int?)6, League.DefaultLeagueID),
                (player.Id, team.TeamID, "Pro", false, "No Xbox", (int?)6, League.DefaultLeagueID),
            }));
        });
    }

    [Test]
    public async Task SetChampions_AnotherTeam_MovesTheTitle_AndNoTeamTakesItAway_OtherTiersKept()
    {
        var season = await _context.AddSeason("Season 9");
        var a = await _context.AddTeam(season, "A", await _context.AddUser("a"), 1);
        var b = await _context.AddTeam(season, "B", await _context.AddUser("b"), 2);
        await _service.SetChampionsAsync(season.SeasonID, "Amateur", b.TeamID, null, null);

        await _service.SetChampionsAsync(season.SeasonID, "Pro", a.TeamID, null, null);
        var moved = await _service.SetChampionsAsync(season.SeasonID, "pro ", b.TeamID, null, null);
        Assert.That(moved, Is.EqualTo(new ChampionshipChanges(1, 0, 1)), "The tier is matched whatever its case and spacing");
        Assert.That(await _context.PlayerChampionships.Where(c => c.Tier == "Pro").Select(c => c.TeamID).ToListAsync(), Is.EqualTo(new[] { b.TeamID }));

        var removed = await _service.SetChampionsAsync(season.SeasonID, "Pro", null, null, null);
        Assert.That(removed, Is.EqualTo(new ChampionshipChanges(0, 0, 1)));
        Assert.That(await _context.PlayerChampionships.Select(c => c.Tier).ToListAsync(), Is.EqualTo(new[] { "Amateur" }));
    }

    [Test]
    public async Task SetChampions_TheGivenRoster_OncePerPlayer()
    {
        var season = await _context.AddSeason("Season 9");
        var team = await _context.AddTeam(season, "A", await _context.AddUser("cap"), 1);
        var other = await _context.AddUser("other");

        var changes = await _service.SetChampionsAsync(season.SeasonID, "Open", team.TeamID,
            [new ChampionMember(other.Id, "Other", true), new ChampionMember(other.Id, "Other again", false)], null);

        Assert.That(changes, Is.EqualTo(new ChampionshipChanges(1, 0, 0)));
        Assert.That(await _context.PlayerChampionships.Select(c => new { c.UserID, c.IsCaptain, c.GamertagUsed }).SingleAsync(),
            Is.EqualTo(new { UserID = other.Id, IsCaptain = true, GamertagUsed = "Other" }));
    }

    [Test]
    public async Task AwardFromBracket_DoesNothingWithoutABracket()
    {
        var season = await _context.AddSeason("Season 9");

        Assert.That(await _service.AwardFromBracketAsync(season.SeasonID), Is.Null);
        Assert.That(await _context.PlayerChampionships.AnyAsync(), Is.False);
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

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context));
        _service = new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService, new ChampionshipService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>A single-elimination bracket of one match: its final.</summary>
    private async Task<SeasonMatchTestData.Seeded> SeedFinal(string? division = null)
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        seeded.Season.Division = division;
        _context.Add(new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.Winner });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return seeded;
    }

    private async Task<List<(int TeamID, string Tier)>> Titles()
        => (await _context.PlayerChampionships.AsNoTracking().Select(c => new { c.TeamID, c.Tier }).ToListAsync())
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
        Assert.Multiple(() =>
        {
            Assert.That(titles, Has.Count.EqualTo(SeasonMatchTestData.HomeXbox.Length));
            Assert.That(titles, Is.All.EqualTo((seeded.Home.TeamID, ChampionshipService.DefaultTier)), "A season without a division has one open tier");
            Assert.That(again, Is.EqualTo(new ChampionshipChanges(0, SeasonMatchTestData.HomeXbox.Length, 0)));
        });
    }

    [Test]
    public async Task ARegularSeasonMatch_AwardsNothing()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        _context.ChangeTracker.Clear();

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await _context.PlayerChampionships.AnyAsync(), Is.False);
    }
}
