using GrifballWebApp.Domain.League;
using GrifballWebApp.Application.Brackets;

namespace GrifballWebApp.Application.Test;

/// <summary>BracketServiceQueryTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BracketServiceQueryUnitTests
{
    // DetermineNextMatches reads only the matches it is given
    private readonly BracketService _service = new(null!, null!, null!);

    /// <summary>The states GetMatchStatus maps, each reached through the match's own methods.</summary>
    public enum StatusCase { NoTeams, HomeOnly, AwayOnly, BothTeams, UnderWay, Won, Forfeited, ByeWaiting, ByeWon }

    [TestCase(StatusCase.NoTeams, Status.Locked)]
    [TestCase(StatusCase.HomeOnly, Status.Waiting)]
    [TestCase(StatusCase.AwayOnly, Status.Waiting)]
    [TestCase(StatusCase.BothTeams, Status.Ready)]
    [TestCase(StatusCase.UnderWay, Status.Running)]
    [TestCase(StatusCase.Won, Status.Completed)]
    [TestCase(StatusCase.Forfeited, Status.Completed)]
    [TestCase(StatusCase.ByeWaiting, Status.Locked)]
    [TestCase(StatusCase.ByeWon, Status.Locked)]
    public void GetMatchStatus_FollowsBracketsManager(StatusCase state, Status expected)
    {
        var sm = state switch
        {
            StatusCase.NoTeams => SeasonMatch.Seed(0, null, null, bestOf: 3),
            StatusCase.HomeOnly => SeasonMatch.Seed(0, 1, null, bestOf: 3),
            StatusCase.AwayOnly => SeasonMatch.Seed(0, null, 2, bestOf: 3),
            StatusCase.BothTeams => SeasonMatch.Seed(0, 1, 2, bestOf: 3),
            StatusCase.UnderWay => SeasonMatch.Seed(0, 1, 2, bestOf: 3).Scored(1, 0),
            StatusCase.Won => SeasonMatch.Seed(0, 1, 2, bestOf: 3).WonBy(SeasonMatchSide.Home, 2, 1),
            StatusCase.Forfeited => SeasonMatch.Seed(0, 1, 2, bestOf: 3).Forfeited(Forfeiter.Home),
            // A BYE side has no team: the team facing it is still to come, or has won on it
            StatusCase.ByeWaiting => SeasonMatch.Seed(0, null, null, bestOf: 3).WithBye(SeasonMatchSide.Home),
            _ => SeasonMatch.Seed(0, 1, null, bestOf: 3).WonOnBye(SeasonMatchSide.Away),
        };

        Assert.That(BracketService.GetMatchStatus(sm), Is.EqualTo(expected));
    }

    [Test]
    public void SeedPlaceholderID_IsNegative()
    {
        Assert.That((BracketService.SeedPlaceholderID(3), BracketService.SeedPlaceholderID((int?)null)), Is.EqualTo(((int?)-3, (int?)null)));
    }

    private static SeasonMatch InMemory(Bracket kind) => SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: kind));

    [Test]
    public void DetermineNextMatches_LoserBracket_HomeSlotOfNextMatch()
    {
        var match = InMemory(Bracket.Loser);
        var next = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.Loser, homeTeamPreviousMatchBracketInfo: match.BracketMatch));
        next.BracketMatch!.SeasonMatch = next;

        var result = _service.DetermineNextMatches(match);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Winner!.Game, Is.SameAs(next));
            Assert.That(result.Winner.IsHomeTeam, Is.True);
            Assert.That(result.Loser, Is.Null);
        }
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
        var sd = SeasonMatch.Seed(0, null, null, bracketMatch: MatchBracketInfo.Seed(bracket: Bracket.GrandFinalSuddenDeath, homeTeamPreviousMatchBracketInfo: gf.BracketMatch));
        sd.BracketMatch!.SeasonMatch = sd;

        var ex = Assert.Throws<Exception>(() => _service.DetermineNextMatches(gf));
        Assert.That(ex!.Message, Is.EqualTo("Failed to find loser next match after grand final"));
    }

    [Test]
    public void DetermineNextMatches_UnknownBracket_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.DetermineNextMatches(InMemory((Bracket)42)));
    }
}
