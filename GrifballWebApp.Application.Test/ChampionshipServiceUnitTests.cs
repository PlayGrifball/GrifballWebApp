using GrifballWebApp.Application.Championships;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class ChampionshipServiceUnitTests
{
    private static SeasonMatch DecidedMatch(int homeTeamID, int awayTeamID, int homeWins, int awayWins, Bracket bracket, int roundNumber = 1)
    {
        var match = SeasonMatch.Seed(seasonID: 1, homeTeamID: homeTeamID, awayTeamID: awayTeamID, bestOf: 3,
            bracketMatch: MatchBracketInfo.Seed(roundNumber: roundNumber, bracket: bracket, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2));
        match.DecideByGames(homeWins, awayWins);
        return match;
    }

    [Test]
    public void ChampionOf_SingleElimination_LastRoundWinnerBracketMatchWins()
    {
        var round1 = DecidedMatch(10, 20, homeWins: 2, awayWins: 0, Bracket.Winner, roundNumber: 1);
        var round2Final = DecidedMatch(10, 30, homeWins: 2, awayWins: 1, Bracket.Winner, roundNumber: 2);

        var champion = ChampionshipService.ChampionOf([round1, round2Final]);

        Assert.That(champion, Is.EqualTo(10));
    }

    [Test]
    public void ChampionOf_NoBracketMatches_ReturnsNull()
    {
        var champion = ChampionshipService.ChampionOf([]);

        Assert.That(champion, Is.Null);
    }

    [Test]
    public void ChampionOf_GrandFinal_HomeWinsOutright_WinnerBracketChampion()
    {
        // The winner bracket's champion (home) beats the loser bracket's champion in the grand final: decided there.
        var grandFinal = DecidedMatch(10, 20, homeWins: 2, awayWins: 0, Bracket.GrandFinal);

        var champion = ChampionshipService.ChampionOf([grandFinal]);

        Assert.That(champion, Is.EqualTo(10));
    }

    [Test]
    public void ChampionOf_GrandFinal_AwayWinsAndThereIsSuddenDeath_SuddenDeathDecides()
    {
        // The loser bracket's champion (away) forces a sudden death by winning the grand final: the bracket isn't
        // decided by the grand final alone, so the sudden death's winner is the champion.
        var grandFinal = DecidedMatch(10, 20, homeWins: 0, awayWins: 2, Bracket.GrandFinal);
        var suddenDeath = DecidedMatch(10, 20, homeWins: 0, awayWins: 2, Bracket.GrandFinalSuddenDeath);

        var champion = ChampionshipService.ChampionOf([grandFinal, suddenDeath]);

        Assert.That(champion, Is.EqualTo(20));
    }

    [Test]
    public void ChampionOf_GrandFinal_AwayWinsWithNoSuddenDeathSlotAtAll_AwayWinsOutright()
    {
        // No sudden death slot exists in this bracket at all (never just not decided yet): the away win stands.
        var grandFinal = DecidedMatch(10, 20, homeWins: 0, awayWins: 2, Bracket.GrandFinal);

        var champion = ChampionshipService.ChampionOf([grandFinal]);

        Assert.That(champion, Is.EqualTo(20));
    }

    [Test]
    public void ChampionOf_GrandFinal_AwayWinsAndSuddenDeathUndecided_ReturnsNull()
    {
        var grandFinal = DecidedMatch(10, 20, homeWins: 0, awayWins: 2, Bracket.GrandFinal);
        var suddenDeath = SeasonMatch.Seed(seasonID: 1, homeTeamID: 10, awayTeamID: 20, bestOf: 3,
            bracketMatch: MatchBracketInfo.Seed(roundNumber: 1, bracket: Bracket.GrandFinalSuddenDeath, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2));

        var champion = ChampionshipService.ChampionOf([grandFinal, suddenDeath]);

        Assert.That(champion, Is.Null);
    }

    [Test]
    public void ChampionOf_GrandFinalUndecided_ReturnsNull()
    {
        var grandFinal = SeasonMatch.Seed(seasonID: 1, homeTeamID: 10, awayTeamID: 20, bestOf: 3,
            bracketMatch: MatchBracketInfo.Seed(roundNumber: 1, bracket: Bracket.GrandFinal, homeTeamSeedNumber: 1, awayTeamSeedNumber: 2));

        var champion = ChampionshipService.ChampionOf([grandFinal]);

        Assert.That(champion, Is.Null);
    }
}
