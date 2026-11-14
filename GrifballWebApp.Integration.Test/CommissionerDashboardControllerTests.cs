using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Commissioner;
using GrifballWebApp.Domain.League;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Reschedule;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommissionerDashboardControllerTests
{
    private GrifballContext _context;
    private CommissionerDashboardController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
        var service = new RescheduleService(_context, new ContextOutbox(_context));
        _controller = new CommissionerDashboardController(service, new GrifballWebApp.Application.EventOrganizer.EventOrganizerService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task TheDashboard_ListsTheSeasonsStillToStart_ThatWontBePublicByTheirSignups()
    {
        var now = DateTime.UtcNow;
        // Signups in 10 days: one with no Public at, one announced after them, one announced before them (fine), one started
        var unannounced = Season.Seed(seasonName: "No Date").WithDates(signupsOpen: now.AddDays(10), signupsClose: now.AddDays(15),
            draftStart: now.AddDays(20), seasonStart: now.AddDays(30), seasonEnd: now.AddDays(60));
        var late = Season.Seed(seasonName: "Too Late").WithDates(signupsOpen: now.AddDays(10), signupsClose: now.AddDays(15),
            draftStart: now.AddDays(20), seasonStart: now.AddDays(30), seasonEnd: now.AddDays(60), publicAt: now.AddDays(12));
        var fine = Season.Seed(seasonName: "In Time").WithDates(signupsOpen: now.AddDays(10), signupsClose: now.AddDays(15),
            draftStart: now.AddDays(20), seasonStart: now.AddDays(30), seasonEnd: now.AddDays(60), publicAt: now.AddDays(5));
        var started = Season.Seed(seasonName: "Started").WithDates(signupsOpen: now.AddDays(-40), signupsClose: now.AddDays(-30),
            draftStart: now.AddDays(-20), seasonStart: now.AddDays(-10), seasonEnd: now.AddDays(30));
        _context.Seasons.AddRange(unannounced, late, fine, started);
        await _context.SaveChangesAsync();

        var data = await _controller.GetDashboardData(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data.SeasonsNotPublicBySignups.Select(s => (s.SeasonName, s.PublicAt is null)),
                Is.EquivalentTo(new[] { ("No Date", true), ("Too Late", false) }));
            Assert.That(data.Summary.SeasonsNotPublicBySignupsCount, Is.EqualTo(2));
        }
    }

    private async Task SeedOverdueAndPending()
    {
        var season = await TeamsTestData.AddCurrentSeason(_context);
        var home = await TeamsTestData.AddTeamWithCaptain(_context, season, "HomeCap", 101);
        var away = await TeamsTestData.AddTeamWithCaptain(_context, season, "AwayCap", 102);
        var now = DateTime.UtcNow;
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 1, scheduledTime: now.AddHours(-100)), // critical
            SeasonMatch.Seed(season.SeasonID, away.TeamID, home.TeamID, bestOf: 1, scheduledTime: now.AddHours(-30)),  // overdue, not critical
            SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 1, scheduledTime: now.AddHours(-30)).WonBy(SeasonMatchSide.Home), // reported
            SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 1, scheduledTime: now.AddHours(5))); // future
        await _context.SaveChangesAsync();

        var future = await _context.SeasonMatches.OrderByDescending(x => x.ScheduledTime).FirstAsync();
        var requester = await _context.Users.FirstAsync(u => u.UserName == "user_HomeCap");
        var old = MatchReschedule.Request(future.SeasonMatchID, future.ScheduledTime, now.AddDays(3), "old", requester.Id, now);
        _context.MatchReschedules.AddRange(
            MatchReschedule.Request(future.SeasonMatchID, future.ScheduledTime, now.AddDays(2), "busy", requester.Id, now), old);
        RescheduleTestData.Decided(_context, old, RescheduleStatus.Approved, requester.Id);
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task GetDashboardData_SummarisesPendingAndOverdue()
    {
        await SeedOverdueAndPending();

        var result = await _controller.GetDashboardData(CancellationToken.None);

        var dto = result;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.PendingReschedules, Has.Count.EqualTo(1));
            Assert.That(dto.PendingReschedules[0].Reason, Is.EqualTo("busy"));
            Assert.That(dto.PendingReschedules[0].RequestedByGamertag, Is.EqualTo("HomeCap"));
            Assert.That(dto.OverdueMatches, Has.Count.EqualTo(2));
            Assert.That(dto.OverdueMatches[0].HomeCaptain, Is.EqualTo("HomeCap"));
            Assert.That(dto.Summary.PendingRescheduleCount, Is.EqualTo(1));
            Assert.That(dto.Summary.OverdueMatchCount, Is.EqualTo(2));
            Assert.That(dto.Summary.CriticalOverdueCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetDashboardData_Empty_ReturnsZeroes()
    {
        var dto = await _controller.GetDashboardData(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.PendingReschedules, Is.Empty);
            Assert.That(dto.OverdueMatches, Is.Empty);
            Assert.That(dto.Summary.CriticalOverdueCount, Is.Zero);
        }
    }

    [Test]
    public async Task GetOverdueMatches_ReturnsOldestFirst()
    {
        await SeedOverdueAndPending();

        var list = await _controller.GetOverdueMatches(CancellationToken.None);

        Assert.That(list, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(list[0].HoursOverdue, Is.GreaterThanOrEqualTo(99));
            Assert.That(list[1].HoursOverdue, Is.InRange(29, 30));
            Assert.That(list[1].HomeCaptain, Is.EqualTo("AwayCap"));
        }
    }
}
