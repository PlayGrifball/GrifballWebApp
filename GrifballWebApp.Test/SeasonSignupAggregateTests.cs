using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>A signup begins and changes only through its methods, while the season's signups are open.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SeasonSignupAggregateTests
{
    private static readonly DateTime Open = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = Open.AddDays(7);
    private static readonly DateTime During = Open.AddDays(1);

    /// <summary>A season with signups open from <see cref="Open"/> to <see cref="Close"/>, offering the timeslots 1, 2 and 3.</summary>
    private static Season OpenSeason(int seasonID = 5)
    {
        var season = Season.Seed(seasonName: "Signups", seasonID: seasonID)
            .WithDates(signupsOpen: Open, signupsClose: Close, draftStart: Close, seasonStart: Close.AddDays(1), seasonEnd: Close.AddDays(30));
        season.SetAvailability([Option(1), Option(2), Option(3)]);
        return season;
    }

    private static AvailabilityOption Option(int id) => AvailabilityOption.Seed(id, DayOfWeek.Monday, new TimeOnly(id, 0));

    private static SignupChoices Choices(string? teamName = null, bool willCaptain = false, bool assistance = false, params int[] timeslots)
        => new(teamName, willCaptain, assistance, timeslots);

    private static int[] Timeslots(SeasonSignup signup) => signup.SignupAvailability.Select(a => a.AvailabilityOptionID).Order().ToArray();

    [Test]
    public void SignUp_WhileOpen_SignsUpWithTheChoices_TheNameTrimmed()
    {
        var outcome = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices("  The Hammers ", true, true, 1, 2, 2));

        var signup = outcome.Signup!;
        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(SignupResult.SignedUp));
            Assert.That((signup.SeasonID, signup.UserID, signup.Timestamp), Is.EqualTo((5, 7, During)));
            Assert.That(signup.TeamName, Is.EqualTo("The Hammers"));
            Assert.That((signup.WillCaptain, signup.RequiresAssistanceDrafting), Is.EqualTo((true, true)));
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 1, 2 }));
        });
    }

    [Test]
    public void SignUp_AsAPlayerWhoWontCaptain_AsksNoHelpDrafting_AndABlankNameIsNone()
    {
        var signup = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices("   ", willCaptain: false, assistance: true)).Signup!;

        Assert.Multiple(() =>
        {
            Assert.That(signup.TeamName, Is.Null);
            Assert.That(signup.RequiresAssistanceDrafting, Is.False, "Only a captain drafts");
            Assert.That(signup.SignupAvailability, Is.Empty);
        });
    }

    [Test]
    public void SignUp_OutsideTheWindow_IsRefused()
    {
        // Open from their open, included, to their close, not included (Season.SignupsAreOpenAt)
        Assert.Multiple(() =>
        {
            Assert.That(SeasonSignup.SignUp(OpenSeason(), 7, Open.AddTicks(-1), Choices()), Is.EqualTo(new SignupOutcome(SignupResult.SignupsClosed)));
            Assert.That(SeasonSignup.SignUp(OpenSeason(), 7, Close, Choices()), Is.EqualTo(new SignupOutcome(SignupResult.SignupsClosed)));
            Assert.That(SeasonSignup.SignUp(OpenSeason(), 7, Open, Choices()).Result, Is.EqualTo(SignupResult.SignedUp));
        });
    }

    [Test]
    public void SignUp_ATimeslotTheSeasonDoesntOffer_IsRefused()
    {
        var outcome = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices(null, false, false, 1, 9, 8, 9));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(SignupResult.TimeslotsNotOffered));
            Assert.That(outcome.Signup, Is.Null);
            Assert.That(outcome.RefusedTimeslots, Is.EqualTo(new[] { 9, 8 }));
        });
    }

    [Test]
    public void Change_WhileOpen_TakesTheNewChoices_AndTimeslots()
    {
        var season = OpenSeason();
        var signup = SeasonSignup.SignUp(season, 7, During, Choices("Hammers", true, true, 1, 2)).Signup!;

        var outcome = signup.Change(season, During.AddHours(1), Choices("Anvils ", false, true, 2, 3));

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(new SignupOutcome(SignupResult.Changed, signup)));
            Assert.That(signup.Timestamp, Is.EqualTo(During.AddHours(1)), "When they last changed it");
            Assert.That(signup.TeamName, Is.EqualTo("Anvils"), "Kept, as the signups page shows it");
            Assert.That((signup.WillCaptain, signup.RequiresAssistanceDrafting), Is.EqualTo((false, false)));
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 2, 3 }));
        });
    }

    [Test]
    public void Change_KeepsATimeslotTheSeasonNoLongerOffers_ButRefusesANewOne()
    {
        var season = OpenSeason();
        var signup = SeasonSignup.Seed(season.SeasonID, 7, Open, teamName: "Hammers", timeslots: [1, 4]);

        var kept = signup.Change(season, During, Choices("Hammers", false, false, 1, 4));
        var refused = signup.Change(season, During.AddHours(1), Choices("Other", false, false, 4, 5));

        Assert.Multiple(() =>
        {
            Assert.That(kept.Result, Is.EqualTo(SignupResult.Changed));
            Assert.That(refused.Result, Is.EqualTo(SignupResult.TimeslotsNotOffered));
            Assert.That(refused.RefusedTimeslots, Is.EqualTo(new[] { 5 }));
            Assert.That((signup.TeamName, signup.Timestamp), Is.EqualTo(("Hammers", During)), "Nothing changed by the refusal");
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 1, 4 }));
        });
    }

    [Test]
    public void SignUp_AsACaptain_ATeamNameTooLongOnceTrimmed_IsRefused_FirstOfAll()
    {
        var tooLong = new string('x', Team.TeamNameMaxLength + 1);
        var atTheLimit = "  " + new string('x', Team.TeamNameMaxLength) + " ";

        Assert.Multiple(() =>
        {
            Assert.That(SeasonSignup.SignUp(OpenSeason(), 7, During, Choices(tooLong, willCaptain: true)), Is.EqualTo(new SignupOutcome(SignupResult.TeamNameTooLong)));
            Assert.That(SeasonSignup.SignUp(OpenSeason(), 7, Close, Choices(tooLong, true, false, 9)).Result, Is.EqualTo(SignupResult.TeamNameTooLong),
                "Before the window and the timeslots, as the signups page always refused it");
            var signedUp = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices(atTheLimit, willCaptain: true));
            Assert.That((signedUp.Result, signedUp.Signup!.TeamName), Is.EqualTo((SignupResult.SignedUp, atTheLimit.Trim())), "Counted trimmed");
        });
    }

    [Test]
    public void SignUp_AsAPlayerWhoWontCaptain_ALongTeamName_IsNoBar()
    {
        var name = new string('x', Team.TeamNameMaxLength + 10);

        var outcome = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices(name, willCaptain: false));

        Assert.That((outcome.Result, outcome.Signup!.TeamName), Is.EqualTo((SignupResult.SignedUp, name)), "No one else's name becomes a team's");
    }

    [Test]
    public void Change_ToCaptainWithATeamNameTooLong_IsRefused_AndChangesNothing_ButNotForAPlayerWhoWontCaptain()
    {
        var season = OpenSeason();
        var longName = new string('x', Team.TeamNameMaxLength + 1);
        var signup = SeasonSignup.Seed(season.SeasonID, 7, Open, teamName: longName, timeslots: [1]);

        var refused = signup.Change(season, During, Choices(longName, true, true, 2));
        Assert.Multiple(() =>
        {
            Assert.That(refused, Is.EqualTo(new SignupOutcome(SignupResult.TeamNameTooLong, signup)));
            Assert.That((signup.WillCaptain, signup.RequiresAssistanceDrafting, signup.Timestamp), Is.EqualTo((false, false, Open)), "Nothing changed by the refusal");
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 1 }));
        });

        var kept = signup.Change(season, During, Choices(longName, false, false, 2));
        Assert.Multiple(() =>
        {
            Assert.That(kept.Result, Is.EqualTo(SignupResult.Changed), "An old long name doesn't block a player who won't captain");
            Assert.That(signup.TeamName, Is.EqualTo(longName));
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public void Change_OnceClosed_IsRefused_AndChangesNothing()
    {
        var season = OpenSeason();
        var signup = SeasonSignup.SignUp(season, 7, During, Choices("Hammers", true, false, 1)).Signup!;

        var outcome = signup.Change(season, Close, Choices("Anvils", false, false));

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Result, Is.EqualTo(SignupResult.SignupsClosed));
            Assert.That((signup.TeamName, signup.WillCaptain, signup.Timestamp), Is.EqualTo(("Hammers", true, During)));
            Assert.That(Timeslots(signup), Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public void Change_ForAnotherSeason_Throws()
    {
        var signup = SeasonSignup.SignUp(OpenSeason(), 7, During, Choices()).Signup!;

        Assert.Throws<ArgumentException>(() => signup.Change(OpenSeason(seasonID: 6), During, Choices()));
    }

    [Test]
    public void Copy_IsTheSamePlayersChoicesAndTimeslots_ForAnotherSeason()
    {
        var signup = SeasonSignup.Seed(5, 7, During, teamName: "Hammers", willCaptain: true, requiresAssistanceDrafting: true, timeslots: [1, 3]);

        var copy = signup.Copy();

        Assert.Multiple(() =>
        {
            Assert.That(copy, Is.Not.SameAs(signup));
            Assert.That((copy.SeasonID, copy.UserID, copy.Timestamp), Is.EqualTo((0, 7, During)), "The season it is added to gives its key");
            Assert.That((copy.TeamName, copy.WillCaptain, copy.RequiresAssistanceDrafting), Is.EqualTo(("Hammers", true, true)));
            Assert.That(Timeslots(copy), Is.EqualTo(new[] { 1, 3 }));
            Assert.That(copy.SignupAvailability.Intersect(signup.SignupAvailability), Is.Empty, "Timeslots of its own");
        });
    }

    [Test]
    public void MoveToUser_MakesItTheirs()
    {
        var signup = SeasonSignup.Seed(5, 7);

        signup.MoveToUser(8);

        Assert.That(signup.UserID, Is.EqualTo(8));
    }

    [Test]
    public void TeamNameAtDraft_IsTheirs_ElseTheCaptainsTeam()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SeasonSignup.Seed(5, 7, teamName: "Hammers", willCaptain: true).TeamNameAtDraft("Cap"), Is.EqualTo("Hammers"));
            Assert.That(SeasonSignup.Seed(5, 7).TeamNameAtDraft("Cap"), Is.EqualTo("Cap's Team"));
        });
    }

    [Test]
    public void Of_IsTheSignupOfThePlayer_ForTheSeason()
    {
        var of = SeasonSignup.Of(5, 7).Compile();

        Assert.Multiple(() =>
        {
            Assert.That(of(SeasonSignup.Seed(5, 7)), Is.True);
            Assert.That(of(SeasonSignup.Seed(5, 8)), Is.False);
            Assert.That(of(SeasonSignup.Seed(6, 7)), Is.False);
        });
    }
}

/// <summary>The database refuses a signup no method makes (CK_Event_SeasonSignups_*).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSignupAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

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

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused("", true, false), Is.True, "A blank team name");
            Assert.That(await Refused("   ", true, false), Is.True, "A team name of spaces");
            Assert.That(await Refused("  Hammers", true, false), Is.True, "A team name not trimmed");
            Assert.That(await Refused(null, false, true), Is.True, "Help drafting for a player who won't captain");

            Assert.That(await Refused(null, false, false), Is.False);
            Assert.That(await Refused("Hammers", false, false), Is.False, "A name kept by a player who no longer captains");
            Assert.That(await Refused("Hammers", true, true), Is.False);
        });
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

        Assert.Multiple(async () =>
        {
            Assert.That(outcome.Result, Is.EqualTo(SignupResult.Changed));
            Assert.That(await _context.SignupAvailability.Select(a => a.AvailabilityOptionID).ToListAsync(), Is.EqualTo(new[] { options[1].AvailabilityOptionID }));
        });
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
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

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
            await _context.Database.ExecuteSqlRawAsync(
                $"INSERT INTO {SeasonSignups} (\"UserID\", \"SeasonID\", \"Timestamp\", \"TeamName\", \"WillCaptain\", \"RequiresAssistanceDrafting\", \"Approved\", \"CreatedAt\", \"ModifiedAt\") " +
                // A null name written as NULL: a raw parameter needs a type, which DBNull has none of
                $"VALUES ({{0}}, {{1}}, {{2}}, {(teamName is null ? "NULL" : "{5}")}, {{3}}, {{4}}, {{3}}, {{2}}, {{2}})", args);
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
        Assert.Multiple(() =>
        {
            Assert.That(signups[padded], Is.EqualTo(("Hammers", true, true)), "Trimmed; a captain keeps their help");
            Assert.That(signups[spaces], Is.EqualTo(((string?)null, false, false)), "No name; no help for a player who won't captain");
            Assert.That(signups[empty], Is.EqualTo(((string?)null, true, false)));
            Assert.That(signups[none], Is.EqualTo(((string?)null, false, false)));
            Assert.That(signups[kept], Is.EqualTo(("Anvils", false, false)), "A name a player kept stays");
        });
    }
}
