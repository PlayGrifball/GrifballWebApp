using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace GrifballWebApp.Test;

/// <summary>Postgres's row history (RowHistory, RowHistoryInterceptor). SQL Server keeps its own, in temporal tables.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RowHistoryTests
{
    private GrifballContext? _context;
    private GrifballContext Context => _context!;

    [OneTimeSetUp]
    public static void PostgresOnly()
    {
        if (TestDatabase.Provider != DatabaseProvider.Postgres)
            Assert.Ignore("Only Postgres keeps row history in the app");
    }

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private async Task<Team> NewTeam(string name = "Alpha", int players = 0)
    {
        var team = new Team { TeamName = name, Season = new Season { SeasonName = "Season" } };
        for (var i = 0; i < players; i++)
            team.TeamPlayers.Add(new TeamPlayer { User = new User { UserName = $"{name}{i}", DisplayName = $"{name}{i}" } });
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        return team;
    }

    private DateTime PeriodStart(object entity) => (DateTime)Context.Entry(entity).Property(RowHistory.PeriodStart).CurrentValue!;

    /// <summary>The history of <typeparamref name="TEntity"/>, oldest first, read afresh.</summary>
    private async Task<List<Dictionary<string, object>>> History<TEntity>() where TEntity : class
    {
        await using var read = Context.NewContextLike();
        var rows = await read.HistoryOf<TEntity>().ToListAsync();
        return rows.OrderBy(r => Valid(r).LowerBound).ToList();
    }

    private static NpgsqlRange<DateTime> Valid(Dictionary<string, object> row) => (NpgsqlRange<DateTime>)row[RowHistory.Valid];

    private static NpgsqlRange<DateTime> Range(DateTime from, DateTime to) => new(from, true, false, to, false, false);

    private static void AssertExclusionViolation(Exception ex)
    {
        Assert.That(ex.InnerException, Is.InstanceOf<PostgresException>());
        Assert.That(((PostgresException)ex.InnerException!).SqlState, Is.EqualTo(PostgresErrorCodes.ExclusionViolation));
    }

    [Test]
    public async Task Insert_BeginsTheRowsVersion_AndRecordsNothing()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var team = await NewTeam();

        Assert.Multiple(async () =>
        {
            Assert.That(PeriodStart(team), Is.InRange(before, DateTime.UtcNow));
            Assert.That(await History<Team>(), Is.Empty);
        });
    }

    [Test]
    public async Task Update_RecordsTheOldVersion_UntilTheNewOneBegins()
    {
        var team = await NewTeam("Alpha");
        var created = PeriodStart(team);

        team.TeamName = "Bravo";
        await Context.SaveChangesAsync();
        var updated = PeriodStart(team);

        var history = await History<Team>();
        await using var read = Context.NewContextLike();
        var stored = await read.Teams.Select(t => EF.Property<DateTime>(t, RowHistory.PeriodStart)).SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.GreaterThan(created));
            Assert.That(stored, Is.EqualTo(updated));
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0]["TeamName"], Is.EqualTo("Alpha"));
            Assert.That(history[0]["TeamID"], Is.EqualTo(team.TeamID));
            Assert.That(history[0]["SeasonID"], Is.EqualTo(team.SeasonID));
            Assert.That(Valid(history[0]), Is.EqualTo(Range(created, updated)));
        });
    }

    [Test]
    public async Task Delete_RecordsTheLastVersion()
    {
        var team = await NewTeam("Alpha");
        var created = PeriodStart(team);

        Context.Teams.Remove(team);
        await Context.SaveChangesAsync();

        var history = await History<Team>();
        Assert.Multiple(() =>
        {
            Assert.That(history.Single()["TeamName"], Is.EqualTo("Alpha"));
            Assert.That(Valid(history.Single()).LowerBound, Is.EqualTo(created));
            Assert.That(Valid(history.Single()).UpperBound, Is.GreaterThan(created).And.LessThanOrEqualTo(DateTime.UtcNow));
        });
    }

    [Test]
    public async Task SeveralUpdates_InOneTransaction_RecordEachVersion_EndToEnd()
    {
        var team = await NewTeam("Alpha");
        var created = PeriodStart(team);

        await Context.StartTransactionAsync();
        foreach (var name in new[] { "Bravo", "Charlie", "Delta" })
        {
            team.TeamName = name;
            await Context.SaveChangesAsync();
        }
        await Context.CommitTransactionAsync();

        var history = await History<Team>();
        Assert.Multiple(() =>
        {
            Assert.That(history.Select(h => h["TeamName"]), Is.EqualTo(new[] { "Alpha", "Bravo", "Charlie" }));
            Assert.That(Valid(history[0]).LowerBound, Is.EqualTo(created));
            Assert.That(Valid(history[1]).LowerBound, Is.EqualTo(Valid(history[0]).UpperBound));
            Assert.That(Valid(history[2]).LowerBound, Is.EqualTo(Valid(history[1]).UpperBound));
            Assert.That(Valid(history[2]).UpperBound, Is.EqualTo(PeriodStart(team)));
        });
    }

    [Test]
    public async Task RolledBackTransaction_RecordsNothing()
    {
        var team = await NewTeam("Alpha");

        await Context.StartTransactionAsync();
        team.TeamName = "Bravo";
        await Context.SaveChangesAsync();
        await Context.RollbackTransactionAsync();

        Assert.That(await History<Team>(), Is.Empty);
    }

    [Test]
    public async Task FailedSave_RecordsNothing()
    {
        var team = await NewTeam("Alpha");

        team.TeamName = new string('x', 31); // varchar(30)
        Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync());

        Assert.That(await History<Team>(), Is.Empty);
    }

    // A version that began after now, by a clock ahead of this one, still ends after it began.
    [Test]
    public async Task Update_OfAVersionFromTheFuture_RecordsAMicrosecond()
    {
        var team = await NewTeam("Alpha");
        var future = new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        await Context.Teams.Where(t => t.TeamID == team.TeamID).ExecuteUpdateAsync(s => s.SetProperty(t => EF.Property<DateTime>(t, RowHistory.PeriodStart), future));

        await using var other = Context.NewContextLike();
        var loaded = await other.Teams.SingleAsync();
        loaded.TeamName = "Bravo";
        await other.SaveChangesAsync();

        var history = await History<Team>();
        Assert.Multiple(() =>
        {
            Assert.That(Valid(history.Single()), Is.EqualTo(Range(future, future.AddTicks(10))));
            Assert.That(other.Entry(loaded).Property<DateTime>(RowHistory.PeriodStart).CurrentValue, Is.EqualTo(future.AddTicks(10)));
        });
    }

    [Test]
    public async Task Delete_RecordsWhatTheDatabaseCascadesTo_Unloaded()
    {
        var team = await NewTeam("Alpha", players: 2);
        var userIDs = team.TeamPlayers.Select(p => p.UserID).Order().ToList();

        await using var other = Context.NewContextLike();
        other.Teams.Remove(await other.Teams.SingleAsync());
        await other.SaveChangesAsync();

        var teams = await History<Team>();
        var players = await History<TeamPlayer>();
        Assert.Multiple(async () =>
        {
            Assert.That(await other.TeamPlayers.CountAsync(), Is.Zero);
            Assert.That(teams, Has.Count.EqualTo(1));
            Assert.That(players.Select(p => (int)p["UserID"]).Order(), Is.EqualTo(userIDs));
            Assert.That(players.Select(p => Valid(p).UpperBound), Has.All.EqualTo(Valid(teams[0]).UpperBound));
            Assert.That(await History<User>(), Is.Empty);
        });
    }

    [Test]
    public async Task Delete_RecordsWhatTheDatabaseCascadesTo_AtEveryLevel()
    {
        var team = await NewTeam("Alpha", players: 2);

        await using var other = Context.NewContextLike();
        other.Seasons.Remove(await other.Seasons.SingleAsync());
        await other.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await History<Season>(), Has.Count.EqualTo(1));
            Assert.That((await History<Team>()).Single()["TeamName"], Is.EqualTo("Alpha"));
            Assert.That(await History<TeamPlayer>(), Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task Delete_OfLoadedDependents_RecordsEachOnce()
    {
        await NewTeam("Alpha", players: 2);

        await using var other = Context.NewContextLike();
        other.Teams.Remove(await other.Teams.Include(t => t.TeamPlayers).SingleAsync());
        await other.SaveChangesAsync();

        Assert.That(await History<TeamPlayer>(), Has.Count.EqualTo(2));
    }

    [Test]
    public async Task TwoDeletes_CascadingToOneRow_RecordItOnce()
    {
        var team = await NewTeam("Alpha", players: 1);
        var userID = team.TeamPlayers.Single().UserID;

        await using var other = Context.NewContextLike();
        other.Teams.Remove(await other.Teams.SingleAsync());
        other.Users.Remove(await other.Users.SingleAsync(u => u.Id == userID));
        await other.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await History<TeamPlayer>(), Has.Count.EqualTo(1));
            Assert.That(await History<User>(), Has.Count.EqualTo(1));
        });
    }

    // An entity attached rather than loaded has only the values it was given: the database has the
    // rest. Synchronous SaveChanges, too.
    [Test]
    public async Task Delete_OfAnAttachedEntity_RecordsTheDatabasesValues()
    {
        var team = await NewTeam("Alpha", players: 1);
        var created = PeriodStart(team);

        await using var other = Context.NewContextLike();
        other.Teams.Remove(new Team { TeamID = team.TeamID });
        other.SaveChanges();

        var history = (await History<Team>()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That(history["TeamName"], Is.EqualTo("Alpha"));
            Assert.That(history["SeasonID"], Is.EqualTo(team.SeasonID));
            Assert.That(Valid(history).LowerBound, Is.EqualTo(created));
            Assert.That(await History<TeamPlayer>(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Delete_OfARowThatIsntThere_FailsAsBefore_RecordingNothing()
    {
        await using var other = Context.NewContextLike();
        other.Teams.Remove(new Team { TeamID = 12345 });

        Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        Assert.That(await History<Team>(), Is.Empty);
    }

    private static UserManager<User> UserManager(GrifballContext context)
    {
        var store = new UserStore<User, Role, GrifballContext, int, UserClaim, UserRole, UserLogin, IdentityUserToken<int>, IdentityRoleClaim<int>>(context);
        return new UserManager<User>(store, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    // UserStore.UpdateAsync attaches the user and marks it all changed: for a user it didn't load,
    // the "original" values are the new ones.
    [Test]
    public async Task UserManagerUpdate_OfAUserFromElsewhere_RecordsTheOldValues()
    {
        var user = new User { UserName = "someone", DisplayName = "Old" };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        var created = PeriodStart(user);

        await using var other = Context.NewContextLike();
        var detached = await other.Users.AsNoTracking().SingleAsync();
        detached.DisplayName = "New";
        var result = await UserManager(other).UpdateAsync(detached);

        var history = (await History<User>()).Single();
        Assert.Multiple(async () =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(history["DisplayName"], Is.EqualTo("Old"));
            Assert.That(history["ConcurrencyStamp"], Is.EqualTo(user.ConcurrencyStamp));
            Assert.That(Valid(history).LowerBound, Is.EqualTo(created));
            Assert.That(await other.Users.Select(u => u.DisplayName).SingleAsync(), Is.EqualTo("New"));
        });
    }

    [Test]
    public async Task UserManagerUpdate_OfAUserItLoaded_RecordsTheOldValues()
    {
        var user = new User { UserName = "someone", DisplayName = "Old" };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        await using var other = Context.NewContextLike();
        var manager = UserManager(other);
        var loaded = (await manager.FindByIdAsync(user.Id.ToString()))!;
        loaded.DisplayName = "New";
        await manager.UpdateAsync(loaded);

        Assert.That((await History<User>()).Single()["DisplayName"], Is.EqualTo("Old"));
    }

    // Two saves of the same version: the second would record it again, overlapping the first's
    // record, which the constraint rejects; it saves nothing.
    [Test]
    public async Task ConcurrentUpdates_TheSecondIsRejected()
    {
        await NewTeam("Alpha");
        await using var first = Context.NewContextLike();
        await using var second = Context.NewContextLike();
        var firstTeam = await first.Teams.SingleAsync();
        var secondTeam = await second.Teams.SingleAsync();

        firstTeam.TeamName = "First";
        await first.SaveChangesAsync();
        secondTeam.TeamName = "Second";
        var ex = Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        AssertExclusionViolation(ex!);
        await using var read = Context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(await read.Teams.Select(t => t.TeamName).SingleAsync(), Is.EqualTo("First"));
            Assert.That((await History<Team>()).Single()["TeamName"], Is.EqualTo("Alpha"));
        });
    }

    [Test]
    public async Task HistoryTable_RejectsOverlappingVersions()
    {
        var team = await NewTeam("Alpha");
        var created = PeriodStart(team);
        team.TeamName = "Bravo";
        await Context.SaveChangesAsync();

        await using var other = Context.NewContextLike();
        other.Set<Dictionary<string, object>>(RowHistory.HistoryName("Teams")).Add(new Dictionary<string, object>
        {
            ["TeamID"] = team.TeamID,
            ["TeamName"] = "Overlapping",
            [RowHistory.Valid] = Range(created.AddTicks(-10), created.AddTicks(10)),
        });
        var ex = Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());

        AssertExclusionViolation(ex!);
    }

    [Test]
    public async Task EveryHistoryTable_HasItsConstraint()
    {
        var expected = Context.Model.GetEntityTypes()
            .Select(e => e.FindHistory())
            .OfType<Microsoft.EntityFrameworkCore.Metadata.IEntityType>()
            .Select(h => $"{h.GetSchema()}.{h.GetTableName()}.{RowHistory.ConstraintName(h.GetTableName()!)}")
            .Order()
            .ToList();

        // Unique, and WITHOUT OVERLAPS (conperiod), on (key, Valid).
        var constraints = await Context.Database.SqlQueryRaw<string>(@"
            SELECT n.nspname || '.' || c.relname || '.' || con.conname AS ""Value""
            FROM pg_constraint con
            JOIN pg_class c ON c.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE con.contype = 'u' AND con.conperiod
              AND (SELECT attname FROM pg_attribute WHERE attrelid = c.oid AND attnum = con.conkey[array_length(con.conkey, 1)]) = 'Valid'")
            .ToListAsync();

        Assert.That(expected, Has.Count.EqualTo(40));
        Assert.That(constraints.Order(), Is.EqualTo(expected));
    }

    [Test]
    public void AsOfAndHistoryOf_ATableWithNoHistory_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Context.AsOf<MatchReschedule>(DateTime.UtcNow));
            Assert.Throws<InvalidOperationException>(() => Context.HistoryOf<MatchReschedule>());
        });
    }
}
