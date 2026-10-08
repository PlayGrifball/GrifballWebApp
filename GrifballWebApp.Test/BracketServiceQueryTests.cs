using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// Covers GetBracketsAsync, GetViewerDataAsync, SetSeeds (standings based) and DetermineNextMatches on brackets
/// produced by CreateBracket.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketServiceQueryTests
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

    private async Task<int> CreateSeason(string name = "Season X")
    {
        var season = new Season { SeasonName = name };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season.SeasonID;
    }

    private async Task<List<MatchBracketInfo>> LoadTrackedBracket(int seasonID)
    {
        _context.ChangeTracker.Clear();
        // Loading every bracket info of the season lets EF fix up the inverse navigation collections
        return await _context.MatchBracketInfo
            .Include(x => x.SeasonMatch)
            .Where(x => x.SeasonMatch.SeasonID == seasonID)
            .ToListAsync();
    }

    /// <summary>
    /// Four teams with a strict standings order: C (3-0), A (2-1), D (1-2), B (0-3).
    /// </summary>
    private async Task<(Team A, Team B, Team C, Team D)> CreateTeamsWithStandings(int seasonID)
    {
        var a = new Team { SeasonID = seasonID, TeamName = "Alpha" };
        var b = new Team { SeasonID = seasonID, TeamName = "Bravo" };
        var c = new Team { SeasonID = seasonID, TeamName = "Charlie" };
        var d = new Team { SeasonID = seasonID, TeamName = "Delta" };
        _context.Teams.AddRange(a, b, c, d);
        await _context.SaveChangesAsync();

        SeasonMatch Win(Team winner, Team loser) => new()
        {
            SeasonID = seasonID,
            BestOf = 1,
            HomeTeamID = winner.TeamID,
            HomeTeamResult = SeasonMatchResult.Won,
            AwayTeamID = loser.TeamID,
            AwayTeamResult = SeasonMatchResult.Loss,
        };
        _context.SeasonMatches.AddRange(Win(c, a), Win(c, d), Win(c, b), Win(a, d), Win(a, b), Win(d, b));
        await _context.SaveChangesAsync();
        return (a, b, c, d);
    }

    // ---------------- GetBracketsAsync ----------------

    [Test]
    public async Task GetBracketsAsync_DoubleElimination_ProducesHumanReadableLabels()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);
        _context.ChangeTracker.Clear();

        var dto = await _service.GetBracketsAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(dto.WinnerRounds.Select(r => r.RoundNumber), Is.EqualTo(new[] { 1, 2 }));
            var r1 = dto.WinnerRounds[0].Matches;
            Assert.That(r1.Select(m => (m.MatchNumber, m.HomeTeam, m.AwayTeam)), Is.EqualTo(new[]
            {
                ("W1", "Seed 1", "Seed 4"),
                ("W2", "Seed 3", "Seed 2"),
            }));
            var r2 = dto.WinnerRounds[1].Matches.Single();
            Assert.That((r2.MatchNumber, r2.HomeTeam, r2.AwayTeam), Is.EqualTo(("W3", "Winner of W1", "Winner of W2")));

            Assert.That(dto.LoserRounds.Select(r => r.RoundNumber), Is.EqualTo(new[] { 1, 2 }));
            var l1 = dto.LoserRounds[0].Matches.Single();
            Assert.That((l1.MatchNumber, l1.HomeTeam, l1.AwayTeam), Is.EqualTo(("L1", "Loser of W1", "Loser of W2")));
            var l2 = dto.LoserRounds[1].Matches.Single();
            Assert.That((l2.MatchNumber, l2.HomeTeam, l2.AwayTeam), Is.EqualTo(("L2", "Loser of W3", "Winner of L1")));

            Assert.That((dto.GrandFinal.MatchNumber, dto.GrandFinal.HomeTeam, dto.GrandFinal.AwayTeam),
                Is.EqualTo(("W4", "Winner of W3", "Winner of L2")));
            Assert.That((dto.GrandFinalSuddenDeath.MatchNumber, dto.GrandFinalSuddenDeath.HomeTeam, dto.GrandFinalSuddenDeath.AwayTeam),
                Is.EqualTo(("W5", "-", "-")));
        });
    }

    [Test]
    public async Task GetBracketsAsync_SingleElimination_HasNoLoserBracketOrGrandFinal()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(8, seasonID, false, 1);
        _context.ChangeTracker.Clear();

        var dto = await _service.GetBracketsAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(dto.WinnerRounds.Select(r => r.Matches.Length), Is.EqualTo(new[] { 4, 2, 1 }));
            Assert.That(dto.WinnerRounds[2].Matches[0].HomeTeam, Is.EqualTo("Winner of W5"));
            Assert.That(dto.WinnerRounds[2].Matches[0].AwayTeam, Is.EqualTo("Winner of W6"));
            Assert.That(dto.LoserRounds, Is.Empty);
            Assert.That(dto.GrandFinal, Is.Null);
            Assert.That(dto.GrandFinalSuddenDeath, Is.Null);
        });
    }

    [Test]
    public async Task GetBracketsAsync_NoBracket_ReturnsEmpty()
    {
        var seasonID = await CreateSeason();

        var dto = await _service.GetBracketsAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(dto.WinnerRounds, Is.Empty);
            Assert.That(dto.LoserRounds, Is.Empty);
            Assert.That(dto.GrandFinal, Is.Null);
            Assert.That(dto.GrandFinalSuddenDeath, Is.Null);
        });
    }

    // ---------------- GetViewerDataAsync ----------------

    [Test]
    public void GetViewerDataAsync_SeasonMissing_Throws()
    {
        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetViewerDataAsync(12345));
        Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
    }

    [Test]
    public async Task GetViewerDataAsync_UnseededDoubleElimination_MapsStructure()
    {
        var seasonID = await CreateSeason("Season Seven");
        await _service.CreateBracket(4, seasonID, true, 1);
        var bracket = await LoadTrackedBracket(seasonID);
        int Sm(Bracket kind, int number) => bracket.Single(x => x.Bracket == kind && x.MatchNumber == number).SeasonMatchID;
        _context.ChangeTracker.Clear();

        var data = await _service.GetViewerDataAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(data.MatchGames, Is.Empty);
            Assert.That(data.Participants.Select(p => (p.Id, p.Name, p.Tournament_id)), Is.EqualTo(new[]
            {
                (1, "Seed 1", seasonID), (4, "Seed 4", seasonID), (3, "Seed 3", seasonID), (2, "Seed 2", seasonID),
            }));

            var stage = data.Stages.Single();
            Assert.That(stage.Id, Is.EqualTo(seasonID));
            Assert.That(stage.Tournament_id, Is.EqualTo(seasonID));
            Assert.That(stage.Name, Is.EqualTo("Season Seven"));
            Assert.That(stage.Type, Is.EqualTo(StageType.double_elimination));
            Assert.That(stage.Number, Is.EqualTo(1));
            Assert.That(stage.Settings.Size, Is.EqualTo(4));
            Assert.That(stage.Settings.GrandFinal, Is.EqualTo(GrandFinalType.@double));
            Assert.That(stage.Settings.SeedOrdering[0], Is.EqualTo(SeedOrdering.inner_outer));
            Assert.That(stage.Settings.BalanceByes, Is.False);
            Assert.That(stage.Settings.ManualOrdering, Is.Empty);

            Assert.That(data.Matches, Has.Count.EqualTo(7));
            var byId = data.Matches.ToDictionary(m => m.Id);

            var w1 = byId[Sm(Bracket.Winner, 1)];
            Assert.That((w1.Group_id, w1.Round_id, w1.Number, w1.Status, w1.Stage_id), Is.EqualTo((1, 1, 1, Status.Ready, seasonID)));
            Assert.That(w1.Opponent1!.Position, Is.EqualTo(1));
            Assert.That(w1.Opponent2!.Position, Is.EqualTo(4));
            Assert.That(w1.Opponent1.Id, Is.Null);
            Assert.That(w1.Opponent1.Result, Is.Null);
            Assert.That(w1.Opponent1.Forfeit, Is.False);

            var w2 = byId[Sm(Bracket.Winner, 2)];
            Assert.That((w2.Round_id, w2.Number), Is.EqualTo((1, 2)));

            // Round 2 match number is relative to the round
            var w3 = byId[Sm(Bracket.Winner, 3)];
            Assert.That((w3.Group_id, w3.Round_id, w3.Number, w3.Status), Is.EqualTo((1, 2, 1, Status.Locked)));
            Assert.That(w3.Opponent1!.Position, Is.Null);

            // Loser rounds continue after the last winner round (2)
            var l1 = byId[Sm(Bracket.Loser, 1)];
            Assert.That((l1.Group_id, l1.Round_id, l1.Number, l1.Status), Is.EqualTo((2, 3, 1, Status.Locked)));
            Assert.That(l1.Opponent1!.Position, Is.EqualTo(1), "position = number of the winner match the loser drops from");
            Assert.That(l1.Opponent2!.Position, Is.EqualTo(2));

            var l2 = byId[Sm(Bracket.Loser, 2)];
            Assert.That((l2.Group_id, l2.Round_id, l2.Number), Is.EqualTo((2, 4, 1)));
            Assert.That(l2.Opponent1!.Position, Is.EqualTo(1), "loser of W3, the first match of winner round 2");
            Assert.That(l2.Opponent2!.Position, Is.Null, "fed by a loser match, which is not a winner match");

            var gf = byId[Sm(Bracket.GrandFinal, 4)];
            Assert.That((gf.Group_id, gf.Round_id, gf.Number, gf.Status), Is.EqualTo((3, 0, 1, Status.Locked)));
            Assert.That(gf.Opponent2!.Position, Is.EqualTo(1));
            Assert.That(gf.Opponent1!.Position, Is.Null);

            var sd = byId[Sm(Bracket.GrandFinalSuddenDeath, 5)];
            Assert.That((sd.Group_id, sd.Round_id, sd.Number, sd.Status), Is.EqualTo((3, 0, 2, Status.Ready)));
        });
    }

    [Test]
    public async Task GetViewerDataAsync_SingleElimination_HasNoGrandFinal()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, false, 1);
        _context.ChangeTracker.Clear();

        var data = await _service.GetViewerDataAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(data.Stages[0].Type, Is.EqualTo(StageType.single_elimination));
            Assert.That(data.Stages[0].Settings.GrandFinal, Is.EqualTo(GrandFinalType.none));
            Assert.That(data.Matches, Has.Count.EqualTo(3));
            Assert.That(data.Matches.All(m => m.Group_id == 1), Is.True);
        });
    }

    [Test]
    public async Task GetViewerDataAsync_GrandFinalWithoutSuddenDeath_IsSimple()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);
        var sd = await _context.SeasonMatches.SingleAsync(x => x.BracketMatch!.Bracket == Bracket.GrandFinalSuddenDeath);
        _context.SeasonMatches.Remove(sd);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var data = await _service.GetViewerDataAsync(seasonID);

        Assert.Multiple(() =>
        {
            Assert.That(data.Stages[0].Settings.GrandFinal, Is.EqualTo(GrandFinalType.simple));
            Assert.That(data.Matches, Has.Count.EqualTo(6));
            Assert.That(data.Matches.Count(m => m.Group_id == 3), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetViewerDataAsync_WithTeamsAndResults_MapsParticipantsScoresAndResults()
    {
        var seasonID = await CreateSeason();
        var (a, b, c, d) = await CreateTeamsWithStandings(seasonID);
        await _service.CreateBracket(4, seasonID, true, 1);
        await _service.SetSeeds(seasonID);

        var w1 = await _context.SeasonMatches.SingleAsync(x => x.BracketMatch!.Bracket == Bracket.Winner && x.BracketMatch.MatchNumber == 1);
        w1.HomeTeamScore = 2; w1.HomeTeamResult = SeasonMatchResult.Won;
        w1.AwayTeamScore = 0; w1.AwayTeamResult = SeasonMatchResult.Loss;
        var w2 = await _context.SeasonMatches.SingleAsync(x => x.BracketMatch!.Bracket == Bracket.Winner && x.BracketMatch.MatchNumber == 2);
        w2.HomeTeamResult = SeasonMatchResult.Forfeit;
        w2.AwayTeamResult = SeasonMatchResult.Won;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var data = await _service.GetViewerDataAsync(seasonID);

        var m1 = data.Matches.Single(m => m.Id == w1.SeasonMatchID);
        var m2 = data.Matches.Single(m => m.Id == w2.SeasonMatchID);
        Assert.Multiple(() =>
        {
            // W1 = seed 1 (C) vs seed 4 (B); W2 = seed 3 (D) vs seed 2 (A)
            Assert.That(data.Participants.Select(p => (p.Id, p.Name, p.Tournament_id)), Is.EqualTo(new[]
            {
                (c.TeamID, "Charlie", seasonID), (b.TeamID, "Bravo", seasonID), (d.TeamID, "Delta", seasonID), (a.TeamID, "Alpha", seasonID),
            }));
            Assert.That((m1.Opponent1!.Id, m1.Opponent1.Score, m1.Opponent1.Result, m1.Opponent1.Forfeit),
                Is.EqualTo(((int?)c.TeamID, (int?)2, (Result?)Result.win, false)));
            Assert.That((m1.Opponent2!.Id, m1.Opponent2.Score, m1.Opponent2.Result, m1.Opponent2.Forfeit),
                Is.EqualTo(((int?)b.TeamID, (int?)0, (Result?)Result.loss, false)));
            // A forfeit is shown as a loss with the forfeit flag
            Assert.That((m2.Opponent1!.Id, m2.Opponent1.Result, m2.Opponent1.Forfeit),
                Is.EqualTo(((int?)d.TeamID, (Result?)Result.loss, true)));
            Assert.That((m2.Opponent2!.Id, m2.Opponent2.Result, m2.Opponent2.Forfeit),
                Is.EqualTo(((int?)a.TeamID, (Result?)Result.win, false)));
        });
    }

    [TestCase(Bracket.Winner)]
    [TestCase(Bracket.Loser)]
    [TestCase(Bracket.GrandFinal)]
    [TestCase(Bracket.GrandFinalSuddenDeath)]
    public async Task GetViewerDataAsync_ByeResult_ThrowsArgumentOutOfRange(Bracket kind)
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);
        var match = await _context.SeasonMatches.FirstAsync(x => x.BracketMatch!.Bracket == kind);
        match.AwayTeamResult = SeasonMatchResult.Bye;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // BUG: BracketService.MapResult has no case for SeasonMatchResult.Bye, although the same code treats Bye as
        // forfeit-like (Forfeit = result is Forfeit or Bye). Expected: a bye maps to a result (e.g. loss with forfeit=true);
        // actual: the whole viewer request fails with ArgumentOutOfRangeException.
        var ex = Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GetViewerDataAsync(seasonID));
        Assert.That(ex!.ParamName, Is.EqualTo("r"));
    }

    [Test]
    public async Task GetViewerDataAsync_FirstRoundHomeWithoutSeedOrTeam_Throws()
    {
        var seasonID = await CreateSeason();
        var feeder = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo { MatchNumber = 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.Loser },
        };
        _context.SeasonMatches.Add(feeder);
        await _context.SaveChangesAsync();
        var invalid = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo
            {
                MatchNumber = 2, RoundNumber = 1, Bracket = Bracket.Winner,
                HomeTeamPreviousMatchBracketInfoID = feeder.BracketMatch.MatchBracketInfoID,
                AwayTeamSeedNumber = 2,
            },
        };
        _context.SeasonMatches.Add(invalid);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetViewerDataAsync(seasonID));

        Assert.That(ex!.Message, Is.EqualTo($"Season Match {invalid.SeasonMatchID} is not valid for first round of bracket"));
    }

    [Test]
    public async Task GetViewerDataAsync_FirstRoundAwayWithoutSeedOrTeam_Throws()
    {
        var seasonID = await CreateSeason();
        var feeder = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo { MatchNumber = 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.Loser },
        };
        _context.SeasonMatches.Add(feeder);
        await _context.SaveChangesAsync();
        var invalid = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo
            {
                MatchNumber = 2, RoundNumber = 1, Bracket = Bracket.Winner,
                HomeTeamSeedNumber = 1,
                AwayTeamPreviousMatchBracketInfoID = feeder.BracketMatch.MatchBracketInfoID,
            },
        };
        _context.SeasonMatches.Add(invalid);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetViewerDataAsync(seasonID));

        Assert.That(ex!.Message, Is.EqualTo($"Season Match {invalid.SeasonMatchID} is not valid for first round of bracket"));
    }

    [Test]
    public async Task GetViewerDataAsync_WinnerRoundWithoutPreviousRound_Throws()
    {
        var seasonID = await CreateSeason();
        var feeder = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo { MatchNumber = 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.Loser },
        };
        _context.SeasonMatches.Add(feeder);
        await _context.SaveChangesAsync();
        _context.SeasonMatches.Add(new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = new MatchBracketInfo
            {
                MatchNumber = 2, RoundNumber = 2, Bracket = Bracket.Winner,
                HomeTeamPreviousMatchBracketInfoID = feeder.BracketMatch.MatchBracketInfoID,
                AwayTeamPreviousMatchBracketInfoID = feeder.BracketMatch.MatchBracketInfoID,
            },
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.GetViewerDataAsync(seasonID));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find previous rounds last match"));
    }

    // ---------------- SetSeeds from standings ----------------

    [Test]
    public async Task SetSeeds_WithoutCustomSeeds_UsesStandingsOrder()
    {
        var seasonID = await CreateSeason();
        var (a, b, c, d) = await CreateTeamsWithStandings(seasonID);
        await _service.CreateBracket(4, seasonID, true, 1);

        await _service.SetSeeds(seasonID);

        var bracket = await LoadTrackedBracket(seasonID);
        var w1 = bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == 1).SeasonMatch;
        var w2 = bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == 2).SeasonMatch;
        Assert.Multiple(() =>
        {
            Assert.That((w1.HomeTeamID, w1.AwayTeamID), Is.EqualTo(((int?)c.TeamID, (int?)b.TeamID)), "seed 1 vs seed 4");
            Assert.That((w2.HomeTeamID, w2.AwayTeamID), Is.EqualTo(((int?)d.TeamID, (int?)a.TeamID)), "seed 3 vs seed 2");
            // Later rounds remain undecided
            Assert.That(bracket.Where(x => x.RoundNumber > 1 || x.Bracket != Bracket.Winner)
                .All(x => x.SeasonMatch.HomeTeamID is null && x.SeasonMatch.AwayTeamID is null), Is.True);
        });
    }

    [Test]
    public async Task SetSeeds_WithoutCustomSeeds_NotEnoughTeams_Throws()
    {
        var seasonID = await CreateSeason();
        await CreateTeamsWithStandings(seasonID);
        await _service.CreateBracket(8, seasonID, false, 1);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.SetSeeds(seasonID));

        Assert.That(ex!.Message, Is.EqualTo("Missing home or away team. Byes are currently not supported"));
    }

    // ---------------- DetermineNextMatches ----------------

    [Test]
    public async Task DetermineNextMatches_DoubleElimination_FollowsBracketGraph()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);
        var bracket = await LoadTrackedBracket(seasonID);
        SeasonMatch W(int n) => bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == n).SeasonMatch;
        SeasonMatch L(int n) => bracket.Single(x => x.Bracket == Bracket.Loser && x.MatchNumber == n).SeasonMatch;
        var gf = bracket.Single(x => x.Bracket == Bracket.GrandFinal).SeasonMatch;
        var sd = bracket.Single(x => x.Bracket == Bracket.GrandFinalSuddenDeath).SeasonMatch;

        void Check(SeasonMatch match, SeasonMatch? winner, bool? winnerHome, SeasonMatch? loser, bool? loserHome)
        {
            var next = _service.DetermineNextMatches(match);
            Assert.That(next.Winner?.Game, Is.SameAs(winner), $"winner of {match.BracketMatch!.Bracket} {match.BracketMatch.MatchNumber}");
            Assert.That(next.Winner?.IsHomeTeam, Is.EqualTo(winnerHome));
            Assert.That(next.Loser?.Game, Is.SameAs(loser), $"loser of {match.BracketMatch.Bracket} {match.BracketMatch.MatchNumber}");
            Assert.That(next.Loser?.IsHomeTeam, Is.EqualTo(loserHome));
        }

        Assert.Multiple(() =>
        {
            Check(W(1), W(3), true, L(1), true);
            Check(W(2), W(3), false, L(1), false);
            Check(W(3), gf, true, L(2), true);
            Check(L(1), L(2), false, null, null);
            Check(L(2), gf, false, null, null);
            Check(gf, sd, true, sd, false);
            Check(sd, null, null, null, null);
        });
    }

    [Test]
    public async Task DetermineNextMatches_SingleElimination_FinalHasNoNextMatches()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, false, 1);
        var bracket = await LoadTrackedBracket(seasonID);
        var w1 = bracket.Single(x => x.MatchNumber == 1).SeasonMatch;
        var final = bracket.Single(x => x.MatchNumber == 3).SeasonMatch;

        var first = _service.DetermineNextMatches(w1);
        var last = _service.DetermineNextMatches(final);

        Assert.Multiple(() =>
        {
            Assert.That(first.Winner!.Game, Is.SameAs(final));
            Assert.That(first.Winner.IsHomeTeam, Is.True);
            Assert.That(first.Loser, Is.Null);
            Assert.That(last.Winner, Is.Null);
            Assert.That(last.Loser, Is.Null);
        });
    }

    private static SeasonMatch InMemory(Bracket kind) => new() { BracketMatch = new MatchBracketInfo { Bracket = kind } };

    [Test]
    public void DetermineNextMatches_LoserBracket_HomeSlotOfNextMatch()
    {
        var match = InMemory(Bracket.Loser);
        var next = InMemory(Bracket.Loser);
        next.BracketMatch!.SeasonMatch = next;
        match.BracketMatch!.InverseHomeTeamPreviousMatchBracketInfo.Add(next.BracketMatch);

        var result = _service.DetermineNextMatches(match);

        Assert.That(result.Winner!.Game, Is.SameAs(next));
        Assert.That(result.Winner.IsHomeTeam, Is.True);
        Assert.That(result.Loser, Is.Null);
    }

    [Test]
    public void DetermineNextMatches_LoserBracketWithoutNext_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _service.DetermineNextMatches(InMemory(Bracket.Loser)));
        Assert.That(ex!.Message, Is.EqualTo("Failed to determine winners next match from loser bracket"));
    }

    [Test]
    public void DetermineNextMatches_GrandFinalWithoutNext_Throws()
    {
        var ex = Assert.Throws<Exception>(() => _service.DetermineNextMatches(InMemory(Bracket.GrandFinal)));
        Assert.That(ex!.Message, Is.EqualTo("Failed to find winner next match after grand final"));
    }

    [Test]
    public void DetermineNextMatches_GrandFinalWithoutAwayNext_Throws()
    {
        var gf = InMemory(Bracket.GrandFinal);
        var sd = InMemory(Bracket.GrandFinalSuddenDeath);
        sd.BracketMatch!.SeasonMatch = sd;
        gf.BracketMatch!.InverseHomeTeamPreviousMatchBracketInfo.Add(sd.BracketMatch);

        var ex = Assert.Throws<Exception>(() => _service.DetermineNextMatches(gf));
        Assert.That(ex!.Message, Is.EqualTo("Failed to find loser next match after grand final"));
    }

    [Test]
    public void DetermineNextMatches_UnknownBracket_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.DetermineNextMatches(InMemory((Bracket)42)));
    }
}
