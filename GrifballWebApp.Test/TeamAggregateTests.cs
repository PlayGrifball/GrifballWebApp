using GrifballWebApp.Database;
using GrifballWebApp.Domain;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Teams;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;

namespace GrifballWebApp.Test;

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

        Assert.Multiple(() =>
        {
            Assert.That((team.SeasonID, team.TeamName, team.CaptainID, team.RecordedWins), Is.EqualTo((5, "Reds", (int?)null, (int?)null)));
            Assert.That(team.TeamPlayers.Select(p => (p.UserID, p.DraftCaptainOrder, p.DraftRound)), Is.EqualTo(new[] { (Cap, (int?)3, (int?)null) }));
            Assert.That(team.IsCaptainedBy(Cap), Is.False, "Captain only once set (after the roster is saved)");
            Assert.That(() => Team.Drafted(5, " ", Cap, 1), Throws.ArgumentException);
        });
    }

    [Test]
    public void Imported_IsNamed_WithNoRosterOrRecord()
    {
        var team = Team.Imported(7, "Relentless");

        Assert.Multiple(() =>
        {
            Assert.That((team.SeasonID, team.TeamName, team.RecordedWins, team.RecordedLosses), Is.EqualTo((7, "Relentless", (int?)null, (int?)null)));
            Assert.That(team.TeamPlayers, Is.Empty);
            Assert.That(() => Team.Imported(7, ""), Throws.ArgumentException);
        });
    }

    [Test]
    public void Rename_RefusesABlankOrTooLongName()
    {
        var team = Team.Imported(7, "Relentless");

        Assert.Multiple(() =>
        {
            Assert.That(team.Rename("  "), Is.EqualTo(TeamRenameResult.Blank));
            Assert.That(team.Rename(new string('x', Team.TeamNameColumnLength + 1)), Is.EqualTo(TeamRenameResult.TooLong));
            Assert.That(team.TeamName, Is.EqualTo("Relentless"), "Unchanged when refused");
            Assert.That(team.Rename("relentless"), Is.EqualTo(TeamRenameResult.Renamed));
            Assert.That(team.TeamName, Is.EqualTo("relentless"));
        });
    }

    [Test]
    public void SetCaptain_MustBeOnTheRoster_AndClearCaptain_KeepsThePlayers()
    {
        var team = DraftedTeam();

        Assert.Multiple(() =>
        {
            Assert.That(team.SetCaptain(Other), Is.EqualTo(TeamCaptainResult.NotOnRoster));
            Assert.That(team.IsCaptainedBy(Cap), Is.True, "Unchanged when refused");
            Assert.That(team.SetCaptain(P1), Is.EqualTo(TeamCaptainResult.Appointed));
            Assert.That((team.IsCaptainedBy(P1), team.Captain?.UserID), Is.EqualTo((true, (int?)P1)));
        });

        team.ClearCaptain();
        Assert.Multiple(() =>
        {
            Assert.That((team.Captain, team.CaptainID), Is.EqualTo(((TeamPlayer?)null, (int?)null)));
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(4));
            Assert.That(team.OrderCaptain(1), Is.EqualTo(TeamCaptainResult.NoCaptain));
        });
    }

    [Test]
    public void OrderCaptain_PlacesTheCaptain()
    {
        var team = DraftedTeam();

        Assert.That(team.OrderCaptain(4), Is.EqualTo(TeamCaptainResult.Appointed));
        Assert.That(team.PlayerOf(Cap)!.DraftCaptainOrder, Is.EqualTo(4));
    }

    [Test]
    public void Draft_AddsInTheNextRound_AndRefusesAPlayerOnTheTeam()
    {
        var team = DraftedTeam();

        Assert.Multiple(() =>
        {
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P1, (int?)1), (P2, (int?)2), (P3, (int?)3) }), "The captain is no pick");
            Assert.That(team.Draft(P2), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.Draft(Cap), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.AddPlayer(P1), Is.EqualTo(TeamRosterResult.AlreadyOnTeam));
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void AddPlayer_PutsAPlayerOnTheRoster_WithNoDraftRound()
    {
        var team = Team.Imported(7, "Relentless");

        Assert.That(team.AddPlayer(Other), Is.EqualTo(TeamRosterResult.Done));
        Assert.That(team.TeamPlayers.Select(p => (p.UserID, p.DraftRound)), Is.EqualTo(new[] { (Other, (int?)null) }));
    }

    [Test]
    public void RemovePlayer_TakesThemOff_TheCaptainToo_LeavingTheRounds()
    {
        var team = DraftedTeam();

        Assert.Multiple(() =>
        {
            Assert.That(team.RemovePlayer(Other), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(team.RemovePlayer(P1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P2, (int?)2), (P3, (int?)3) }), "The import's and a merge's removal leaves the rounds");
            Assert.That(team.RemovePlayer(Cap), Is.EqualTo(TeamRosterResult.Done));
            Assert.That((team.Captain, team.CaptainID), Is.EqualTo(((TeamPlayer?)null, (int?)null)), "No captain once they are gone");
        });
    }

    [Test]
    public void Undraft_MovesTheLaterPicksUp_AndRefusesTheCaptain()
    {
        var team = DraftedTeam();

        Assert.Multiple(() =>
        {
            Assert.That(team.Undraft(Other), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(team.Undraft(Cap), Is.EqualTo(TeamRosterResult.Captain));
            Assert.That(team.Undraft(P1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P2, (int?)1), (P3, (int?)2) }));
            Assert.That(team.IsCaptainedBy(Cap), Is.True);
        });
    }

    [Test]
    public void MovePick_Reorders_AndRefusesARoundPastThePicks()
    {
        var team = DraftedTeam();

        Assert.Multiple(() =>
        {
            Assert.That(team.MovePick(P3, 4), Is.EqualTo(TeamRosterResult.RoundOutOfRange));
            Assert.That(team.MovePick(P3, 0), Is.EqualTo(TeamRosterResult.RoundOutOfRange));
            Assert.That(team.MovePick(Cap, 1), Is.EqualTo(TeamRosterResult.Captain));
            Assert.That(team.MovePick(Other, 1), Is.EqualTo(TeamRosterResult.NotOnTeam));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P1, (int?)1), (P2, (int?)2), (P3, (int?)3) }), "Unchanged when refused");
            Assert.That(team.MovePick(P3, 1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(team), Is.EqualTo(new[] { (P3, (int?)1), (P1, (int?)2), (P2, (int?)3) }));
        });
    }

    [Test]
    public void TradePick_MovesTheRow_RenumberingBothTeams()
    {
        var from = DraftedTeam();
        var to = Team.Drafted(5, "Blues", 20, 1);
        to.SetCaptain(20);
        to.Draft(21);
        var traded = from.PlayerOf(P1);

        Assert.Multiple(() =>
        {
            Assert.That(from.TradePick(P1, to, 1), Is.EqualTo(TeamRosterResult.Done));
            Assert.That(Rounds(from), Is.EqualTo(new[] { (P2, (int?)1), (P3, (int?)2) }));
            Assert.That(Rounds(to), Is.EqualTo(new[] { (P1, (int?)1), (21, (int?)2) }));
            Assert.That(to.PlayerOf(P1), Is.SameAs(traded), "The same row, moved");
            Assert.That(traded!.Team, Is.SameAs(to));
        });
    }

    [Test]
    public void TradePick_RefusesWhatCantMove()
    {
        var from = DraftedTeam();
        var to = Team.Drafted(5, "Blues", 20, 1);
        to.SetCaptain(20);
        to.AddPlayer(P2);

        Assert.Multiple(() =>
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
        });
    }

    [Test]
    public void MergePlayer_ReassignsTheSourcesRow_OrKeepsTheTargets_WithTheCaptaincy()
    {
        var reassigned = DraftedTeam();
        var both = DraftedTeam();
        both.PlayerOf(P1)!.OrderedAsCaptain(null);
        var orderedTarget = DraftedTeam();
        orderedTarget.PlayerOf(P1)!.OrderedAsCaptain(7);

        Assert.Multiple(() =>
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
        });
    }

    [Test]
    public void MergePlayer_OfANonCaptain_OntoAPlayer_KeepsTheCaptain()
    {
        var team = DraftedTeam();

        Assert.That(team.MergePlayer(P2, P3), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(team.PlayerOf(P2), Is.Null);
            Assert.That(team.IsCaptainedBy(Cap), Is.True);
            Assert.That(team.TeamPlayers, Has.Count.EqualTo(3));
        });
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

        Assert.Multiple(() =>
        {
            Assert.That((row.TeamID, row.UserID, row.DraftRound, row.CaptainTeam), Is.EqualTo((4, 8, (int?)2, (Team?)null)));
            Assert.That(TeamPlayer.Seed(teamID: 5, userID: 6).TeamID, Is.EqualTo(5));
            Assert.That(team.PlayerSeasonStats, Is.Empty);
            Assert.That(team.PlayerChampionships, Is.Empty);
        });
    }

    [Test]
    public void Copy_CopiesTheRosterAndTimeslots_ReturningTheCaptainToSetLater()
    {
        var team = DraftedTeam();

        var copy = team.Copy(out var captain);
        var none = Team.Imported(7, "No captain").Copy(out var noCaptain);

        Assert.Multiple(() =>
        {
            Assert.That(copy.TeamName, Is.EqualTo("Reds"));
            Assert.That(copy.TeamPlayers.Select(p => (p.UserID, p.DraftRound, p.DraftCaptainOrder)), Is.EquivalentTo(team.TeamPlayers.Select(p => (p.UserID, p.DraftRound, p.DraftCaptainOrder))));
            Assert.That(copy.TeamPlayers.All(p => p.Team == copy), Is.True);
            Assert.That((copy.Captain, captain), Is.EqualTo(((TeamPlayer?)null, (int?)Cap)), "The captain is set once the copy is saved");
            Assert.That((none.TeamPlayers.Count, noCaptain), Is.EqualTo((0, (int?)null)));
        });
    }
}

/// <summary>What the database holds a team to, and its methods on saved teams (#318).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheDatabase_RefusesHalfARecord()
    {
        var season = await _context.AddSeason();
        // Past the private setters, as only something outside the class could (raw SQL, a bad migration)
        await using var ctx = _context.NewContextLike();
        var entry = ctx.Teams.Add(Team.Imported(season.SeasonID, "Relentless"));
        entry.Property(t => t.RecordedWins).CurrentValue = 9;

        await Assert.ThatAsync(() => ctx.SaveChangesAsync(), Throws.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task ACaptainedTeam_IsSavedInTwoSteps_AndFoundByItsCaptain()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        var team = Team.Drafted(season.SeasonID, "Reds", cap.Id, 1);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        Assert.That(team.SetCaptain(cap.Id), Is.EqualTo(TeamCaptainResult.Appointed));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var found = await _context.Teams.WithRoster().Where(Team.CaptainedBy(cap.Id)).SingleAsync();
        var withoutRoster = await _context.Teams.AsNoTracking().SingleAsync(t => t.TeamID == team.TeamID);
        Assert.Multiple(async () =>
        {
            Assert.That(found.TeamID, Is.EqualTo(team.TeamID));
            Assert.That(found.IsCaptainedBy(cap.Id), Is.True, "Through the roster's row CaptainID names");
            Assert.That(found.Captain!.CaptainTeam, Is.SameAs(found));
            Assert.That((withoutRoster.CaptainID, withoutRoster.IsCaptainedBy(cap.Id)), Is.EqualTo(((int?)found.CaptainID, false)), "Its captain's row not loaded: not known");
            Assert.That(await _context.Rosters().Select(p => p.UserID).ToListAsync(), Is.EqualTo(new[] { cap.Id }));
            Assert.That(_context.ChangeTracker.Entries<TeamPlayer>().Count(), Is.EqualTo(1), "Only the team's own, loaded with it, are tracked: the reads are not");
        });
    }

    [Test]
    public async Task TheRulesQueries_AgreeWithTheLoadedTeams()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        var pick = await _context.AddUser("pick");
        var other = await _context.AddUser("other");
        // A captained team with a pick, an archive's with its record, and one with a roster but no captain
        var drafted = Team.Drafted(season.SeasonID, "Reds", cap.Id, 1);
        var archived = Team.Imported(season.SeasonID, "Relentless");
        archived.RecordArchive(9, 0);
        var uncaptained = Team.Imported(season.SeasonID, "Blues");
        uncaptained.AddPlayer(other.Id);
        _context.Teams.AddRange(drafted, archived, uncaptained);
        await _context.SaveChangesAsync();
        drafted.SetCaptain(cap.Id);
        drafted.Draft(pick.Id);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var queried = await _context.Teams.AsNoTracking().OrderBy(t => t.TeamID)
            .Select(ModelRules.Inline((Team t) => new
            {
                t.TeamID,
                Archived = t.Apply(Team.HasArchivedRecord),
                Captained = t.Apply(Team.CaptainedBy(cap.Id)),
                Picks = t.Apply(Team.PicksOf).Select(p => p.UserID).ToList(),
            }))
            .ToListAsync();
        var loaded = await _context.Teams.WithRoster().OrderBy(t => t.TeamID).ToListAsync();

        var expected = new[]
        {
            (drafted.TeamID, false, true, $"{pick.Id}"),
            (archived.TeamID, true, false, ""),
            (uncaptained.TeamID, false, false, $"{other.Id}"),
        };
        Assert.Multiple(() =>
        {
            Assert.That(queried.Select(q => (q.TeamID, q.Archived, q.Captained, string.Join(",", q.Picks.Order()))), Is.EqualTo(expected), "The queries");
            Assert.That(loaded.Select(t => (t.TeamID, t.IsRecordArchived, t.IsCaptainedBy(cap.Id), string.Join(",", t.Picks.Select(p => p.UserID).Order()))),
                Is.EqualTo(expected), "The loaded teams");
        });
    }

    [Test]
    public async Task ATrade_MovesTheSavedRow_ToTheOtherTeam()
    {
        var season = await _context.AddSeason();
        var capA = await _context.AddUser("capA");
        var capB = await _context.AddUser("capB");
        var pick = await _context.AddUser("pick");
        var a = await _context.AddTeam(season, "A", capA, 1, pick);
        var b = await _context.AddTeam(season, "B", capB, 2);
        var rowID = a.PlayerOf(pick.Id)!.TeamPlayerID;

        Assert.That(a.TradePick(pick.Id, b, 1), Is.EqualTo(TeamRosterResult.Done));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var row = await _context.Rosters().SingleAsync(p => p.UserID == pick.Id);
        Assert.That((row.TeamPlayerID, row.TeamID, row.DraftRound), Is.EqualTo((rowID, b.TeamID, (int?)1)), "The same row, on the other team");
    }

    [Test]
    public async Task TheDraft_RefusesAMoveOrRemoval_ForACaptainWithNoTeam()
    {
        var season = await _context.AddSeason();
        var noTeam = await _context.AddUser("noTeam");
        var p1 = await _context.AddUser("p1");
        var service = new TeamService(_context, Substitute.For<IPublisher>(), Substitute.For<IOutbox>());

        var moved = await Assert.ThrowsAsync<TeamServiceException>(() =>
            service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = noTeam.Id, NewCaptainID = noTeam.Id, PersonID = p1.Id, RoundNumber = 1 }));
        var removed = await Assert.ThrowsAsync<TeamServiceException>(() =>
            service.RemovePlayerFromTeam(new RemovePlayerFromTeamRequestDto { SeasonID = season.SeasonID, CaptainID = noTeam.Id, PersonID = p1.Id }));
        Assert.That((moved!.Message, removed!.Message), Is.EqualTo(("Player is not on that team", "Player is not on this team")));
    }

    [Test]
    public async Task MovePlayerToTeam_RefusesARoundPastThePicks()
    {
        var season = await _context.AddSeason();
        var cap = await _context.AddUser("cap");
        var p1 = await _context.AddUser("p1");
        await _context.AddTeam(season, "A", cap, 1, p1);
        var service = new TeamService(_context, Substitute.For<IPublisher>(), Substitute.For<IOutbox>());

        var ex = await Assert.ThrowsAsync<TeamServiceException>(() =>
            service.MovePlayerToTeam(new MovePlayerToTeamRequestDto { SeasonID = season.SeasonID, PreviousCaptainID = cap.Id, NewCaptainID = cap.Id, PersonID = p1.Id, RoundNumber = 3 }));
        Assert.That(ex!.Message, Is.EqualTo("Round number is out of range"));
    }

    [Test]
    public async Task AMerge_OfACaptainOntoATeammate_MovesTheCaptaincy()
    {
        var season = await _context.AddSeason();
        var placeholder = await _context.AddUser("placeholder");
        var real = await _context.AddUser("real");
        var team = await _context.AddTeam(season, "A", placeholder, 3, real);
        var service = new UserMergeService(_context, TimeProvider.System);

        await service.Merge(real.Id, placeholder.Id, new MergeOptions(), CancellationToken.None);
        _context.ChangeTracker.Clear();

        var merged = await _context.Teams.WithRoster().SingleAsync(t => t.TeamID == team.TeamID);
        Assert.Multiple(() =>
        {
            Assert.That(merged.TeamPlayers.Select(p => p.UserID), Is.EqualTo(new[] { real.Id }), "The source's row goes");
            Assert.That(merged.IsCaptainedBy(real.Id), Is.True);
            Assert.That(merged.PlayerOf(real.Id)!.DraftCaptainOrder, Is.EqualTo(3));
        });
    }
}

/// <summary>
/// The TeamAggregate migration settles the teams its constraint (CK_Event_Teams_RecordBothOrNeither) would refuse before
/// adding it: half a record of the spreadsheet's gets its blank half as 0, as the standings already counted it. Alike on
/// whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamAggregateMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task HalfARecord_IsSettled_BeforeTheConstraint()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_TeamAggregate"))) - 1]);

        var season = await _context.AddSeason();
        // As the old model's public setters could leave them
        var winsOnly = Team.Seed(teamName: "Wins only", seasonID: season.SeasonID, recordedWins: 5);
        var lossesOnly = Team.Seed(teamName: "Losses only", seasonID: season.SeasonID, recordedLosses: 4);
        var whole = Team.Seed(teamName: "Whole", seasonID: season.SeasonID, recordedWins: 3, recordedLosses: 2);
        var none = Team.Seed(teamName: "None", seasonID: season.SeasonID);
        _context.Teams.AddRange(winsOnly, lossesOnly, whole, none);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var records = await _context.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamID, t => (t.RecordedWins, t.RecordedLosses));
        Assert.Multiple(() =>
        {
            Assert.That(records[winsOnly.TeamID], Is.EqualTo(((int?)5, (int?)0)), "The blank half is 0");
            Assert.That(records[lossesOnly.TeamID], Is.EqualTo(((int?)0, (int?)4)));
            Assert.That(records[whole.TeamID], Is.EqualTo(((int?)3, (int?)2)), "A whole record stays");
            Assert.That(records[none.TeamID], Is.EqualTo(((int?)null, (int?)null)), "No record stays none");
        });
    }
}
