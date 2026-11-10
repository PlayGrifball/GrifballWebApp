using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>The database refuses a signup no method makes (CK_Event_SeasonSignups_*).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSignupAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ASignupNoMethodMakes_IsRefused()
    {
        var season = await _context.AddSeason();
        var users = new List<User>();
        for (var i = 0; i < 8; i++)
            users.Add(await _context.AddUser($"signer{i}"));
        var next = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(string? teamName, bool willCaptain, bool assistance)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.SeasonSignups.Add(SeasonSignup.Seed(season.SeasonID, users[next++].Id));
            entry.Property(s => s.TeamName).CurrentValue = teamName;
            entry.Property(s => s.WillCaptain).CurrentValue = willCaptain;
            entry.Property(s => s.RequiresAssistanceDrafting).CurrentValue = assistance;
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused("", true, false), Is.True, "A blank team name");
            Assert.That(await Refused("   ", true, false), Is.True, "A team name of spaces");
            Assert.That(await Refused("  Hammers", true, false), Is.True, "A team name not trimmed");
            Assert.That(await Refused(null, false, true), Is.True, "Help drafting for a player who won't captain");

            Assert.That(await Refused(null, false, false), Is.False);
            Assert.That(await Refused("Hammers", false, false), Is.False, "A name kept by a player who no longer captains");
            Assert.That(await Refused("Hammers", true, true), Is.False);
        }
    }

    [Test]
    public async Task AChangedSignup_DropsTheTimeslotsNoLongerChosen()
    {
        var season = await SignupTestData.OpenSeason(_context);
        var options = await SignupTestData.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(1, 17)), (DayOfWeek.Tuesday, new TimeOnly(2, 17)));
        var user = await _context.AddUser("signer");
        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timeslots: [options[0].AvailabilityOptionID, options[1].AvailabilityOptionID]);
        _context.SeasonSignups.Add(signup);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.Seasons.Include(s => s.SeasonAvailability).SingleAsync(s => s.SeasonID == season.SeasonID);
        var saved = await _context.SeasonSignups.Include(s => s.SignupAvailability).SingleAsync();
        var outcome = saved.Change(loaded, DateTime.UtcNow,
            new SignupChoices(null, false, false, [options[1].AvailabilityOptionID]));
        await _context.SaveChangesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome.Result, Is.EqualTo(SignupResult.Changed));
            Assert.That(await _context.Set<SignupAvailability>().Select(a => a.AvailabilityOptionID).ToListAsync(), Is.EqualTo(new[] { options[1].AvailabilityOptionID }));
        }
    }
}

/// <summary>
/// The SeasonSignupAggregate migration settles the signups its constraints would refuse before adding them: a team name is
/// trimmed (a blank one goes), and a player who won't captain asks no help drafting. Alike on whichever database the suite
/// runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSignupAggregateMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private static string SeasonSignups => TestDatabase.Provider == DatabaseProvider.Sqlite ? "\"SeasonSignups\"" : "\"Event\".\"SeasonSignups\"";

    [Test]
    public async Task SignupsTheConstraintsWouldRefuse_AreSettled_BeforeThem()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_SeasonSignupAggregate"))) - 1]);

        var season = await _context.AddSeason();
        var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // As the old model's public setters could leave them, in SQL: the old schema has a column the model no longer has (Approved)
        async Task<int> Insert(string name, string? teamName, bool willCaptain, bool assistance)
        {
            var user = await _context.AddUser(name);
            object[] args = teamName is null ? [user.Id, season.SeasonID, at, willCaptain, assistance] : [user.Id, season.SeasonID, at, willCaptain, assistance, teamName];
            // The table's name and the NULL are the test's own text; every value is a parameter
            var insert =
                $"INSERT INTO {SeasonSignups} (\"UserID\", \"SeasonID\", \"Timestamp\", \"TeamName\", \"WillCaptain\", \"RequiresAssistanceDrafting\", \"Approved\", \"CreatedAt\", \"ModifiedAt\") " +
                // A null name written as NULL: a raw parameter needs a type, which DBNull has none of
                $"VALUES ({{0}}, {{1}}, {{2}}, {(teamName is null ? "NULL" : "{5}")}, {{3}}, {{4}}, {{3}}, {{2}}, {{2}})";
            await _context.Database.ExecuteSqlRawAsync(insert, args);
            return user.Id;
        }
        var padded = await Insert("padded", "  Hammers  ", willCaptain: true, assistance: true);
        var spaces = await Insert("spaces", "   ", willCaptain: false, assistance: true);
        var empty = await Insert("empty", "", willCaptain: true, assistance: false);
        var none = await Insert("none", null, willCaptain: false, assistance: false);
        var kept = await Insert("kept", "Anvils", willCaptain: false, assistance: false);

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var signups = await _context.SeasonSignups.AsNoTracking().ToDictionaryAsync(s => s.UserID, s => (s.TeamName, s.WillCaptain, s.RequiresAssistanceDrafting));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signups[padded], Is.EqualTo(("Hammers", true, true)), "Trimmed; a captain keeps their help");
            Assert.That(signups[spaces], Is.EqualTo(((string?)null, false, false)), "No name; no help for a player who won't captain");
            Assert.That(signups[empty], Is.EqualTo(((string?)null, true, false)));
            Assert.That(signups[none], Is.EqualTo(((string?)null, false, false)));
            Assert.That(signups[kept], Is.EqualTo(("Anvils", false, false)), "A name a player kept stays");
        }
    }
}
