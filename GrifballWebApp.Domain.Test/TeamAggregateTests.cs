using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A team is an aggregate (#318): it begins as the draft's or the archive's, and its roster, captain and record change
/// through its methods alone, which refuse the moves that can't happen.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TeamAggregateTests
{
    private const int Cap = 1, P1 = 2, P2 = 3, P3 = 4, Other = 9;

    /// <summary>A drafted team: its captain and picks in rounds 1, 2, 3.</summary>
    private static Team DraftedTeam()
    {
        var team = Team.Drafted(5, "Reds", Cap, 2);
        team.SetCaptain(Cap);
        foreach (var pick in new[] { P1, P2, P3 })
            team.Draft(pick);
        return team;
    }

    private static IEnumerable<(int UserID, int? Round)> Rounds(Team team) => team.Picks.Select(p => (p.UserID, p.DraftRound));

    [Test]
    public void Drafted_IsTheCaptainAlone_PlacedAmongTheCaptains()
    {
        var team = Team.Drafted(5, "Reds", Cap, 3);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((team.SeasonID, team.TeamName, team.CaptainID, team.RecordedWins), Is.EqualTo((5, "Reds", (int?)null, (int?)null)));
            Assert.That(team.TeamPlayers.Select(p => (p.UserID, p.DraftCaptainOrder, p.DraftRound)), Is.EqualTo(new[] { (Cap, (int?)3, (int?)null) }));
            Assert.That(team.IsCaptainedBy(Cap), Is.False, "Captain only once set (after the roster is saved)");
            Assert.That(() => Team.Drafted(5, " ", Cap, 1), Throws.ArgumentException);
        }
    }

    [Test]
    public void Imported_IsNamed_WithNoRosterOrRecord()
    {
        var team = Team.Imported(7, "Relentless");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((team.SeasonID, team.TeamName, team.RecordedWins, team.RecordedLosses), Is.EqualTo((7, "Relentless", (int?)null, (int?)null)));
            Assert.That(team.TeamPlayers, Is.Empty);
            Assert.That(() => Team.Imported(7, ""), Throws.ArgumentException);
        }
    }

    [Test]
    public void Rename_RefusesABlankOrTooLongName()
    {
        var team = Team.Imported(7, "Relentless");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.Rename("  "), Is.EqualTo(TeamRenameResult.Blank));
            Assert.That(team.Rename(new string('x', Team.TeamNameColumnLength + 1)), Is.EqualTo(TeamRenameResult.TooLong));
            Assert.That(team.TeamName, Is.EqualTo("Relentless"), "Unchanged when refused");
            Assert.That(team.Rename("relentless"), Is.EqualTo(TeamRenameResult.Renamed));
            Assert.That(team.TeamName, Is.EqualTo("relentless"));
        }
    }

    [Test]
    public void SetCaptain_MustBeOnTheRoster_AndClearCaptain_KeepsThePlayers()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.SetCaptain(Other), Is.EqualTo(TeamCaptainResult.NotOnRoster));
            Assert.That(team.IsCaptainedBy(Cap), Is.True, "Unchanged when refused");
            Assert.That(team.SetCaptain(P1), Is.EqualTo(TeamCaptainResult.Appointed));
            Assert.That((team.IsCaptainedBy(P1), team.Captain?.UserID), Is.EqualTo((true, (int?)P1)));
        }

        team.ClearCaptain();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((team.Captain, team.CaptainID), Is.EqualTo(((TeamPlayer?)null, (int?)null)));
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(4));
            Assert.That(team.OrderCaptain(1), Is.EqualTo(TeamCaptainResult.NoCaptain));
        }
    }

    [Test]
    public void OrderCaptain_PlacesTheCaptain()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.OrderCaptain(4), Is.EqualTo(TeamCaptainResult.Appointed));
            Assert.That(team.PlayerOf(Cap)!.DraftCaptainOrder, Is.EqualTo(4));
        }
    }

    [Test]
    public void Draft_AddsInTheNextRound_AndRefusesAPlayerOnTheTeam()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P1, (int?)1), (P2, (int?)2), (P3, (int?)3) }), "The captain is no pick");
            Assert.That(team.Draft(P2), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.Draft(Cap), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.AddPlayer(P1), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(4));
        }
    }

    [Test]
    public void AddPlayer_PutsAPlayerOnTheRoster_WithNoDraftRound()
    {
        var team = Team.Imported(7, "Relentless");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.AddPlayer(Other), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(team.TeamPlayers.Select(p => (p.UserID, p.DraftRound)), Is.EqualTo(new[] { (Other, (int?)null) }));
        }
    }

    [Test]
    public void RemovePlayer_TakesThemOff_TheCaptainToo_LeavingTheRounds()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.RemovePlayer(Other), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(team.RemovePlayer(P1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P2, (int?)2), (P3, (int?)3) }), "The import's and a merge's removal leaves the rounds");
            Assert.That(team.RemovePlayer(Cap), Is.EqualTo(TeamRosterResult.Done));
            Assert.That((team.Captain, team.CaptainID), Is.EqualTo(((TeamPlayer?)null, (int?)null)), "No captain once they are gone");
        }
    }

    [Test]
    public void Undraft_MovesTheLaterPicksUp_AndRefusesTheCaptain()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.Undraft(Other), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(team.Undraft(Cap), Is.EqualTo(TeamRosterResult.Captain));
            Assert.That(team.Undraft(P1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P2, (int?)1), (P3, (int?)2) }));
            Assert.That(team.IsCaptainedBy(Cap), Is.True);
        }
    }

    [Test]
    public void MovePick_Reorders_AndRefusesARoundPastThePicks()
    {
        var team = DraftedTeam();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.MovePick(P3, 4), Is.EqualTo(TeamRosterResult.RoundOutOfRange));
            Assert.That(team.MovePick(P3, 0), Is.EqualTo(TeamRosterResult.RoundOutOfRange));
            Assert.That(team.MovePick(Cap, 1), Is.EqualTo(TeamRosterResult.Captain));
            Assert.That(team.MovePick(Other, 1), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P1, (int?)1), (P2, (int?)2), (P3, (int?)3) }), "Unchanged when refused");
            Assert.That(team.MovePick(P3, 1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P3, (int?)1), (P1, (int?)2), (P2, (int?)3) }));
        }
    }

    [Test]
    public void TradePick_MovesTheRow_RenumberingBothTeams()
    {
        var from = DraftedTeam();
        var to = Team.Drafted(5, "Blues", 20, 1);
        to.SetCaptain(20);
        to.Draft(21);
        var traded = from.PlayerOf(P1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(from.TradePick(P1, to, 1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(from), Is.EqualTo(new[] { (P2, (int?)1), (P3, (int?)2) }));
            Assert.That(Rounds(to), Is.EqualTo(new[] { (P1, (int?)1), (21, (int?)2) }));
            Assert.That(to.PlayerOf(P1), Is.SameAs(traded), "The same row, moved");
            Assert.That(traded!.Team, Is.SameAs(to));
        }
    }

    [Test]
    public void TradePick_RefusesWhatCantMove()
    {
        var from = DraftedTeam();
        var to = Team.Drafted(5, "Blues", 20, 1);
        to.SetCaptain(20);
        to.AddPlayer(P2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(from.TradePick(Other, to, 1), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(from.TradePick(Cap, to, 1), Is.EqualTo(TeamRosterResult.Captain));
            Assert.That(from.TradePick(P2, to, 1), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(from.TradePick(P1, to, 3), Is.EqualTo(TeamRosterResult.RoundOutOfRange), "Past its one pick and the new one");
            Assert.That(from.TradePick(P1, to, 0), Is.EqualTo(TeamRosterResult.RoundOutOfRange), "Before the first");
            Assert.That(from.TradePick(P1, from, 3), Is.EqualTo(TeamRosterResult.Done), "To itself: a move within the team");
            Assert.That(Rounds(from), Is.EqualTo(new[] { (P2, (int?)1), (P3, (int?)2), (P1, (int?)3) }));
            Assert.That(from.TeamPlayers, Has.Count.EqualTo(4));
            Assert.That(() => from.TradePick(P1, null!, 1), Throws.ArgumentNullException);
        }
    }

    [Test]
    public void MergePlayer_ReassignsTheSourcesRow_OrKeepsTheTargets_WithTheCaptaincy()
    {
        var reassigned = DraftedTeam();
        var both = DraftedTeam();
        both.PlayerOf(P1)!.OrderedAsCaptain(null);
        var orderedTarget = DraftedTeam();
        orderedTarget.PlayerOf(P1)!.OrderedAsCaptain(7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reassigned.MergePlayer(Other, P1), Is.False, "The source isn't on it");
            Assert.That(reassigned.MergePlayer(P2, Other), Is.True);
            Assert.That(reassigned.PlayerOf(Other)!.DraftRound, Is.EqualTo(2), "The source's row, the target's now");
            Assert.That(reassigned.PlayerOf(P2), Is.Null);

            Assert.That(both.MergePlayer(Cap, P1), Is.True);
            Assert.That(both.PlayerOf(Cap), Is.Null, "The source's row goes");
            Assert.That((both.IsCaptainedBy(P1), both.PlayerOf(P1)!.DraftCaptainOrder), Is.EqualTo((true, (int?)2)), "The target captains, in the source's place");

            Assert.That(orderedTarget.MergePlayer(Cap, P1), Is.True);
            Assert.That(orderedTarget.PlayerOf(P1)!.DraftCaptainOrder, Is.EqualTo(7), "A captain order of its own is kept");

            Assert.That(DraftedTeam().MergePlayer(P2, P3), Is.True);
        }
    }

    [Test]
    public void MergePlayer_OfANonCaptain_OntoAPlayer_KeepsTheCaptain()
    {
        var team = DraftedTeam();

        Assert.That(team.MergePlayer(P2, P3), Is.True);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(team.PlayerOf(P2), Is.Null);
            Assert.That(team.IsCaptainedBy(Cap), Is.True);
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void RecordArchive_SetsBothHalves_AndClearArchivedRecord_Neither()
    {
        var team = Team.Imported(7, "Relentless");

        team.RecordArchive(9, 1);
        Assert.That((team.RecordedWins, team.RecordedLosses), Is.EqualTo(((int?)9, (int?)1)));
        team.ClearArchivedRecord();
        Assert.That((team.RecordedWins, team.RecordedLosses), Is.EqualTo(((int?)null, (int?)null)));
    }

    [Test]
    public void CaptainedBy_TheQuery_AgreesWithIsCaptainedBy()
    {
        var teams = new[] { DraftedTeam(), Team.Imported(7, "No captain") };
        var rule = Team.CaptainedBy(Cap).Compile();

        Assert.That(teams.Select(t => t.Captain is not null && rule(t)), Is.EqualTo(teams.Select(t => t.IsCaptainedBy(Cap))));
    }

    [Test]
    public void Seed_TakesTheKeysOfSavedEntities_AndAFreshTeamHasNoStatsOrTitles()
    {
        var team = Team.Seed(teamID: 4, teamName: "Seeded");
        var user = User.Seed(userName: "u", id: 8);
        var row = TeamPlayer.Seed(team: team, user: user, draftRound: 2);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((row.TeamID, row.UserID, row.DraftRound, row.CaptainTeam), Is.EqualTo((4, 8, (int?)2, (Team?)null)));
            Assert.That(TeamPlayer.Seed(teamID: 5, userID: 6).TeamID, Is.EqualTo(5));
            Assert.That(team.PlayerSeasonStats, Is.Empty);
            Assert.That(team.PlayerChampionships, Is.Empty);
        }
    }

    [Test]
    public void Copy_CopiesTheRosterAndTimeslots_ReturningTheCaptainToSetLater()
    {
        var team = DraftedTeam();

        var copy = team.Copy(out var captain);
        var none = Team.Imported(7, "No captain").Copy(out var noCaptain);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(copy.TeamName, Is.EqualTo("Reds"));
            Assert.That(copy.TeamPlayers.Select(p => (p.UserID, p.DraftRound, p.DraftCaptainOrder)), Is.EquivalentTo(team.TeamPlayers.Select(p => (p.UserID, p.DraftRound, p.DraftCaptainOrder))));
            Assert.That(copy.TeamPlayers.All(p => p.Team == copy), Is.True);
            Assert.That((copy.Captain, captain), Is.EqualTo(((TeamPlayer?)null, (int?)Cap)), "The captain is set once the copy is saved");
            Assert.That((none.TeamPlayers.Count, noCaptain), Is.EqualTo((0, (int?)null)));
        }
    }
}
