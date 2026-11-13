using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// BracketService beyond BracketServiceUnitTests and BracketServiceQueryUnitTests: the viewer's participants (teams,
/// BYEs) and results (win, loss, forfeit), a loser bracket slot's BYE label, a grand final's and its sudden death's next
/// matches, and undoing a result whose loser a BYE then sent further on. Not reachable through the app's own brackets
/// (left out): the dependent-match and missing-team guards of the bracket build and the viewer, a result outside the
/// enum, a loser slot fed by no match.
/// </summary>
[TestFixture]
internal sealed class BracketServiceMoreUnitTests : ApplicationDbContextTests
{
    private static readonly int[] ThreeSeedPlaceholders = [-1, -2, -3];
    private BracketService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new BracketService(Context, new TeamStandingsService(Context), new ChampionshipService(Context));
    }

    private async Task<int> CreateSeason()
    {
        var season = Season.Seed(seasonName: "Playoffs");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season.SeasonID;
    }

    /// <summary><paramref name="count"/> teams T1.., seeded in that order into a bracket of that many.</summary>
    private async Task<List<Team>> SeedBracket(int seasonID, int count, bool doubleElimination)
    {
        var teams = Enumerable.Range(1, count).Select(i => Team.Seed(seasonID: seasonID, teamName: $"T{i}")).ToList();
        Context.Teams.AddRange(teams);
        await Context.SaveChangesAsync();
        await _service.CreateBracket(count, seasonID, doubleElimination, bestOf: 1);
        await _service.SetSeeds(seasonID, teams.Select((t, i) => new CustomSeedDto { TeamID = t.TeamID, Seed = i + 1 }).ToArray());
        return teams;
    }

    private Task<List<SeasonMatch>> TrackedBracket(int seasonID) => Context.SeasonMatches.Include(m => m.BracketMatch)
        .Where(m => m.SeasonID == seasonID && m.BracketMatch != null).ToListAsync();

    [Test]
    public async Task GetViewerDataAsync_SeededAndPlayed_TeamsAreParticipantsAndResultsAreMapped()
    {
        var seasonID = await CreateSeason();
        var teams = await SeedBracket(seasonID, 4, doubleElimination: false);
        var bracket = await TrackedBracket(seasonID);
        var w1 = bracket.Single(m => m.BracketMatch!.Bracket is Bracket.Winner && m.BracketMatch.MatchNumber == 1);
        var w2 = bracket.Single(m => m.BracketMatch!.Bracket is Bracket.Winner && m.BracketMatch.MatchNumber == 2);
        w1.WonBy(SeasonMatchSide.Home);
        w2.Forfeited(Forfeiter.Away);
        await Context.SaveChangesAsync();

        var data = await _service.GetViewerDataAsync(seasonID);

        var m1 = data!.Matches.Single(m => m.Id == w1.SeasonMatchID);
        var m2 = data.Matches.Single(m => m.Id == w2.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.Participants.Select(p => p.Id), Is.EquivalentTo(teams.Select(t => t.TeamID)));
            Assert.That(data.Participants.Select(p => p.Name), Is.EquivalentTo(teams.Select(t => t.TeamName)));
            Assert.That(data.Participants.Select(p => p.Tournament_id), Has.All.EqualTo(seasonID));
            Assert.That(m1.Status, Is.EqualTo(Status.Completed));
            Assert.That(m1.Opponent1!.Result, Is.EqualTo(Result.win));
            Assert.That(m1.Opponent2!.Result, Is.EqualTo(Result.loss));
            Assert.That(m1.Opponent2.Forfeit, Is.False);
            Assert.That(m2.Opponent1!.Result, Is.EqualTo(Result.win));
            Assert.That(m2.Opponent2!.Result, Is.EqualTo(Result.loss));
            Assert.That(m2.Opponent2.Forfeit, Is.True);
        }
    }

    [Test]
    public async Task GetViewerDataAsync_ByeSide_IsNoParticipantAndNoOpponent()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(3, seasonID, false, 1); // W1: seed 1 vs a BYE (seed 4)

        var data = await _service.GetViewerDataAsync(seasonID);

        var w1 = data!.Matches.Single(m => m.Round_id == 1 && m.Number == 1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.Participants.Select(p => p.Id), Is.EquivalentTo(ThreeSeedPlaceholders));
            Assert.That(w1.Opponent2, Is.Null);
            Assert.That(w1.Status, Is.EqualTo(Status.Locked));
        }
    }

    [Test]
    public async Task GetBracketsAsync_DoubleEliminationWithByes_LoserSlotsShowBye()
    {
        var seasonID = await CreateSeason();
        await _service.CreateBracket(5, seasonID, doubleElimination: true, bestOf: 1); // L2 gets a BYE from both W3 and W4

        var dto = await _service.GetBracketsAsync(seasonID);

        var l1 = dto.LoserRounds.SelectMany(r => r.Matches).Single(m => m.MatchNumber == "L1");
        var l2 = dto.LoserRounds.SelectMany(r => r.Matches).Single(m => m.MatchNumber == "L2");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(l1.HomeTeam, Is.EqualTo("BYE"));
            Assert.That(l1.AwayTeam, Is.EqualTo("Loser of W2"));
            Assert.That(l2.HomeTeam, Is.EqualTo("BYE"));
            Assert.That(l2.AwayTeam, Is.EqualTo("BYE"));
        }
    }

    [Test]
    public void DetermineNextMatches_GrandFinalSuddenDeath_SendsNobodyOn()
    {
        var suddenDeath = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.GrandFinalSuddenDeath));

        var next = _service.DetermineNextMatches(suddenDeath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(next.Winner, Is.Null);
            Assert.That(next.Loser, Is.Null);
        }
    }

    [Test]
    public void DetermineNextMatches_GrandFinal_WinnerHomeOfTheNextLoserAwayOfTheOther()
    {
        var grandFinal = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.GrandFinal));
        var winnerNext = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.GrandFinalSuddenDeath,
            homeTeamPreviousMatchBracketInfo: grandFinal.BracketMatch));
        winnerNext.BracketMatch!.SeasonMatch = winnerNext;
        var loserNext = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.GrandFinalSuddenDeath,
            awayTeamPreviousMatchBracketInfo: grandFinal.BracketMatch));
        loserNext.BracketMatch!.SeasonMatch = loserNext;

        var next = _service.DetermineNextMatches(grandFinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(next.Winner!.Game, Is.SameAs(winnerNext));
            Assert.That(next.Winner.IsHomeTeam, Is.True);
            Assert.That(next.Loser!.Game, Is.SameAs(loserNext));
            Assert.That(next.Loser.IsHomeTeam, Is.False);
        }
    }

    /// <summary>
    /// Five teams, double elimination: W2 is played, its winner and loser placed in their next matches; its loser's
    /// loser bracket match faces a BYE (W1 had no loser), so AdvanceByesAsync gives them that win and sends them on.
    /// </summary>
    private async Task<(SeasonMatch W2, NextGame Winner, NextGame Loser, NextGame LoserOnward)> PlayW2WithLoserFacingABye(int seasonID)
    {
        await SeedBracket(seasonID, 5, doubleElimination: true);
        var bracket = await TrackedBracket(seasonID);
        var w2 = bracket.Single(m => m.BracketMatch!.Bracket is Bracket.Winner && m.BracketMatch.MatchNumber == 2);
        w2.WonBy(SeasonMatchSide.Home);
        var next = _service.DetermineNextMatches(w2);
        next.Winner!.Game.PlaceTeam(next.Winner.Side, w2.HomeTeamID!.Value);
        next.Loser!.Game.PlaceTeam(next.Loser.Side, w2.AwayTeamID!.Value);
        await _service.AdvanceByesAsync(seasonID);
        await Context.SaveChangesAsync();
        var onward = _service.DetermineNextMatches(next.Loser.Game).Winner!;
        return (w2, next.Winner, next.Loser, onward);
    }

    [Test]
    public async Task UndoAdvancementAsync_LoserSentOnByABye_TakesBackEveryMoveItMade()
    {
        var seasonID = await CreateSeason();
        var (w2, winner, loser, onward) = await PlayW2WithLoserFacingABye(seasonID);
        var loserTeamID = w2.AwayTeamID;
        var sentOnBeforeUndo = onward.Game.TeamOf(onward.Side);

        var refusal = await _service.UndoAdvancementRefusalAsync(w2);
        await _service.UndoAdvancementAsync(w2);
        await Context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sentOnBeforeUndo, Is.EqualTo(loserTeamID), "the BYE had sent W2's loser on");
            Assert.That(refusal, Is.Null);
            Assert.That(winner.Game.TeamOf(winner.Side), Is.Null);
            Assert.That(loser.Game.TeamOf(loser.Side), Is.Null);
            Assert.That(onward.Game.TeamOf(onward.Side), Is.Null);
            // The BYE stays, its win taken back
            Assert.That(loser.Game.IsBye(SeasonMatch.Other(loser.Side)), Is.True);
            Assert.That(loser.Game.ResultOf(loser.Side), Is.Null);
        }
    }
}
