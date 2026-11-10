using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NSubstitute;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Infrastructure.Teams;

namespace GrifballWebApp.Integration.Test;

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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(found.TeamID, Is.EqualTo(team.TeamID));
            Assert.That(found.IsCaptainedBy(cap.Id), Is.True, "Through the roster's row CaptainID names");
            Assert.That(found.Captain!.CaptainTeam, Is.SameAs(found));
            Assert.That((withoutRoster.CaptainID, withoutRoster.IsCaptainedBy(cap.Id)), Is.EqualTo(((int?)found.CaptainID, false)), "Its captain's row not loaded: not known");
            Assert.That(await _context.Teams.Rosters().Select(p => p.UserID).ToListAsync(), Is.EqualTo(new[] { cap.Id }));
            Assert.That(_context.ChangeTracker.Entries<TeamPlayer>().Count(), Is.EqualTo(1), "Only the team's own, loaded with it, are tracked: the reads are not");
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(queried.Select(q => (q.TeamID, q.Archived, q.Captained, string.Join(",", q.Picks.Order()))), Is.EqualTo(expected), "The queries");
            Assert.That(loaded.Select(t => (t.TeamID, t.IsRecordArchived, t.IsCaptainedBy(cap.Id), string.Join(",", t.Picks.Select(p => p.UserID).Order()))),
                Is.EqualTo(expected), "The loaded teams");
        }
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

        var row = await _context.Teams.Rosters().SingleAsync(p => p.UserID == pick.Id);
        Assert.That((row.TeamPlayerID, row.TeamID, row.DraftRound), Is.EqualTo((rowID, b.TeamID, (int?)1)), "The same row, on the other team");
    }

    [Test]
    public async Task TheDraft_RefusesAMoveOrRemoval_ForACaptainWithNoTeam()
    {
        var season = await _context.AddSeason();
        var noTeam = await _context.AddUser("noTeam");
        var p1 = await _context.AddUser("p1");
        var service = new TeamService(_context, Substitute.For<IPublisher>(), new OutboxDraftChannel(_context, Substitute.For<IOutbox>()), new ContextDatabaseLocks(_context));

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
        var service = new TeamService(_context, Substitute.For<IPublisher>(), new OutboxDraftChannel(_context, Substitute.For<IOutbox>()), new ContextDatabaseLocks(_context));

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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged.TeamPlayers.Select(p => p.UserID), Is.EqualTo(new[] { real.Id }), "The source's row goes");
            Assert.That(merged.IsCaptainedBy(real.Id), Is.True);
            Assert.That(merged.PlayerOf(real.Id)!.DraftCaptainOrder, Is.EqualTo(3));
        }
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
        using (Assert.EnterMultipleScope())
        {
            Assert.That(records[winsOnly.TeamID], Is.EqualTo(((int?)5, (int?)0)), "The blank half is 0");
            Assert.That(records[lossesOnly.TeamID], Is.EqualTo(((int?)0, (int?)4)));
            Assert.That(records[whole.TeamID], Is.EqualTo(((int?)3, (int?)2)), "A whole record stays");
            Assert.That(records[none.TeamID], Is.EqualTo(((int?)null, (int?)null)), "No record stays none");
        }
    }
}
