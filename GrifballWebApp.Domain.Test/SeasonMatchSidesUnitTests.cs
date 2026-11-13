using GrifballWebApp.Domain.League;
using static GrifballWebApp.Domain.Test.MatchRescheduleTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A season match's sides: a team placed on one, a side cleared, each side's score, and a reschedule request whose match
/// changed while it waited. No database, host, file or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchSidesUnitTests
{
    [Test]
    public void PlaceTeam_TakesTheAwaySide_OnceOnly_AndNeverAgainstItself()
    {
        var match = SeasonMatch.Seed(1, homeTeamID: 10, awayTeamID: null);

        var placed = match.PlaceTeam(SeasonMatchSide.Away, 20);
        var again = match.PlaceTeam(SeasonMatchSide.Away, 20);
        var itself = Assert.Throws<InvalidOperationException>(() => match.PlaceTeam(SeasonMatchSide.Away, 10))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((placed, again), Is.EqualTo((true, false)), "Changed once; placing it again changes nothing");
            Assert.That((match.HomeTeamID, match.AwayTeamID), Is.EqualTo(((int?)10, (int?)20)));
            Assert.That(itself.Message, Is.EqualTo("Team 10 can't play itself in season match 0"));
            Assert.That(match.HasBothTeams, Is.True);
        }
    }

    [Test]
    public void ClearSide_OfAByesSide_TakesBackTheByesWin()
    {
        var match = SeasonMatch.Seed(1, homeTeamID: 10, awayTeamID: null).WonOnBye(SeasonMatchSide.Away);
        var before = (match.HomeTeamResult, match.DecidedBy);

        match.ClearSide(SeasonMatchSide.Away);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchDecision?)SeasonMatchDecision.Bye)));
            Assert.That((match.HomeTeamResult, match.AwayTeamResult, match.AwayTeamID), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null, (int?)null)),
                "No BYE, so no win over it");
            Assert.That((match.HomeTeamID, match.IsDecided), Is.EqualTo(((int?)10, false)), "The home team stays, undecided");
        }
    }

    [Test]
    public void ScoreOf_IsEachSidesGamesWon()
    {
        var match = SeasonMatch.Seed(1, 10, 20, bestOf: 5).Scored(2, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.ScoreOf(SeasonMatchSide.Home), match.ScoreOf(SeasonMatchSide.Away)), Is.EqualTo(((int?)2, (int?)1)));
            Assert.That((match.HomeTeamScore, match.AwayTeamScore), Is.EqualTo(((int?)2, (int?)1)));
            Assert.That(SeasonMatch.Seed(1, 10, 20).ScoreOf(SeasonMatchSide.Away), Is.Null, "Not played yet");
        }
    }

    [Test]
    public void Seed_OfLoadedTeams_LinksTheMatchToEachTeamsSide()
    {
        var season = Season.Seed(seasonName: "Spring", seasonID: 4);
        var home = Team.Seed("Home", seasonID: 4, teamID: 10);
        var away = Team.Seed("Away", seasonID: 4, teamID: 20);

        var match = SeasonMatch.Seed(season, home, away, bestOf: 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.SeasonID, match.HomeTeamID, match.AwayTeamID, match.BestOf), Is.EqualTo((4, (int?)10, (int?)20, 3)));
            Assert.That((match.Season, match.HomeTeam, match.AwayTeam), Is.EqualTo((season, home, away)));
            Assert.That((home.HomeMatches, home.AwayMatches), Is.EqualTo((new[] { match }, Array.Empty<SeasonMatch>())));
            Assert.That((away.HomeMatches, away.AwayMatches), Is.EqualTo((Array.Empty<SeasonMatch>(), new[] { match })));
        }
    }

    [Test]
    public void Seed_OfUnsavedTeams_LeavesTheirKeysUnset()
    {
        var match = SeasonMatch.Seed(null, Team.Seed("New"), null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.SeasonID, match.HomeTeamID, match.AwayTeamID), Is.EqualTo((0, (int?)null, (int?)null)), "Set when the teams are saved");
            Assert.That(match.HomeTeam!.HomeMatches, Is.EqualTo(new[] { match }));
        }
    }

    [Test]
    public void ApprovingARequest_WhoseMatchLostATeamSinceIt_IsRefused_AndLeavesItPending()
    {
        var match = Match();
        var request = Pending(match);
        match.ClearSide(SeasonMatchSide.Away);

        var decision = request.Decide(true, 9, "ok", Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decision, Is.EqualTo(RescheduleDecision.MatchNotReschedulable));
            Assert.That((request.Status, request.ApprovedByUserID, request.ProcessedAt), Is.EqualTo((RescheduleStatus.Pending, (int?)null, (DateTime?)null)));
            Assert.That(match.ScheduledTime, Is.EqualTo(Scheduled), "The match keeps its time");
            Assert.That(request.Decide(false, 9, "no", Now), Is.EqualTo(RescheduleDecision.Rejected), "It can still be rejected");
        }
    }

    [Test]
    public void CloseBecauseDecided_LeavesARequestDecidedBefore_AsItWas()
    {
        var match = Match();
        var request = Pending(match);
        request.Decide(false, 9, "no", Now);

        request.CloseBecauseDecided(Now.AddDays(1));
        match.WonBy(SeasonMatchSide.Home);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((request.Status, request.ApprovedByUserID, request.CommissionerNotes), Is.EqualTo((RescheduleStatus.Rejected, (int?)9, "no")),
                "Not closed again as decided by the match");
            Assert.That(request.ProcessedAt, Is.EqualTo(Now), "Its own decision's time");
            Assert.That((match.IsDecided, match.PendingReschedule), Is.EqualTo((true, (MatchReschedule?)null)));
        }
    }
}
