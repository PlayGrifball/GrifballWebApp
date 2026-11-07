using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>
/// A season match's result state (#298): one owner, the match. Its methods move it only between the legal states, and
/// what decided it, who won and who lost are the match's to say.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchModelTests
{
    private static SeasonMatch Ready(int bestOf = 3) => SeasonMatch.Seed(1, 10, 20, bestOf);

    private static readonly System.Linq.Expressions.Expression<Func<SeasonMatch, bool>> Undecided = SeasonMatch.Undecided;
    private static readonly Func<SeasonMatch, bool> IsUndecided = Undecided.Compile();

    [Test]
    public void Between_IsAnUndecidedMatchOfTwoTeams_NeverATeamAgainstItself()
    {
        var match = SeasonMatch.Between(1, 10, 20, 3, new DateTime(2030, 1, 1));

        Assert.Multiple(() =>
        {
            Assert.That((match.SeasonID, match.HomeTeamID, match.AwayTeamID, match.BestOf, match.ScheduledTime),
                Is.EqualTo((1, (int?)10, (int?)20, 3, (DateTime?)new DateTime(2030, 1, 1))));
            Assert.That(match.IsDecided, Is.False);
            Assert.That(match.DecidedBy, Is.Null);
            Assert.That(match.HasBothTeams, Is.True);
            Assert.Throws<ArgumentException>(() => SeasonMatch.Between(1, 10, 10, 3));
        });
    }

    [Test]
    public void InBracket_IsASlotWithoutTeams_ItsBYEsGivenFromTheStart()
    {
        var info = new MatchBracketInfo { Bracket = Bracket.Winner, RoundNumber = 1, MatchNumber = 1 };
        var plain = SeasonMatch.InBracket(1, info, 3);
        var bye = SeasonMatch.InBracket(1, new MatchBracketInfo(), 3, awayBye: true);

        Assert.Multiple(() =>
        {
            Assert.That(plain.BracketMatch, Is.SameAs(info));
            Assert.That((plain.HomeTeamID, plain.AwayTeamID, plain.IsDecided), Is.EqualTo(((int?)null, (int?)null, false)));
            Assert.That((bye.HomeTeamResult, bye.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)SeasonMatchResult.Bye)));
            Assert.That(bye.DecidedBy, Is.EqualTo(SeasonMatchDecision.Bye));
            Assert.That(bye.Winner, Is.Null, "Nobody won yet: the team facing the BYE is still to come");
        });
    }

    [TestCase(3, 1, SeasonMatchSide.Home)]
    [TestCase(1, 3, SeasonMatchSide.Away)]
    [TestCase(2, 2, null)]
    public void Imported_MoreWinsWin_AndADrawIsUndecided(int homeWins, int awayWins, SeasonMatchSide? winner)
    {
        // A side the import could not tell has no team, and still wins or loses
        var match = SeasonMatch.Imported(1, null, 20, homeWins, awayWins, 5, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(match.Winner, Is.EqualTo(winner));
            Assert.That(match.DecidedBy, Is.EqualTo(winner is null ? null : (SeasonMatchDecision?)SeasonMatchDecision.Games));
            Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)homeWins, (int?)awayWins)));
        });
    }

    [Test]
    public void DecideByGames_ScoresTheSeries_AndDecidesItOnceASideHasTheWinsItNeeds()
    {
        var match = Ready(bestOf: 5);

        var first = match.DecideByGames(2, 1);
        var underWay = (match.HomeTeamScore, match.AwayTeamScore);
        var second = SeasonMatch.Seed(1, 10, 20, 5).DecideByGames(1, 3);
        var won = Ready(bestOf: 5);
        won.DecideByGames(1, 3);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.False, "2-1 of a best of 5 is under way");
            Assert.That(match.IsDecided, Is.False);
            Assert.That(underWay, Is.EqualTo(((int?)2, (int?)1)));
            Assert.That(second, Is.True);
            Assert.That((won.Winner, won.Loser, won.DecidedBy), Is.EqualTo(((SeasonMatchSide?)SeasonMatchSide.Away, (SeasonMatchSide?)SeasonMatchSide.Home, (SeasonMatchDecision?)SeasonMatchDecision.Games)));
            Assert.That((won.HomeTeamResult, won.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Loss, (SeasonMatchResult?)SeasonMatchResult.Won)));
            Assert.That((won.WinnerTeamID, won.LoserTeamID), Is.EqualTo(((int?)20, (int?)10)));
        });
    }

    [Test]
    public void DecideByGames_WithoutGames_HasNoScore()
    {
        var match = Ready();

        match.DecideByGames(0, 0);

        Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)null, (int?)null)), "As before any were reported");
    }

    [Test]
    public void DecideByGames_IsRefused_ForADecidedMatch_OrOneMissingATeam()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Ready().WonBy(SeasonMatchSide.Home).DecideByGames(2, 0), "Reopened first");
            Assert.Throws<InvalidOperationException>(() => SeasonMatch.Seed(1, 10, null, 3).DecideByGames(2, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ready().DecideByGames(-1, 0));
        });
    }

    [TestCase(Forfeiter.Home, SeasonMatchResult.Forfeit, SeasonMatchResult.Won, SeasonMatchSide.Away)]
    [TestCase(Forfeiter.Away, SeasonMatchResult.Won, SeasonMatchResult.Forfeit, SeasonMatchSide.Home)]
    [TestCase(Forfeiter.Both, SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit, null)]
    public void Forfeit_TheOtherSideWins_OrNobodyWhenBothForfeit(Forfeiter forfeiter, SeasonMatchResult home, SeasonMatchResult away, SeasonMatchSide? winner)
    {
        var match = Ready().Scored(1, 0);

        var result = match.Forfeit(forfeiter);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(ForfeitResult.Forfeited));
            Assert.That((match.HomeTeamResult, match.AwayTeamResult), Is.EqualTo(((SeasonMatchResult?)home, (SeasonMatchResult?)away)));
            Assert.That(match.DecidedBy, Is.EqualTo(SeasonMatchDecision.Forfeit));
            Assert.That(match.Winner, Is.EqualTo(winner));
            Assert.That(match.Loser, Is.EqualTo(winner is { } w ? SeasonMatch.Other(w) : (SeasonMatchSide?)null), "Both forfeiting leaves no single loser");
            Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)1, (int?)0)), "The games before it stay");
        });
    }

    [Test]
    public void Forfeit_IsRefused_WithoutBothTeams_OnceDecided_AndForAGrandFinal()
    {
        var grandFinal = SeasonMatch.Seed(1, 10, 20, 3, bracketMatch: new MatchBracketInfo { Bracket = Bracket.GrandFinal });
        var suddenDeath = SeasonMatch.Seed(1, 10, 20, 3, bracketMatch: new MatchBracketInfo { Bracket = Bracket.GrandFinalSuddenDeath });
        var bye = SeasonMatch.Seed(1, null, 20, 3).WithBye(SeasonMatchSide.Home);

        Assert.Multiple(() =>
        {
            Assert.That(SeasonMatch.Seed(1, 10, null, 3).Forfeit(Forfeiter.Home), Is.EqualTo(ForfeitResult.TeamsMissing));
            Assert.That(bye.Forfeit(Forfeiter.Away), Is.EqualTo(ForfeitResult.TeamsMissing));
            Assert.That(Ready().WonBy(SeasonMatchSide.Home).Forfeit(Forfeiter.Away), Is.EqualTo(ForfeitResult.AlreadyDecided));
            Assert.That(Ready().Forfeited(Forfeiter.Home).Forfeit(Forfeiter.Away), Is.EqualTo(ForfeitResult.AlreadyDecided));
            Assert.That(grandFinal.Forfeit(Forfeiter.Home), Is.EqualTo(ForfeitResult.GrandFinal));
            Assert.That(suddenDeath.Forfeit(Forfeiter.Both), Is.EqualTo(ForfeitResult.GrandFinal));
            Assert.That(grandFinal.IsDecided, Is.False);
        });
    }

    [Test]
    public void Reopen_TakesBackAResultButKeepsTheGames_NeverABYEs()
    {
        var won = Ready().WonBy(SeasonMatchSide.Home, 2, 1);
        var forfeited = Ready().Scored(1, 0).Forfeited(Forfeiter.Away);
        var undecided = Ready();
        var bye = SeasonMatch.Seed(1, null, 20, 3).WonOnBye(SeasonMatchSide.Home);

        won.Reopen();
        forfeited.Reopen();
        undecided.Reopen();

        Assert.Multiple(() =>
        {
            Assert.That((won.IsDecided, won.HomeTeamScore, won.AwayTeamScore), Is.EqualTo((false, (int?)2, (int?)1)));
            Assert.That((forfeited.IsDecided, forfeited.HomeTeamScore), Is.EqualTo((false, (int?)1)));
            Assert.That(undecided.IsDecided, Is.False, "Nothing to take back");
            Assert.That(bye.CanReopen, Is.False);
            Assert.Throws<InvalidOperationException>(() => bye.Reopen());
            Assert.That(bye.Winner, Is.EqualTo(SeasonMatchSide.Away), "Unchanged");
        });
    }

    [Test]
    public void TheSeries_TakesNewGamesUntilDecidedOrFull()
    {
        var full = SeasonMatch.Seed(1, 10, 20, 1);
        full.Game(1, recorded: new RecordedGame { SourceKey = "manual/1" });

        Assert.Multiple(() =>
        {
            Assert.That(Ready().WhyNoNewGame(), Is.Null);
            Assert.That(Ready().WonBy(SeasonMatchSide.Away).WhyNoNewGame(), Is.EqualTo(NewGameRefusal.Decided));
            Assert.That(Ready().Forfeited(Forfeiter.Both).WhyNoNewGame(), Is.EqualTo(NewGameRefusal.Decided));
            Assert.That(full.WhyNoNewGame(), Is.EqualTo(NewGameRefusal.Full));
        });
    }

    [Test]
    public void ItsGames_AreAddedNumberedAndRemovedThroughIt_RemovedOnlyWhileUndecided()
    {
        var match = Ready();
        var halo = new Match { MatchID = Guid.NewGuid() };
        var first = match.AddGame(halo);
        var second = match.AddGame(new RecordedGame { SourceKey = "manual/2" }, 2);
        match.NumberGame(first, 1);
        var stranger = Ready().AddGame(new RecordedGame { SourceKey = "manual/3" }, 1);

        Assert.Multiple(() =>
        {
            Assert.That(match.SeriesGames, Is.EquivalentTo(new[] { first, second }));
            Assert.That((first.SeasonMatch, first.Match, first.GameNumber), Is.EqualTo((match, halo, 1)));
            Assert.Throws<InvalidOperationException>(() => match.NumberGame(stranger, 3), "Not its game");
            Assert.Throws<InvalidOperationException>(() => match.RemoveGame(stranger));
        });

        match.DecideByGames(2, 0);
        Assert.Throws<InvalidOperationException>(() => match.RemoveGame(first), "Reopened first");
        Assert.Throws<InvalidOperationException>(() => match.ClearSeries(), "Reopened first");

        match.Reopen();
        match.RemoveGame(first);
        Assert.That(match.SeriesGames, Is.EqualTo(new[] { second }));

        var cleared = match.ClearSeries();
        Assert.Multiple(() =>
        {
            Assert.That(cleared, Is.EqualTo(new[] { second }));
            Assert.That(match.SeriesGames, Is.Empty);
            Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)null, (int?)null)));
        });
    }

    [Test]
    public void AnImportedSeries_TakesItsGamesByWaypointIDOrRecordedGame()
    {
        var match = SeasonMatch.Imported(1, 10, 20, 2, 0, 3, null, null);
        var id = Guid.NewGuid();

        var waypoint = match.AddImportedGame(id, null, 1);
        var recorded = match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/1" }, 2);

        Assert.That((waypoint.MatchID, waypoint.GameNumber, recorded.RecordedGame!.SourceKey, recorded.GameNumber), Is.EqualTo(((Guid?)id, 1, "archive/1", 2)));
    }

    /// <summary>An imported 2-1 series (home won games 1 and 2, away game 3).</summary>
    private static (SeasonMatch Match, SeriesGame[] Games) ImportedTwoOne()
    {
        var match = SeasonMatch.Imported(1, 10, 20, 2, 1, 3, null, null);
        var games = Enumerable.Range(1, 3).Select(n => match.AddImportedGame(null, new RecordedGame { SourceKey = $"archive/{n}" }, n)).ToArray();
        return (match, games);
    }

    [TestCase(true, false, 1, 1, null, null, 2, TestName = "RemoveImportedGame_AWinnersWinGone_LeavesADraw_Undecided")]
    [TestCase(false, true, 2, 0, SeasonMatchResult.Won, SeasonMatchResult.Loss, 3, TestName = "RemoveImportedGame_ALosersWinGone_KeepsTheWinner")]
    [TestCase(false, false, 2, 1, SeasonMatchResult.Won, SeasonMatchResult.Loss, 3, TestName = "RemoveImportedGame_AGameNobodyWon_LeavesTheScore")]
    public void RemoveImportedGame_WorksTheSeriesOutAgain_AsImportedDoes(bool homeWon, bool awayWon, int home, int away, SeasonMatchResult? homeResult, SeasonMatchResult? awayResult, int bestOf)
    {
        var (match, games) = ImportedTwoOne();
        var gone = games[homeWon ? 0 : 2];

        var left = match.RemoveImportedGame(gone, homeWon, awayWon);

        var fresh = SeasonMatch.Imported(1, 10, 20, home, away, SeasonMatch.ImportedBestOf(2, home, away), null, null);
        Assert.Multiple(() =>
        {
            Assert.That(left, Is.True);
            Assert.That(match.SeriesGames, Has.Count.EqualTo(2).And.No.Member(gone));
            Assert.That((match.HomeTeamScore, match.AwayTeamScore, match.HomeTeamResult, match.AwayTeamResult, match.BestOf),
                Is.EqualTo(((int?)home, (int?)away, homeResult, awayResult, bestOf)));
            Assert.That((match.HomeTeamResult, match.AwayTeamResult, match.BestOf), Is.EqualTo((fresh.HomeTeamResult, fresh.AwayTeamResult, fresh.BestOf)),
                "The same as importing the games left");
        });
    }

    [Test]
    public void RemoveImportedGame_ADrawGainsAWinner_WhenTheOtherSidesWinGoes()
    {
        var match = SeasonMatch.Imported(1, 10, 20, 1, 1, 2, null, null);
        var homeWin = match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/1" }, 1);
        match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/2" }, 2);

        match.RemoveImportedGame(homeWin, homeWon: true, awayWon: false);

        Assert.That((match.HomeTeamScore, match.AwayTeamScore, match.Winner, match.DecidedBy, match.BestOf),
            Is.EqualTo(((int?)0, (int?)1, (SeasonMatchSide?)SeasonMatchSide.Away, (SeasonMatchDecision?)SeasonMatchDecision.Games, 1)));
    }

    [Test]
    public void RemoveImportedGame_TheLastGame_LeavesNoneForTheImportToDelete()
    {
        var match = SeasonMatch.Imported(1, null, 20, 1, 0, 1, null, null);
        var only = match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/1" }, 1);

        var left = match.RemoveImportedGame(only, homeWon: true, awayWon: false);

        Assert.Multiple(() =>
        {
            Assert.That(left, Is.False);
            Assert.That(match.SeriesGames, Is.Empty);
            Assert.That((match.HomeTeamScore, match.AwayTeamScore, match.IsDecided, match.BestOf), Is.EqualTo(((int?)0, (int?)0, false, 1)));
        });
    }

    [Test]
    public void RemoveImportedGame_KeepsAForfeit_WhichWasNotTheGames()
    {
        var match = SeasonMatch.Imported(1, 10, 20, 1, 1, 2, null, null);
        var game = match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/1" }, 1);
        match.AddImportedGame(null, new RecordedGame { SourceKey = "archive/2" }, 2);
        match.Forfeit(Forfeiter.Away);

        match.RemoveImportedGame(game, homeWon: true, awayWon: false);

        Assert.That((match.HomeTeamScore, match.AwayTeamScore, match.HomeTeamResult, match.AwayTeamResult),
            Is.EqualTo(((int?)0, (int?)1, (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Forfeit)));
    }

    [Test]
    public void RemoveImportedGame_OnlyItsOwnGame()
    {
        var (match, _) = ImportedTwoOne();
        var (_, others) = ImportedTwoOne();

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => match.RemoveImportedGame(others[0], true, false));
            Assert.That((match.SeriesGames.Count, match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo((3, (int?)2, (int?)1)), "Unchanged");
        });
    }

    [TestCase(1, 1, 0, ExpectedResult = 1)]
    [TestCase(2, 1, 1, ExpectedResult = 2)]
    [TestCase(2, 2, 0, ExpectedResult = 3)]
    [TestCase(3, 2, 1, ExpectedResult = 3)]
    [TestCase(0, 0, 0, ExpectedResult = 1)]
    public int ImportedBestOf_IsItsGames_OrWhatTheLeadersWinsNeed(int games, int homeWins, int awayWins) => SeasonMatch.ImportedBestOf(games, homeWins, awayWins);

    [Test]
    public void PlaceTeam_FillsASide_NeverABYEsOrAgainstItself()
    {
        var match = SeasonMatch.Seed(1, null, null, 3);

        var placed = match.PlaceTeam(SeasonMatchSide.Home, 10);
        var again = match.PlaceTeam(SeasonMatchSide.Home, 10);

        Assert.Multiple(() =>
        {
            Assert.That((placed, again, match.HomeTeamID), Is.EqualTo((true, false, (int?)10)));
            Assert.Throws<InvalidOperationException>(() => match.PlaceTeam(SeasonMatchSide.Away, 10));
            Assert.Throws<InvalidOperationException>(() => SeasonMatch.Seed(1, null, null, 3).WithBye(SeasonMatchSide.Away).PlaceTeam(SeasonMatchSide.Away, 20));
        });
    }

    [Test]
    public void ABYE_TakesAnEmptySide_AndTheTeamFacingItWins()
    {
        var match = SeasonMatch.Seed(1, null, null, 3);

        var placed = match.PlaceBye(SeasonMatchSide.Away);
        var again = match.PlaceBye(SeasonMatchSide.Away);
        var waiting = (match.DecidedBy, match.Winner, match.IsDecided);
        Assert.Throws<InvalidOperationException>(() => match.AwardByeWin(), "No team facing it yet");
        match.PlaceTeam(SeasonMatchSide.Home, 10);
        var won = match.AwardByeWin();
        var wonAgain = match.AwardByeWin();

        Assert.Multiple(() =>
        {
            Assert.That((placed, again), Is.EqualTo((true, false)));
            Assert.That(waiting, Is.EqualTo(((SeasonMatchDecision?)SeasonMatchDecision.Bye, (SeasonMatchSide?)null, true)));
            Assert.That((won, wonAgain), Is.EqualTo((true, false)));
            Assert.That((match.Winner, match.WinnerTeamID, match.Loser), Is.EqualTo(((SeasonMatchSide?)SeasonMatchSide.Home, (int?)10, (SeasonMatchSide?)null)),
                "A BYE is no team: nobody lost");
            Assert.That(match.HasBye, Is.True);
        });
    }

    [Test]
    public void ABYE_IsRefused_OnASideWithATeam_OrInAMatchItsGamesOrAForfeitDecided()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Ready().PlaceBye(SeasonMatchSide.Home));
            Assert.Throws<InvalidOperationException>(() => SeasonMatch.Imported(1, null, 20, 2, 0, 3, null, null).PlaceBye(SeasonMatchSide.Home));
            Assert.Throws<InvalidOperationException>(() => SeasonMatch.Seed(1, null, null, 3).AwardByeWin(), "No BYE");
            Assert.Throws<InvalidOperationException>(() => SeasonMatch.Seed(1, null, null, 3).WithBye(SeasonMatchSide.Home).WithBye(SeasonMatchSide.Away).AwardByeWin(),
                "Two BYEs: no team to win");
            Assert.That(SeasonMatch.Seed(1, null, null, 3).WithBye(SeasonMatchSide.Home).WithBye(SeasonMatchSide.Away).DecidedBy, Is.EqualTo(SeasonMatchDecision.Bye));
        });
    }

    [Test]
    public void ClearSide_EmptiesASide_AndTakesBackTheWinItGave()
    {
        var byeWin = SeasonMatch.Seed(1, null, 20, 3).WonOnBye(SeasonMatchSide.Home);
        var waiting = SeasonMatch.Seed(1, 10, 20, 3);

        byeWin.ClearSide(SeasonMatchSide.Home);
        waiting.ClearSide(SeasonMatchSide.Away);

        Assert.Multiple(() =>
        {
            Assert.That((byeWin.HomeTeamResult, byeWin.AwayTeamResult, byeWin.AwayTeamID), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)20)));
            Assert.That((waiting.HomeTeamID, waiting.AwayTeamID), Is.EqualTo(((int?)10, (int?)null)));
            Assert.Throws<InvalidOperationException>(() => Ready().WonBy(SeasonMatchSide.Home).ClearSide(SeasonMatchSide.Away), "Undone first");
            Assert.Throws<InvalidOperationException>(() => Ready().Forfeited(Forfeiter.Home).ClearSide(SeasonMatchSide.Home));
        });
    }

    [Test]
    public void ScheduleAt_MovesIt()
    {
        var match = Ready();
        match.ScheduleAt(new DateTime(2030, 1, 1));
        var scheduled = match.ScheduledTime;
        match.ScheduleAt(null);

        Assert.That((scheduled, match.ScheduledTime), Is.EqualTo(((DateTime?)new DateTime(2030, 1, 1), (DateTime?)null)));
    }

    /// <summary>Every legal state the methods reach, and what the match says of it.</summary>
    private static IEnumerable<TestCaseData> States()
    {
        TestCaseData Case(string name, Func<SeasonMatch> make, SeasonMatchDecision? by, SeasonMatchSide? winner, SeasonMatchSide? loser)
            => new TestCaseData(make, by, winner, loser).SetName($"{{m}}({name})");
        yield return Case("waiting", () => SeasonMatch.Seed(1, null, null, 3), null, null, null);
        yield return Case("ready", () => Ready(), null, null, null);
        yield return Case("under way", () => Ready().Scored(1, 1), null, null, null);
        yield return Case("home won", () => Ready().WonBy(SeasonMatchSide.Home), SeasonMatchDecision.Games, SeasonMatchSide.Home, SeasonMatchSide.Away);
        yield return Case("away won", () => Ready().WonBy(SeasonMatchSide.Away), SeasonMatchDecision.Games, SeasonMatchSide.Away, SeasonMatchSide.Home);
        yield return Case("home forfeit", () => Ready().Forfeited(Forfeiter.Home), SeasonMatchDecision.Forfeit, SeasonMatchSide.Away, SeasonMatchSide.Home);
        yield return Case("away forfeit", () => Ready().Forfeited(Forfeiter.Away), SeasonMatchDecision.Forfeit, SeasonMatchSide.Home, SeasonMatchSide.Away);
        yield return Case("double forfeit", () => Ready().Forfeited(Forfeiter.Both), SeasonMatchDecision.Forfeit, null, null);
        yield return Case("BYE pending", () => SeasonMatch.Seed(1, null, null, 3).WithBye(SeasonMatchSide.Home), SeasonMatchDecision.Bye, null, null);
        yield return Case("two BYEs", () => SeasonMatch.Seed(1, null, null, 3).WithBye(SeasonMatchSide.Home).WithBye(SeasonMatchSide.Away), SeasonMatchDecision.Bye, null, null);
        yield return Case("BYE win", () => SeasonMatch.Seed(1, 10, null, 3).WonOnBye(SeasonMatchSide.Away), SeasonMatchDecision.Bye, SeasonMatchSide.Home, null);
    }

    [TestCaseSource(nameof(States))]
    public void EveryState_SaysWhatDecidedIt_WhoWon_AndWhoLost(Func<SeasonMatch> make, SeasonMatchDecision? by, SeasonMatchSide? winner, SeasonMatchSide? loser)
    {
        var match = make();

        Assert.Multiple(() =>
        {
            Assert.That(match.DecidedBy, Is.EqualTo(by));
            Assert.That(match.IsDecided, Is.EqualTo(by is not null));
            Assert.That(IsUndecided(match), Is.EqualTo(!match.IsDecided), "The query's rule is the loaded match's");
            Assert.That(match.Winner, Is.EqualTo(winner));
            Assert.That(match.Loser, Is.EqualTo(loser));
            Assert.That(match.WinnerTeamID, Is.EqualTo(winner is { } w ? match.TeamOf(w) : null));
            Assert.That(match.LoserTeamID, Is.EqualTo(loser is { } l ? match.TeamOf(l) : null));
        });
    }
}
