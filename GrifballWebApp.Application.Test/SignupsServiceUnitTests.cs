using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>SignupsService against the fake IApplicationDbContext: reading a season's signups and a player's timeslots
/// (shifted to their time zone), and signing up or changing a signup with each refusal.</summary>
[TestFixture]
internal sealed class SignupsServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SignedUpAt = new(2026, 5, 30, 18, 0, 0, DateTimeKind.Utc);
    private const int SeasonID = 1;
    private const int OtherSeasonID = 2;
    private const int PlayerID = 1;
    private const int PlaceholderID = 2;
    private const int SaturdayLate = 10;
    private const int MondayEarly = 11;
    private const int SundayEarly = 12;
    private const int NotOffered = 13;
    private static readonly int[] OnlySaturdayLate = [SaturdayLate];
    private static readonly int[] BothOffered = [SaturdayLate, MondayEarly];
    private static readonly int[] OfferedAndNot = [SaturdayLate, NotOffered];
    private static readonly int[] NoOptions = [];
    private static readonly DayOfWeek[] ByDay = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Saturday];
    private static readonly int[] OptionsByDay = [SundayEarly, MondayEarly, SaturdayLate];
    private static readonly bool[] OnlyLastChecked = [false, false, true];

    private SignupsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new SignupsService(Context, new FakeTimeProvider(Now));
    }

    /// <summary>A season whose signups are open at <see cref="Now"/> (unless <paramref name="open"/> is false), offering
    /// Saturday 23:00, Monday 01:00 and Sunday 00:30; option 13 exists but the season doesn't offer it.</summary>
    private async Task SeedSeason(bool open = true)
    {
        var season = Season.Seed(seasonName: "S1", seasonID: SeasonID);
        if (open)
            season.Schedule(Now.AddDays(-5), Now.AddDays(5), Now.AddDays(6), Now.AddDays(10), Now.AddDays(60));
        Context.Seasons.AddRange(season, Season.Seed(seasonName: "S2", seasonID: OtherSeasonID));
        Context.Availability.AddRange(
            AvailabilityOption.Seed(SaturdayLate, DayOfWeek.Saturday, new TimeOnly(23, 0)),
            AvailabilityOption.Seed(MondayEarly, DayOfWeek.Monday, new TimeOnly(1, 0)),
            AvailabilityOption.Seed(SundayEarly, DayOfWeek.Sunday, new TimeOnly(0, 30)),
            AvailabilityOption.Seed(NotOffered, DayOfWeek.Friday, new TimeOnly(20, 0)));
        await Context.SaveChangesAsync();
        Context.AddRange(
            SeasonAvailability.Seed(SeasonID, SaturdayLate),
            SeasonAvailability.Seed(SeasonID, MondayEarly),
            SeasonAvailability.Seed(SeasonID, SundayEarly));
        await Context.SaveChangesAsync();
    }

    private async Task SeedPlayer(bool withGamertag = true)
    {
        var xbox = withGamertag ? XboxUser.FromXbox(500, "Chief") : null;
        Context.Users.Add(User.Seed(userName: "chief", displayName: "Chief Display", xboxUser: xbox, regionID: 3, regionSource: RegionSource.User, id: PlayerID));
        await Context.SaveChangesAsync();
    }

    private Task<SeasonSignup> ReadSignup() => Context.SeasonSignups.AsNoTracking().Include(s => s.SignupAvailability)
        .SingleAsync(s => s.SeasonID == SeasonID && s.UserID == PlayerID);

    private static SignupRequestDto Request(int[] checkedOptions, string? teamName = null, bool willCaptain = false, bool assistance = false) => new()
    {
        SeasonID = SeasonID,
        UserID = PlayerID,
        TeamName = teamName,
        WillCaptain = willCaptain,
        RequiresAssistanceDrafting = assistance,
        Timeslots = checkedOptions.Select(o => new TimeslotDto { OptionID = o, IsChecked = true })
            .Append(new TimeslotDto { OptionID = SundayEarly, IsChecked = false })
            .ToArray(),
    };

    [Test]
    public async Task GetSignups_ReturnsTheSeasonsSignupsNamedAsTheAppNamesThem()
    {
        await SeedSeason();
        await SeedPlayer();
        Context.Users.Add(User.Seed(userName: "placeholder", displayName: "Old Timer", id: PlaceholderID));
        await Context.SaveChangesAsync();
        Context.SeasonSignups.AddRange(
            SeasonSignup.Seed(SeasonID, PlayerID, SignedUpAt, teamName: "Bombers", willCaptain: true, requiresAssistanceDrafting: true),
            SeasonSignup.Seed(SeasonID, PlaceholderID, SignedUpAt),
            SeasonSignup.Seed(OtherSeasonID, PlayerID, SignedUpAt));
        await Context.SaveChangesAsync();

        var signups = (await _service.GetSignups(SeasonID)).OrderBy(s => s.UserID).ToList();

        Assert.That(signups, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signups[0].SeasonID, Is.EqualTo(SeasonID));
            Assert.That(signups[0].UserID, Is.EqualTo(PlayerID));
            Assert.That(signups[0].PersonName, Is.EqualTo("Chief"));
            Assert.That(signups[0].Timestamp, Is.EqualTo(SignedUpAt));
            Assert.That(signups[0].TeamName, Is.EqualTo("Bombers"));
            Assert.That(signups[0].WillCaptain, Is.True);
            Assert.That(signups[0].RequiresAssistanceDrafting, Is.True);
            Assert.That(signups[0].Region, Is.EqualTo("East North America"));
            Assert.That(signups[0].Timeslots, Is.Empty);
            Assert.That(signups[1].PersonName, Is.EqualTo("Old Timer"));
            Assert.That(signups[1].Region, Is.Null);
            Assert.That(signups[1].WillCaptain, Is.False);
        }
    }

    [Test]
    public async Task GetSignups_NoSignups_ReturnsEmpty()
    {
        await SeedSeason();

        var signups = await _service.GetSignups(SeasonID);

        Assert.That(signups, Is.Empty);
    }

    [Test]
    public async Task GetTimeslots_NoOffset_MarksThePlayersChoicesAndOrdersByDay()
    {
        await SeedSeason();
        await SeedPlayer();
        Context.SeasonSignups.Add(SeasonSignup.Seed(SeasonID, PlayerID, SignedUpAt, timeslots: OnlySaturdayLate));
        await Context.SaveChangesAsync();

        var timeslots = await _service.GetTimeslots(SeasonID, 0, PlayerID);

        Assert.That(timeslots, Has.Length.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(timeslots.Select(t => t.DayOfWeek), Is.EqualTo(ByDay));
            Assert.That(timeslots.Select(t => t.Time), Is.EqualTo(new[] { new TimeOnly(0, 30), new TimeOnly(1, 0), new TimeOnly(23, 0) }));
            Assert.That(timeslots.Select(t => t.IsChecked), Is.EqualTo(OnlyLastChecked));
            Assert.That(timeslots.Select(t => t.OptionID), Is.EqualTo(OptionsByDay));
        }
    }

    [Test]
    public async Task GetTimeslots_PositiveOffset_WrapsSaturdayNightIntoSunday()
    {
        await SeedSeason();

        var timeslots = await _service.GetTimeslots(SeasonID, 120, PlayerID);

        var saturdayLate = timeslots.Single(t => t.OptionID == SaturdayLate);
        var mondayEarly = timeslots.Single(t => t.OptionID == MondayEarly);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saturdayLate.DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
            Assert.That(saturdayLate.Time, Is.EqualTo(new TimeOnly(1, 0)));
            Assert.That(mondayEarly.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(mondayEarly.Time, Is.EqualTo(new TimeOnly(3, 0)));
            Assert.That(timeslots.All(t => !t.IsChecked), Is.True);
        }
    }

    [Test]
    public async Task GetTimeslots_NegativeOffset_WrapsSundayMorningBackIntoSaturday()
    {
        await SeedSeason();

        var timeslots = await _service.GetTimeslots(SeasonID, -60, PlayerID);

        var sundayEarly = timeslots.Single(t => t.OptionID == SundayEarly);
        var mondayEarly = timeslots.Single(t => t.OptionID == MondayEarly);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sundayEarly.DayOfWeek, Is.EqualTo(DayOfWeek.Saturday));
            Assert.That(sundayEarly.Time, Is.EqualTo(new TimeOnly(23, 30)));
            Assert.That(mondayEarly.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
            Assert.That(mondayEarly.Time, Is.EqualTo(new TimeOnly(0, 0)));
        }
    }

    [Test]
    public async Task GetSignup_NotSignedUp_ReturnsNull()
    {
        await SeedSeason();
        await SeedPlayer();

        var signup = await _service.GetSignup(SeasonID, 0, PlayerID);

        Assert.That(signup, Is.Null);
    }

    [Test]
    public async Task GetSignup_SignedUp_ReturnsItWithItsTimeslots()
    {
        await SeedSeason();
        await SeedPlayer();
        Context.SeasonSignups.Add(SeasonSignup.Seed(SeasonID, PlayerID, SignedUpAt, teamName: "Bombers", willCaptain: true, timeslots: OnlySaturdayLate));
        await Context.SaveChangesAsync();

        var signup = await _service.GetSignup(SeasonID, 0, PlayerID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(signup!.UserID, Is.EqualTo(PlayerID));
            Assert.That(signup.SeasonID, Is.EqualTo(SeasonID));
            Assert.That(signup.PersonName, Is.EqualTo("Chief"));
            Assert.That(signup.TeamName, Is.EqualTo("Bombers"));
            Assert.That(signup.WillCaptain, Is.True);
            Assert.That(signup.RequiresAssistanceDrafting, Is.False);
            Assert.That(signup.Region, Is.EqualTo("East North America"));
            Assert.That(signup.Timestamp, Is.EqualTo(SignedUpAt));
            Assert.That(signup.Timeslots.Where(t => t.IsChecked).Select(t => t.OptionID), Is.EqualTo(OnlySaturdayLate));
            Assert.That(signup.Timeslots, Has.Length.EqualTo(3));
        }
    }

    [Test]
    public async Task UpsertSignup_NoSuchSeason_Throws()
    {
        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(Request(NoOptions)));

        var signups = await Context.SeasonSignups.CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("Season does not exist"));
            Assert.That(signups, Is.Zero);
        }
    }

    [Test]
    public async Task UpsertSignup_CaptainsTeamNameTooLong_ThrowsTeamNameTooLong()
    {
        await SeedSeason();
        await SeedPlayer();

        var ex = await Assert.ThrowsAsync<SignupTeamNameTooLongException>(() => _service.UpsertSignup(Request(NoOptions, new string('x', Team.TeamNameMaxLength + 1), willCaptain: true)));

        var signups = await Context.SeasonSignups.CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(SignupTeamNameTooLongException.Reason));
            Assert.That(signups, Is.Zero);
        }
    }

    [Test]
    public async Task UpsertSignup_SignupsClosed_ThrowsSignupsClosed()
    {
        await SeedSeason(open: false);
        await SeedPlayer();

        await Assert.ThrowsAsync<SignupsClosedException>(() => _service.UpsertSignup(Request(NoOptions)));

        Assert.That(await Context.SeasonSignups.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task UpsertSignup_NoGamertag_ThrowsNeedsGamertag()
    {
        await SeedSeason();
        await SeedPlayer(withGamertag: false);

        var ex = await Assert.ThrowsAsync<SignupNeedsGamertagException>(() => _service.UpsertSignup(Request(OnlySaturdayLate)));

        var signups = await Context.SeasonSignups.CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(SignupNeedsGamertagException.Reason));
            Assert.That(signups, Is.Zero);
        }
    }

    [Test]
    public async Task UpsertSignup_TimeslotNotOffered_ThrowsNamingIt()
    {
        await SeedSeason();
        await SeedPlayer();

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(Request(OfferedAndNot)));

        var signups = await Context.SeasonSignups.CountAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo($"Cannot add options for season {SeasonID}. The following are not valid {NotOffered}"));
            Assert.That(signups, Is.Zero);
        }
    }

    [Test]
    public async Task UpsertSignup_New_AddsTheSignupWithItsCheckedTimeslots()
    {
        await SeedSeason();
        await SeedPlayer();

        await _service.UpsertSignup(Request(BothOffered, "  Bombers  ", willCaptain: true, assistance: true));

        var saved = await ReadSignup();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.TeamName, Is.EqualTo("Bombers"));
            Assert.That(saved.WillCaptain, Is.True);
            Assert.That(saved.RequiresAssistanceDrafting, Is.True);
            Assert.That(saved.Timestamp, Is.EqualTo(Now));
            Assert.That(saved.SignupAvailability.Select(a => a.AvailabilityOptionID).Order(), Is.EqualTo(BothOffered));
        }
    }

    [Test]
    public async Task UpsertSignup_Existing_ChangesItInPlace()
    {
        await SeedSeason();
        await SeedPlayer();
        Context.SeasonSignups.Add(SeasonSignup.Seed(SeasonID, PlayerID, SignedUpAt, teamName: "Old", willCaptain: true, timeslots: BothOffered));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        await _service.UpsertSignup(Request(OnlySaturdayLate));

        Context.ChangeTracker.Clear();
        var saved = await ReadSignup();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonSignups.CountAsync(), Is.EqualTo(1));
            Assert.That(saved.TeamName, Is.Null);
            Assert.That(saved.WillCaptain, Is.False);
            Assert.That(saved.Timestamp, Is.EqualTo(Now));
            Assert.That(saved.SignupAvailability.Select(a => a.AvailabilityOptionID), Is.EqualTo(OnlySaturdayLate));
        }
    }
}
