using System.Net;
using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Database.Models.User;
using DiscordUser = GrifballWebApp.Database.Models.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SignupsServiceTimeslotTests
{
    private GrifballContext _context;
    private SignupsService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new SignupsService(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetTimeslots_PositiveOffsetPastMidnight_MovesToNextDay()
    {
        var season = await SignupTestData.OpenSeason(_context);
        await SignupTestData.Options(_context, season, (DayOfWeek.Saturday, new TimeOnly(23, 17)), (DayOfWeek.Monday, new TimeOnly(10, 17)));

        var slots = await _service.GetTimeslots(season.SeasonID, 120, userID: 0);

        Assert.That(slots, Has.Length.EqualTo(2));
        // Saturday 23:17 + 2h => Sunday 01:17, which sorts first (Sunday = 0)
        Assert.That(slots[0].DayOfWeek, Is.EqualTo(DayOfWeek.Sunday));
        Assert.That(slots[0].Time, Is.EqualTo(new TimeOnly(1, 17)));
        Assert.That(slots[1].DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        Assert.That(slots[1].Time, Is.EqualTo(new TimeOnly(12, 17)));
        Assert.That(slots.All(s => s.IsChecked is false));
    }

    [Test]
    public async Task GetTimeslots_NegativeOffsetBeforeMidnight_CurrentBehaviour()
    {
        var season = await SignupTestData.OpenSeason(_context);
        await SignupTestData.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(1, 17)), (DayOfWeek.Wednesday, new TimeOnly(15, 17)));

        var slots = await _service.GetTimeslots(season.SeasonID, -120, userID: 0);

        var shifted = slots.Single(s => s.Time == new TimeOnly(23, 17));
        // BUG: SignupsService.GetTimeslots uses TimeSpan.Days on a negative span (01:17 - 2h = -00:43), which truncates
        // to 0, so the slot stays on Monday instead of rolling back to Sunday 23:17.
        Assert.That(shifted.DayOfWeek, Is.EqualTo(DayOfWeek.Monday));
        var other = slots.Single(s => s.Time == new TimeOnly(13, 17));
        Assert.That(other.DayOfWeek, Is.EqualTo(DayOfWeek.Wednesday));
    }

    [Test]
    public async Task UpsertSignup_AddsAndRemovesAvailability()
    {
        var season = await SignupTestData.OpenSeason(_context);
        var opts = await SignupTestData.Options(_context, season, (DayOfWeek.Monday, new TimeOnly(1, 17)), (DayOfWeek.Tuesday, new TimeOnly(2, 17)));
        var user = new User { UserName = "signer" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id, TeamName = "T",
            Timeslots = [new TimeslotDto { OptionID = opts[0].AvailabilityOptionID, IsChecked = true }, new TimeslotDto { OptionID = opts[1].AvailabilityOptionID, IsChecked = false }],
        });

        var first = await _service.GetTimeslots(season.SeasonID, 0, user.Id);
        Assert.That(first.Where(s => s.IsChecked).Select(s => s.OptionID), Is.EqualTo(new[] { opts[0].AvailabilityOptionID }));

        _context.ChangeTracker.Clear();
        await _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id, TeamName = "T2",
            Timeslots = [new TimeslotDto { OptionID = opts[1].AvailabilityOptionID, IsChecked = true }],
        });

        var second = await _service.GetSignup(season.SeasonID, 0, user.Id);
        Assert.That(second!.TeamName, Is.EqualTo("T2"));
        Assert.That(second.Timeslots.Where(s => s.IsChecked).Select(s => s.OptionID), Is.EqualTo(new[] { opts[1].AvailabilityOptionID }));
        Assert.That(await _context.SignupAvailability.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task UpsertSignup_OptionNotInSeason_Throws()
    {
        var season = await SignupTestData.OpenSeason(_context);
        var otherSeason = await SignupTestData.OpenSeason(_context, name: "Other Season");
        var foreign = await SignupTestData.Options(_context, otherSeason, (DayOfWeek.Friday, new TimeOnly(4, 17)));
        var user = new User { UserName = "signer2" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.UpsertSignup(new SignupRequestDto
        {
            SeasonID = season.SeasonID, UserID = user.Id,
            Timeslots = [new TimeslotDto { OptionID = foreign[0].AvailabilityOptionID, IsChecked = true }],
        }));
        Assert.That(ex!.Message, Is.EqualTo($"Cannot add options for season {season.SeasonID}. The following are not valid {foreign[0].AvailabilityOptionID}"));
    }
}
