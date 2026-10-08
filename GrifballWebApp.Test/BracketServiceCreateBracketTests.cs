using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketServiceCreateBracketTests
{
    private GrifballContext _context;
    private BracketService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new BracketService(_context, new TeamStandingsService(_context));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<int> CreateSeason(string name = "Season")
    {
        var season = new Season { SeasonName = name };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season.SeasonID;
    }

    private async Task<List<MatchBracketInfo>> LoadBracket(int seasonID)
    {
        _context.ChangeTracker.Clear();
        return await _context.MatchBracketInfo
            .Include(x => x.SeasonMatch)
            .Where(x => x.SeasonMatch.SeasonID == seasonID)
            .AsNoTracking()
            .ToListAsync();
    }

    private static MatchBracketInfo ById(List<MatchBracketInfo> all, int? id) => all.Single(x => x.MatchBracketInfoID == id);

    [TestCase(2)]
    [TestCase(4)]
    [TestCase(8)]
    [TestCase(16)]
    public async Task CreateBracket_SingleElimination_BuildsFullWinnerTree(int participants)
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(participants, seasonID, doubleElimination: false, bestOf: 3);

        var bracket = await LoadBracket(seasonID);
        var rounds = (int)Math.Log2(participants);

        Assert.Multiple(() =>
        {
            Assert.That(bracket, Has.Count.EqualTo(participants - 1));
            Assert.That(bracket.All(x => x.Bracket == Bracket.Winner), Is.True);
            Assert.That(bracket.All(x => x.SeasonMatch.BestOf == 3), Is.True);
            Assert.That(bracket.All(x => x.SeasonMatch.HomeTeamID is null && x.SeasonMatch.AwayTeamID is null), Is.True);
            // Match numbers are 1..n-1 without gaps
            Assert.That(bracket.Select(x => x.MatchNumber).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, participants - 1)));
            Assert.That(bracket.Max(x => x.RoundNumber), Is.EqualTo(rounds));
        });

        // Round 1 holds every seed exactly once and pairs (s, size+1-s)
        var round1 = bracket.Where(x => x.RoundNumber == 1).OrderBy(x => x.MatchNumber).ToList();
        Assert.That(round1, Has.Count.EqualTo(participants / 2));
        var seeds = round1.SelectMany(x => new[] { x.HomeTeamSeedNumber!.Value, x.AwayTeamSeedNumber!.Value }).OrderBy(x => x);
        Assert.That(seeds, Is.EqualTo(Enumerable.Range(1, participants)));
        foreach (var m in round1)
        {
            Assert.That(m.HomeTeamSeedNumber + m.AwayTeamSeedNumber, Is.EqualTo(participants + 1));
            Assert.That(m.HomeTeamPreviousMatchBracketInfoID, Is.Null);
            Assert.That(m.AwayTeamPreviousMatchBracketInfoID, Is.Null);
        }
        // Seed 1 always in the first match
        Assert.That(round1[0].HomeTeamSeedNumber, Is.EqualTo(1));

        // Every later round match is fed by two distinct matches of the previous round
        foreach (var m in bracket.Where(x => x.RoundNumber > 1))
        {
            Assert.That(m.HomeTeamSeedNumber, Is.Null);
            Assert.That(m.AwayTeamSeedNumber, Is.Null);
            var home = ById(bracket, m.HomeTeamPreviousMatchBracketInfoID);
            var away = ById(bracket, m.AwayTeamPreviousMatchBracketInfoID);
            Assert.That(home.RoundNumber, Is.EqualTo(m.RoundNumber - 1));
            Assert.That(away.RoundNumber, Is.EqualTo(m.RoundNumber - 1));
            Assert.That(away.MatchNumber, Is.EqualTo(home.MatchNumber + 1));
        }
        // Each non-final winner match feeds exactly one later match
        var feeders = bracket.Where(x => x.RoundNumber > 1)
            .SelectMany(x => new[] { x.HomeTeamPreviousMatchBracketInfoID, x.AwayTeamPreviousMatchBracketInfoID })
            .ToList();
        Assert.That(feeders, Is.Unique);
        Assert.That(feeders, Has.Count.EqualTo(participants - 2));
    }

    [Test]
    public async Task CreateBracket_EightTeams_UsesStandardSeedOrder()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(8, seasonID, false, 1);

        var round1 = (await LoadBracket(seasonID)).Where(x => x.RoundNumber == 1).OrderBy(x => x.MatchNumber)
            .Select(x => (x.HomeTeamSeedNumber, x.AwayTeamSeedNumber)).ToList();
        Assert.That(round1, Is.EqualTo(new List<(int?, int?)> { (1, 8), (5, 4), (3, 6), (7, 2) }));
    }

    [Test]
    public async Task CreateBracket_DoubleElimination_FourTeams_BuildsExpectedGraph()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(4, seasonID, doubleElimination: true, bestOf: 5);

        var bracket = await LoadBracket(seasonID);
        MatchBracketInfo W(int n) => bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == n);
        MatchBracketInfo L(int n) => bracket.Single(x => x.Bracket == Bracket.Loser && x.MatchNumber == n);
        var gf = bracket.Single(x => x.Bracket == Bracket.GrandFinal);
        var sd = bracket.Single(x => x.Bracket == Bracket.GrandFinalSuddenDeath);

        Assert.Multiple(() =>
        {
            Assert.That(bracket, Has.Count.EqualTo(7));
            Assert.That(bracket.All(x => x.SeasonMatch.BestOf == 5), Is.True);
            Assert.That(bracket.Count(x => x.Bracket == Bracket.Winner), Is.EqualTo(3));
            Assert.That(bracket.Count(x => x.Bracket == Bracket.Loser), Is.EqualTo(2));

            Assert.That((W(1).HomeTeamSeedNumber, W(1).AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)4)));
            Assert.That((W(2).HomeTeamSeedNumber, W(2).AwayTeamSeedNumber), Is.EqualTo(((int?)3, (int?)2)));
            Assert.That(W(3).HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(1).MatchBracketInfoID));
            Assert.That(W(3).AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(W(2).MatchBracketInfoID));

            // Grand final continues winner numbering, round after the last winner round
            Assert.That(gf.MatchNumber, Is.EqualTo(4));
            Assert.That(gf.RoundNumber, Is.EqualTo(3));
            Assert.That(gf.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(3).MatchBracketInfoID));
            Assert.That(gf.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(L(2).MatchBracketInfoID));

            // Sudden death is fed by the grand final on both sides
            Assert.That(sd.MatchNumber, Is.EqualTo(5));
            Assert.That(sd.RoundNumber, Is.EqualTo(4));
            Assert.That(sd.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(gf.MatchBracketInfoID));
            Assert.That(sd.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(gf.MatchBracketInfoID));

            // Loser bracket numbering restarts at 1
            Assert.That(L(1).RoundNumber, Is.EqualTo(1));
            Assert.That(L(1).HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(1).MatchBracketInfoID));
            Assert.That(L(1).AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(W(2).MatchBracketInfoID));
            Assert.That(L(2).RoundNumber, Is.EqualTo(2));
            Assert.That(L(2).HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(3).MatchBracketInfoID));
            Assert.That(L(2).AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(L(1).MatchBracketInfoID));
        });
    }

    [Test]
    public async Task CreateBracket_DoubleElimination_EightTeams_BuildsLoserRounds()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(8, seasonID, true, 1);

        var bracket = await LoadBracket(seasonID);
        MatchBracketInfo W(int n) => bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == n);
        MatchBracketInfo L(int n) => bracket.Single(x => x.Bracket == Bracket.Loser && x.MatchNumber == n);
        (int?, int?) Prev(MatchBracketInfo m) => (m.HomeTeamPreviousMatchBracketInfoID, m.AwayTeamPreviousMatchBracketInfoID);
        (int?, int?) Ids(MatchBracketInfo a, MatchBracketInfo b) => (a.MatchBracketInfoID, b.MatchBracketInfoID);
        var gf = bracket.Single(x => x.Bracket == Bracket.GrandFinal);

        Assert.Multiple(() =>
        {
            Assert.That(bracket, Has.Count.EqualTo(15));
            Assert.That(bracket.Where(x => x.Bracket == Bracket.Loser).Select(x => x.RoundNumber).OrderBy(x => x),
                Is.EqualTo(new[] { 1, 1, 2, 2, 3, 4 }));
            Assert.That(Prev(L(1)), Is.EqualTo(Ids(W(1), W(2))));
            Assert.That(Prev(L(2)), Is.EqualTo(Ids(W(3), W(4))));
            // Major round: losers of winner round 2 vs survivors of loser round 1
            Assert.That(Prev(L(3)), Is.EqualTo(Ids(W(5), L(1))));
            Assert.That(Prev(L(4)), Is.EqualTo(Ids(W(6), L(2))));
            // Minor round: only loser bracket survivors
            Assert.That(Prev(L(5)), Is.EqualTo(Ids(L(3), L(4))));
            // Loser final gets the loser of the winner final
            Assert.That(Prev(L(6)), Is.EqualTo(Ids(W(7), L(5))));
            Assert.That(gf.MatchNumber, Is.EqualTo(8));
            Assert.That(gf.RoundNumber, Is.EqualTo(4));
            Assert.That(Prev(gf), Is.EqualTo(Ids(W(7), L(6))));
            Assert.That(bracket.Single(x => x.Bracket == Bracket.GrandFinalSuddenDeath).MatchNumber, Is.EqualTo(9));
        });
    }

    [Test]
    public async Task CreateBracket_DoubleElimination_SixteenTeams_HasExpectedCounts()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(16, seasonID, true, 1);

        var bracket = await LoadBracket(seasonID);
        var losers = bracket.Where(x => x.Bracket == Bracket.Loser).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(bracket.Count(x => x.Bracket == Bracket.Winner), Is.EqualTo(15));
            Assert.That(losers, Has.Count.EqualTo(14));
            Assert.That(losers.GroupBy(x => x.RoundNumber).OrderBy(x => x.Key).Select(x => x.Count()),
                Is.EqualTo(new[] { 4, 4, 2, 2, 1, 1 }));
            Assert.That(losers.Select(x => x.MatchNumber).OrderBy(x => x), Is.EqualTo(Enumerable.Range(1, 14)));
            // Every loser match is fed by two existing matches
            Assert.That(losers.All(x => x.HomeTeamPreviousMatchBracketInfoID is not null && x.AwayTeamPreviousMatchBracketInfoID is not null), Is.True);
            Assert.That(bracket.Single(x => x.Bracket == Bracket.GrandFinal).AwayTeamPreviousMatchBracketInfoID,
                Is.EqualTo(losers.Single(x => x.MatchNumber == 14).MatchBracketInfoID));
        });
    }

    [Test]
    public async Task CreateBracket_Recreate_ReplacesPreviousBracketOnlyForThatSeason()
    {
        var seasonID = await CreateSeason("A");
        var otherSeasonID = await CreateSeason("B");
        // Regular season match (no bracket info) must survive
        _context.SeasonMatches.Add(new SeasonMatch { SeasonID = seasonID, BestOf = 1 });
        await _context.SaveChangesAsync();
        await _service.CreateBracket(4, otherSeasonID, false, 1);
        await _service.CreateBracket(8, seasonID, true, 1);

        await _service.CreateBracket(4, seasonID, false, 3);

        _context.ChangeTracker.Clear();
        var bracket = await LoadBracket(seasonID);
        var regular = await _context.SeasonMatches.CountAsync(x => x.SeasonID == seasonID && x.BracketMatch == null);
        var other = await _context.SeasonMatches.CountAsync(x => x.SeasonID == otherSeasonID);
        var infos = await _context.MatchBracketInfo.CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(bracket, Has.Count.EqualTo(3));
            Assert.That(bracket.All(x => x.SeasonMatch.BestOf == 3), Is.True);
            Assert.That(regular, Is.EqualTo(1));
            Assert.That(other, Is.EqualTo(3));
            Assert.That(infos, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task CreateBracket_SingleParticipant_SingleElimination_CreatesNothing()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(1, seasonID, false, 1);

        Assert.That(await _context.SeasonMatches.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task CreateBracket_SingleParticipant_DoubleElimination_Throws()
    {
        var seasonID = await CreateSeason();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.CreateBracket(1, seasonID, true, 1));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the last winner bracket match"));
    }

    [Test]
    public async Task CreateBracket_TwoParticipants_DoubleElimination_ThrowsAndRollsBackDelete()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, false, 1);
        _context.ChangeTracker.Clear();

        // With only one winner round there is no loser bracket to build
        var ex = Assert.ThrowsAsync<Exception>(() => _service.CreateBracket(2, seasonID, true, 1));

        Assert.That(ex!.Message, Is.EqualTo("Failed to get last loser match"));
        _context.ChangeTracker.Clear();
        // The delete of the previous bracket happened inside the rolled back transaction
        Assert.That(await _context.SeasonMatches.CountAsync(x => x.SeasonID == seasonID), Is.EqualTo(3));
    }

    [TestCase(3)]
    [TestCase(6)]
    public async Task CreateBracket_NonPowerOfTwo_ByesViolateCheckConstraint(int participants)
    {
        var seasonID = await CreateSeason();

        // BUG: GetSeedMatchUps turns missing seeds into byes (null seed) but the match has no previous match either,
        // which violates CK_Event_MatchBracketInfo_Require{Home,Away}SeedOrPreviousMatch. Expected: a bracket with byes is
        // created; actual: SaveChanges throws and nothing is persisted.
        var ex = Assert.ThrowsAsync<DbUpdateException>(() => _service.CreateBracket(participants, seasonID, false, 1));

        Assert.That(ex!.InnerException?.Message, Does.Contain("CK_Event_MatchBracketInfo_Require"));
        _context.ChangeTracker.Clear();
        Assert.That(await _context.SeasonMatches.CountAsync(), Is.Zero);
    }
}
