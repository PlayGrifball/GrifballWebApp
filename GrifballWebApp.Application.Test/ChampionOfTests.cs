using GrifballWebApp.Domain.League;
using NSubstitute;
using static GrifballWebApp.Application.Championships.ChampionshipService;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.Test;

[TestFixture]
public class ChampionOfTests
{
    /// <summary>A bracket match with these teams and results, decided through its own methods.</summary>
    private static SeasonMatch M(Bracket bracket, int round, int? home, SeasonMatchResult? homeResult, int? away, SeasonMatchResult? awayResult)
    {
        var match = SeasonMatch.Seed(1, home, away, bestOf: 1, bracketMatch: MatchBracketInfo.Seed(bracket: bracket, roundNumber: round));
        return (homeResult, awayResult) switch
        {
            (null, null) => match,
            (W, L) => match.WonBy(SeasonMatchSide.Home),
            (L, W) => match.WonBy(SeasonMatchSide.Away),
            (SeasonMatchResult.Bye, W) => match.WonOnBye(SeasonMatchSide.Home),
            _ => throw new ArgumentException($"No such result: {homeResult}/{awayResult}"),
        };
    }

    private const SeasonMatchResult W = SeasonMatchResult.Won;
    private const SeasonMatchResult L = SeasonMatchResult.Loss;

    [Test]
    public void SingleElimination_TheLastRoundsWinner()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChampionOf([M(Bracket.Winner, 1, 1, W, 2, L), M(Bracket.Winner, 1, 3, L, 4, W), M(Bracket.Winner, 2, 1, L, 4, W)]), Is.EqualTo(4));
            Assert.That(ChampionOf([M(Bracket.Winner, 1, 1, W, 2, L), M(Bracket.Winner, 2, 1, null, 4, null)]), Is.Null, "The final is not played yet");
            Assert.That(ChampionOf([]), Is.Null);
        }
    }

    [Test]
    public void DoubleElimination_TheGrandFinal_OrItsSuddenDeath()
    {
        var winnerBracket = M(Bracket.Winner, 3, 1, W, 2, L);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, W, 3, L), M(Bracket.GrandFinalSuddenDeath, 5, null, null, null, null)]), Is.EqualTo(1),
                "The winner bracket's champion wins the grand final: no sudden death");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, L, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, 3, null, 1, null)]), Is.Null,
                "The sudden death is still to be played");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, L, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, 3, L, 1, W)]), Is.EqualTo(1));
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, null, SeasonMatchResult.Bye, 3, W), M(Bracket.GrandFinalSuddenDeath, 5, null, null, null, null)]), Is.EqualTo(3),
                "A grand final decided by a BYE ends the bracket");
            Assert.That(ChampionOf([winnerBracket, M(Bracket.GrandFinal, 4, 1, null, 3, null)]), Is.Null);
        }
    }
}
