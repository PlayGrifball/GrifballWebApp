using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// BracketService against the fake IApplicationDbContext: drawing, viewing and seeding a bracket, and undoing what a
/// decided match sent on. DetermineNextMatches, GetMatchStatus and SeedPlaceholderID (no database needed) are
/// BracketServiceQueryUnitTests's; this file doesn't repeat them.
/// </summary>
[TestFixture]
internal sealed class BracketServiceUnitTests : ApplicationDbContextTests
{
    private static readonly int[] SeedPlaceholderIds = { -1, -2, -3, -4 };
    private BracketService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new BracketService(Context, new TeamStandingsService(Context), new ChampionshipService(Context));
    }

    private async Task<int> CreateSeason(string name = "Season")
    {
        var season = Season.Seed(seasonName: name);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season.SeasonID;
    }

    private Task<List<MatchBracketInfo>> LoadBracket(int seasonID)
        => Context.Set<MatchBracketInfo>().Include(x => x.SeasonMatch).Where(x => x.SeasonMatch.SeasonID == seasonID).AsNoTracking().ToListAsync();

    private async Task<(SeasonMatch Match1, SeasonMatch Match2, SeasonMatch Final, Team T1, Team T2, Team T3, Team T4)> SeedFourTeamBracket(int seasonID)
    {
        var t1 = Team.Seed(seasonID: seasonID, teamName: "T1");
        var t2 = Team.Seed(seasonID: seasonID, teamName: "T2");
        var t3 = Team.Seed(seasonID: seasonID, teamName: "T3");
        var t4 = Team.Seed(seasonID: seasonID, teamName: "T4");
        Context.Teams.AddRange(t1, t2, t3, t4);
        await Context.SaveChangesAsync();
        await _service.CreateBracket(4, seasonID, doubleElimination: false, bestOf: 1);
        await _service.SetSeeds(seasonID, new[]
        {
            new CustomSeedDto { TeamID = t1.TeamID, Seed = 1 },
            new CustomSeedDto { TeamID = t2.TeamID, Seed = 2 },
            new CustomSeedDto { TeamID = t3.TeamID, Seed = 3 },
            new CustomSeedDto { TeamID = t4.TeamID, Seed = 4 },
        });
        Context.ChangeTracker.Clear();
        var bracket = await Context.SeasonMatches.Include(m => m.BracketMatch).Where(m => m.SeasonID == seasonID).ToListAsync();
        var match1 = bracket.Single(m => m.BracketMatch!.RoundNumber == 1 && m.BracketMatch.MatchNumber == 1);
        var match2 = bracket.Single(m => m.BracketMatch!.RoundNumber == 1 && m.BracketMatch.MatchNumber == 2);
        var final = bracket.Single(m => m.BracketMatch!.RoundNumber == 2);
        return (match1, match2, final, t1, t2, t3, t4);
    }

    #region CreateBracket

    [Test]
    public async Task CreateBracket_SeasonNotFound_ThrowsNotFoundAndWritesNothing()
    {
        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.CreateBracket(4, 987654, false, 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Status, Is.EqualTo(404));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task CreateBracket_SingleElimination_FourTeams_BuildsWinnerTreeWithSeeds()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(4, seasonID, doubleElimination: false, bestOf: 3);

        var bracket = await LoadBracket(seasonID);
        var round1 = bracket.Where(x => x.RoundNumber == 1).OrderBy(x => x.MatchNumber).ToList();
        var final = bracket.Single(x => x.RoundNumber == 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket, Has.Count.EqualTo(3));
            Assert.That(bracket.All(x => x.Bracket == Bracket.Winner), Is.True);
            Assert.That(bracket.All(x => x.SeasonMatch.BestOf == 3), Is.True);
            Assert.That((round1[0].HomeTeamSeedNumber, round1[0].AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)4)));
            Assert.That((round1[1].HomeTeamSeedNumber, round1[1].AwayTeamSeedNumber), Is.EqualTo(((int?)3, (int?)2)));
            Assert.That(final.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(round1[0].MatchBracketInfoID));
            Assert.That(final.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(round1[1].MatchBracketInfoID));
            Assert.That(round1.All(x => x.SeasonMatch.HomeTeamResult is null && x.SeasonMatch.AwayTeamResult is null), Is.True, "4 is an exact power of two: no byes");
        }
    }

    [Test]
    public async Task CreateBracket_NonPowerOfTwo_TopSeedsGetByes()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(3, seasonID, doubleElimination: false, bestOf: 1);

        var bracket = await LoadBracket(seasonID);
        var round1 = bracket.Where(x => x.RoundNumber == 1).OrderBy(x => x.MatchNumber).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket, Has.Count.EqualTo(3)); // a size-4 bracket for 3 participants
            Assert.That((round1[0].HomeTeamSeedNumber, round1[0].AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)4)));
            Assert.That(round1[0].SeasonMatch.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Bye), "seed 4 does not exist: a bye");
            Assert.That(round1[0].SeasonMatch.HomeTeamResult, Is.Null, "seed 1 is real: no team there yet to award the bye to");
            Assert.That((round1[1].HomeTeamSeedNumber, round1[1].AwayTeamSeedNumber), Is.EqualTo(((int?)3, (int?)2)));
            Assert.That(round1[1].SeasonMatch.HomeTeamResult, Is.Null);
            Assert.That(round1[1].SeasonMatch.AwayTeamResult, Is.Null);
        }
    }

    [Test]
    public async Task CreateBracket_DoubleElimination_FourTeams_BuildsGrandFinalAndLoserBracket()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(4, seasonID, doubleElimination: true, bestOf: 5);

        var bracket = await LoadBracket(seasonID);
        MatchBracketInfo W(int n) => bracket.Single(x => x.Bracket == Bracket.Winner && x.MatchNumber == n);
        MatchBracketInfo L(int n) => bracket.Single(x => x.Bracket == Bracket.Loser && x.MatchNumber == n);
        var gf = bracket.Single(x => x.Bracket == Bracket.GrandFinal);
        var sd = bracket.Single(x => x.Bracket == Bracket.GrandFinalSuddenDeath);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracket, Has.Count.EqualTo(7));
            Assert.That(bracket.All(x => x.SeasonMatch.BestOf == 5), Is.True);
            Assert.That(gf.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(3).MatchBracketInfoID));
            Assert.That(gf.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(L(2).MatchBracketInfoID));
            Assert.That(sd.HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(gf.MatchBracketInfoID));
            Assert.That(sd.AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(gf.MatchBracketInfoID));
            Assert.That(L(1).HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(1).MatchBracketInfoID));
            Assert.That(L(1).AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(W(2).MatchBracketInfoID));
            Assert.That(L(2).HomeTeamPreviousMatchBracketInfoID, Is.EqualTo(W(3).MatchBracketInfoID));
            Assert.That(L(2).AwayTeamPreviousMatchBracketInfoID, Is.EqualTo(L(1).MatchBracketInfoID));
        }
    }

    [Test]
    public async Task CreateBracket_DoubleElimination_FiveTeams_ByesPropagateIntoLoserBracket()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(5, seasonID, doubleElimination: true, bestOf: 1);

        var bracket = await LoadBracket(seasonID);
        (SeasonMatchResult?, SeasonMatchResult?) Results(Bracket kind, int n)
        {
            var sm = bracket.Single(x => x.Bracket == kind && x.MatchNumber == n).SeasonMatch;
            return (sm.HomeTeamResult, sm.AwayTeamResult);
        }
        const SeasonMatchResult Bye = SeasonMatchResult.Bye;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Results(Bracket.Winner, 1), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)Bye)));
            Assert.That(Results(Bracket.Winner, 2), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(Results(Bracket.Winner, 3), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)Bye)));
            Assert.That(Results(Bracket.Winner, 4), Is.EqualTo(((SeasonMatchResult?)Bye, (SeasonMatchResult?)null)));
            // A BYE match has no loser: L1 gets a BYE from W1, L2 gets two (from W3 and W4)
            Assert.That(Results(Bracket.Loser, 1), Is.EqualTo(((SeasonMatchResult?)Bye, (SeasonMatchResult?)null)));
            Assert.That(Results(Bracket.Loser, 2), Is.EqualTo(((SeasonMatchResult?)Bye, (SeasonMatchResult?)Bye)));
            // L2 has two BYEs, so its winner slot in L4 is a BYE too
            Assert.That(Results(Bracket.Loser, 3), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
            Assert.That(Results(Bracket.Loser, 4), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)Bye)));
            Assert.That(bracket.Count(x => x.SeasonMatch.HomeTeamResult == Bye || x.SeasonMatch.AwayTeamResult == Bye), Is.EqualTo(6));
        }
    }

    [Test]
    public async Task CreateBracket_AlreadyPlayed_ThrowsConflictAndKeepsExistingBracket()
    {
        var seasonID = await CreateSeason();
        var (match1, _, _, _, _, _, _) = await SeedFourTeamBracket(seasonID);
        match1.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(() => _service.CreateBracket(4, seasonID, false, 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(BracketService.PlayedBracket));
            Assert.That(ex.Status, Is.EqualTo(409));
            Assert.That(await Context.SeasonMatches.CountAsync(m => m.SeasonID == seasonID), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task CreateBracket_Recreate_ReplacesOnlyThatSeasonsBracket()
    {
        var seasonA = await CreateSeason("A");
        var seasonB = await CreateSeason("B");
        await _service.CreateBracket(4, seasonB, false, 1);
        await _service.CreateBracket(8, seasonA, true, 1);

        await _service.CreateBracket(4, seasonA, false, 3);

        var bracketA = await LoadBracket(seasonA);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(bracketA, Has.Count.EqualTo(3));
            Assert.That(bracketA.All(x => x.SeasonMatch.BestOf == 3), Is.True);
            Assert.That(await Context.SeasonMatches.CountAsync(x => x.SeasonID == seasonB), Is.EqualTo(3));
        }
    }

    [Test]
    public async Task CreateBracket_SingleParticipant_CreatesNothing()
    {
        var seasonID = await CreateSeason();

        await _service.CreateBracket(1, seasonID, false, 1);

        Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task CreateBracket_SingleParticipant_DoubleElimination_Throws()
    {
        var seasonID = await CreateSeason();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.CreateBracket(1, seasonID, true, 1));

        Assert.That(ex!.Message, Is.EqualTo("Failed to find the last winner bracket match"));
    }

    #endregion

    #region GetBracketsAsync

    [Test]
    public async Task GetBracketsAsync_SingleElimination_ReturnsWinnerRoundsWithSeedAndWinnerLabels()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, false, 1);

        var dto = await _service.GetBracketsAsync(seasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.WinnerRounds, Has.Length.EqualTo(2));
            Assert.That(dto.LoserRounds, Is.Empty);
            Assert.That(dto.GrandFinal, Is.Null);
            Assert.That(dto.GrandFinalSuddenDeath, Is.Null);
        }
        var round1 = dto.WinnerRounds.Single(r => r.RoundNumber == 1);
        var m1 = round1.Matches.Single(m => m.MatchNumber == "W1");
        var round2 = dto.WinnerRounds.Single(r => r.RoundNumber == 2);
        var final = round2.Matches.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(m1.HomeTeam, Is.EqualTo("Seed 1"));
            Assert.That(m1.AwayTeam, Is.EqualTo("Seed 4"));
            Assert.That(final.HomeTeam, Is.EqualTo("Winner of W1"));
            Assert.That(final.AwayTeam, Is.EqualTo("Winner of W2"));
        }
    }

    [Test]
    public async Task GetBracketsAsync_ByeSlot_ShowsBye()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(3, seasonID, false, 1);

        var dto = await _service.GetBracketsAsync(seasonID);

        var m1 = dto.WinnerRounds.Single(r => r.RoundNumber == 1).Matches.Single(m => m.MatchNumber == "W1");
        Assert.That(m1.AwayTeam, Is.EqualTo("BYE"));
    }

    [Test]
    public async Task GetBracketsAsync_DoubleElimination_ReturnsLoserRoundsAndGrandFinalLabels()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);

        var dto = await _service.GetBracketsAsync(seasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.GrandFinal, Is.Not.Null);
            Assert.That(dto.GrandFinalSuddenDeath, Is.Not.Null);
        }
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.LoserRounds, Has.Length.EqualTo(2));
            Assert.That(dto.GrandFinal!.HomeTeam, Is.EqualTo("Winner of W3"));
            Assert.That(dto.GrandFinal.AwayTeam, Is.EqualTo("Winner of L2"));
            Assert.That(dto.GrandFinalSuddenDeath!.HomeTeam, Is.EqualTo("-"));
            Assert.That(dto.GrandFinalSuddenDeath.AwayTeam, Is.EqualTo("-"));
        }
        var loserRound1 = dto.LoserRounds.Single(r => r.RoundNumber == 1).Matches.Single();
        var loserRound2 = dto.LoserRounds.Single(r => r.RoundNumber == 2).Matches.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loserRound1.HomeTeam, Is.EqualTo("Loser of W1"));
            Assert.That(loserRound1.AwayTeam, Is.EqualTo("Loser of W2"));
            Assert.That(loserRound2.HomeTeam, Is.EqualTo("Loser of W3"));
            Assert.That(loserRound2.AwayTeam, Is.EqualTo("Winner of L1"));
        }
    }

    #endregion

    #region GetViewerDataAsync

    [Test]
    public async Task GetViewerDataAsync_SeasonNotFound_ReturnsNull()
    {
        var result = await _service.GetViewerDataAsync(999);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetViewerDataAsync_SingleElimination_BuildsStageParticipantsAndMatches()
    {
        var seasonID = await CreateSeason("Playoffs");
        await _service.CreateBracket(4, seasonID, false, 1);

        var data = await _service.GetViewerDataAsync(seasonID);

        Assert.That(data, Is.Not.Null);
        var stage = data!.Stages.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stage.Name, Is.EqualTo("Playoffs"));
            Assert.That(stage.Type, Is.EqualTo(StageType.single_elimination));
            Assert.That(stage.Settings.Size, Is.EqualTo(4));
            Assert.That(stage.Settings.GrandFinal, Is.EqualTo(GrandFinalType.none));
            Assert.That(data.Participants.Select(p => p.Id), Is.EquivalentTo(SeedPlaceholderIds), "no teams yet: seed placeholders");
            Assert.That(data.Matches, Has.Count.EqualTo(3));
        }
        var m1 = data.Matches.Single(m => m.Round_id == 1 && m.Number == 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(m1.Status, Is.EqualTo(Status.Locked), "neither side is a known team yet");
            Assert.That(m1.Opponent1!.Id, Is.EqualTo(-1));
            Assert.That(m1.Opponent1.Position, Is.EqualTo(1));
            Assert.That(m1.Opponent2!.Id, Is.EqualTo(-4));
        }
    }

    [Test]
    public async Task GetViewerDataAsync_DoubleElimination_GrandFinalTypeIsDouble()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(4, seasonID, true, 1);

        var data = await _service.GetViewerDataAsync(seasonID);

        var stage = data!.Stages.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stage.Type, Is.EqualTo(StageType.double_elimination));
            Assert.That(stage.Settings.GrandFinal, Is.EqualTo(GrandFinalType.@double));
            Assert.That(data.Matches, Has.Count.EqualTo(7));
        }
    }

    #endregion

    #region SetSeeds

    [Test]
    public async Task SetSeeds_CustomSeeds_AssignsTeamsAndAdvancesByes()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(3, seasonID, false, 1); // W1: seed1 vs BYE(seed4); W2: seed3 vs seed2
        var t1 = Team.Seed(seasonID: seasonID, teamName: "T1");
        var t2 = Team.Seed(seasonID: seasonID, teamName: "T2");
        var t3 = Team.Seed(seasonID: seasonID, teamName: "T3");
        Context.Teams.AddRange(t1, t2, t3);
        await Context.SaveChangesAsync();

        await _service.SetSeeds(seasonID, new[]
        {
            new CustomSeedDto { TeamID = t1.TeamID, Seed = 1 },
            new CustomSeedDto { TeamID = t2.TeamID, Seed = 2 },
            new CustomSeedDto { TeamID = t3.TeamID, Seed = 3 },
        });

        Context.ChangeTracker.Clear();
        var bracket = await Context.SeasonMatches.Include(m => m.BracketMatch).Where(m => m.SeasonID == seasonID).ToListAsync();
        var match1 = bracket.Single(m => m.BracketMatch!.RoundNumber == 1 && m.BracketMatch.MatchNumber == 1);
        var final = bracket.Single(m => m.BracketMatch!.RoundNumber == 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(match1.HomeTeamID, Is.EqualTo(t1.TeamID));
            Assert.That(match1.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won), "the bye auto-wins for the real team");
            Assert.That(final.HomeTeamID, Is.EqualTo(t1.TeamID), "the bye's winner goes straight to the final");
        }
    }

    [Test]
    public async Task SetSeeds_NoCustomSeeds_FallsBackToStandingsOrder()
    {
        var seasonID = await CreateSeason();
        var top = Team.Seed(seasonID: seasonID, teamName: "Top");
        var mid = Team.Seed(seasonID: seasonID, teamName: "Mid");
        Context.Teams.AddRange(top, mid);
        await Context.SaveChangesAsync();
        Context.SeasonMatches.Add(SeasonMatch.Seed(seasonID, top.TeamID, mid.TeamID, bestOf: 1).WonBy(SeasonMatchSide.Home));
        await Context.SaveChangesAsync();
        await _service.CreateBracket(2, seasonID, false, 1);

        await _service.SetSeeds(seasonID, customSeeds: null);

        Context.ChangeTracker.Clear();
        var match = await Context.SeasonMatches.SingleAsync(m => m.SeasonID == seasonID && m.BracketMatch != null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.HomeTeamID, Is.EqualTo(top.TeamID), "standings' top team becomes seed 1 (home)");
            Assert.That(match.AwayTeamID, Is.EqualTo(mid.TeamID));
        }
    }

    [Test]
    public async Task SetSeeds_AlreadySeeded_Throws()
    {
        var seasonID = await CreateSeason();
        var t1 = Team.Seed(seasonID: seasonID, teamName: "T1");
        var t2 = Team.Seed(seasonID: seasonID, teamName: "T2");
        Context.Teams.AddRange(t1, t2);
        await Context.SaveChangesAsync();
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 1, homeSeed: 1, awaySeed: 2);
        Context.SeasonMatches.Add(SeasonMatch.Seed(seasonID, t1.TeamID, t2.TeamID, bestOf: 1, bracketMatch: slot));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.SetSeeds(seasonID,
            new[] { new CustomSeedDto { TeamID = t1.TeamID, Seed = 1 }, new CustomSeedDto { TeamID = t2.TeamID, Seed = 2 } }));

        Assert.That(ex!.Message, Is.EqualTo("Teams have already been seeded"));
    }

    [Test]
    public async Task SetSeeds_MissingSeedMapping_Throws()
    {
        var seasonID = await CreateSeason();
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 1, homeSeed: 1, awaySeed: 3);
        Context.SeasonMatches.Add(SeasonMatch.Seed(seasonID, null, null, bestOf: 1, bracketMatch: slot));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SetSeeds(seasonID, new[] { new CustomSeedDto { TeamID = 1, Seed = 1 } }));

        Assert.That(ex!.Message, Is.EqualTo("Missing home or away team"));
    }

    [Test]
    public async Task SetSeeds_HomeTeamSeedNumberNull_Throws()
    {
        var seasonID = await CreateSeason();
        var fedSlot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 1, homeSeed: 1, awaySeed: 2);
        var fedMatch = SeasonMatch.InBracket(seasonID, fedSlot, bestOf: 1);
        var slot = MatchBracketInfo.From(Bracket.Winner, roundNumber: 2, matchNumber: 2, homeSeed: null, homeFrom: fedSlot, awaySeed: 2, awayFrom: null);
        var match = SeasonMatch.InBracket(seasonID, slot, bestOf: 1);
        Context.SeasonMatches.AddRange(fedMatch, match);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SetSeeds(seasonID,
            new[] { new CustomSeedDto { TeamID = 1, Seed = 1 }, new CustomSeedDto { TeamID = 2, Seed = 2 } }));

        Assert.That(ex!.Message, Is.EqualTo("Missing home team seed number"));
    }

    [Test]
    public async Task SetSeeds_AwayTeamSeedNumberNull_Throws()
    {
        var seasonID = await CreateSeason();
        var fedSlot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 1, homeSeed: 1, awaySeed: 2);
        var fedMatch = SeasonMatch.InBracket(seasonID, fedSlot, bestOf: 1);
        var slot = MatchBracketInfo.From(Bracket.Winner, roundNumber: 2, matchNumber: 2, homeSeed: 1, homeFrom: null, awaySeed: null, awayFrom: fedSlot);
        var match = SeasonMatch.InBracket(seasonID, slot, bestOf: 1);
        Context.SeasonMatches.AddRange(fedMatch, match);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.SetSeeds(seasonID,
            new[] { new CustomSeedDto { TeamID = 1, Seed = 1 }, new CustomSeedDto { TeamID = 2, Seed = 2 } }));

        Assert.That(ex!.Message, Is.EqualTo("Missing away team seed number"));
    }

    [Test]
    public async Task SetSeeds_IgnoresMatchesWithoutSeedNumbers()
    {
        var seasonID = await CreateSeason();
        var (_, _, final, _, _, _, _) = await SeedFourTeamBracket(seasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(final.HomeTeamID, Is.Null, "the final has no seed numbers of its own: untouched by SetSeeds");
            Assert.That(final.AwayTeamID, Is.Null);
        }
    }

    #endregion

    #region UndoAdvancementAsync / UndoAdvancementRefusalAsync

    [Test]
    public async Task UndoAdvancementRefusalAsync_NothingBlocking_ReturnsNull()
    {
        var seasonID = await CreateSeason();
        var (match1, _, final, t1, _, _, _) = await SeedFourTeamBracket(seasonID);
        match1.WonBy(SeasonMatchSide.Home);
        final.PlaceTeam(SeasonMatchSide.Home, t1.TeamID); // what the rest of the app does once a match is decided
        await Context.SaveChangesAsync();

        var refusal = await _service.UndoAdvancementRefusalAsync(match1);

        Assert.That(refusal, Is.Null);
    }

    [Test]
    public async Task UndoAdvancementAsync_NothingBlocking_ClearsTheSlotItFilled()
    {
        var seasonID = await CreateSeason();
        var (match1, _, final, t1, _, _, _) = await SeedFourTeamBracket(seasonID);
        match1.WonBy(SeasonMatchSide.Home);
        final.PlaceTeam(SeasonMatchSide.Home, t1.TeamID);
        await Context.SaveChangesAsync();

        await _service.UndoAdvancementAsync(match1);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        var reloaded = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == final.SeasonMatchID);
        Assert.That(reloaded.HomeTeamID, Is.Null);
    }

    [Test]
    public async Task UndoAdvancementAsync_NextMatchDecided_ThrowsAndRefusalExplainsWhy()
    {
        var seasonID = await CreateSeason();
        var (match1, match2, final, t1, _, t3, _) = await SeedFourTeamBracket(seasonID);
        match1.WonBy(SeasonMatchSide.Home);
        final.PlaceTeam(SeasonMatchSide.Home, t1.TeamID);
        match2.WonBy(SeasonMatchSide.Home);
        final.PlaceTeam(SeasonMatchSide.Away, t3.TeamID);
        final.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var refusal = await _service.UndoAdvancementRefusalAsync(match1);
        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.UndoAdvancementAsync(match1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal, Is.EqualTo($"Season match {final.SeasonMatchID} that this result fed into already has a result of its own. Undo that match first."));
            Assert.That(ex!.Message, Is.EqualTo(refusal));
        }
    }

    [Test]
    public async Task UndoAdvancementAsync_NextMatchHasGames_ThrowsAndRefusalExplainsWhy()
    {
        var seasonID = await CreateSeason();
        var (match1, _, final, t1, _, _, _) = await SeedFourTeamBracket(seasonID);
        match1.WonBy(SeasonMatchSide.Home);
        final.PlaceTeam(SeasonMatchSide.Home, t1.TeamID);
        var haloMatch = HaloMatch.Seed(Guid.NewGuid());
        Context.Matches.Add(haloMatch);
        final.Game(1, halo: haloMatch);
        await Context.SaveChangesAsync();

        var refusal = await _service.UndoAdvancementRefusalAsync(match1);
        var ex = await Assert.ThrowsAsync<SeasonMatchActionNotAllowedException>(() => _service.UndoAdvancementAsync(match1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal, Is.EqualTo($"Season match {final.SeasonMatchID} that this result fed into already has games. Undo that match first."));
            Assert.That(ex!.Message, Is.EqualTo(refusal));
        }
    }

    #endregion

    #region SendByesToNextMatches (pure: no database)

    [Test]
    public void SendByesToNextMatches_DoubleForfeit_PutsByeInTheMatchItFed()
    {
        var pureService = new BracketService(null!, null!, null!);
        var match = SeasonMatch.Seed(0, 1, 2, bestOf: 1, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.Winner, roundNumber: 1, matchNumber: 1));
        match.Forfeited(Forfeiter.Both);
        var winnerNext = SeasonMatch.Seed(0, null, null,
            bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.Winner, roundNumber: 2, matchNumber: 2, homeTeamPreviousMatchBracketInfo: match.BracketMatch));
        winnerNext.BracketMatch!.SeasonMatch = winnerNext;

        pureService.SendByesToNextMatches(match);

        Assert.That(winnerNext.IsBye(SeasonMatchSide.Home), Is.True);
    }

    #endregion
}
