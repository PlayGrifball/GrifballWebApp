using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using System.Security.Claims;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class RescheduleServiceWorkflowTests
{
    private const ulong Channel = 123UL;
    private GrifballContext _context;
    private RescheduleService _service;
    private IDiscordRestClient _discordClient;
    private ILogger<RescheduleService> _logger;
    private IDiscordGuildThread _thread;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _discordClient = Substitute.For<IDiscordRestClient>();
        _logger = Substitute.For<ILogger<RescheduleService>>();
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { ReschedulesChannel = Channel });

        var msg = Substitute.For<IDiscordRestMessage>();
        msg.Id.Returns(777UL);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(msg);
        _thread = Substitute.For<IDiscordGuildThread>();
        _thread.Id.Returns(888UL);
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_thread);

        _service = new RescheduleService(_context, _discordClient, _logger, options);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private List<string> SentContents(ulong channel) => _discordClient.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == nameof(IDiscordRestClient.SendMessageAsync) && c.GetArguments().Length == 4 && (ulong)c.GetArguments()[0]! == channel)
        .Select(c => ((MessageProperties)c.GetArguments()[1]!).Content!)
        .ToList();

    // ---------- RequestRescheduleAsync ----------

    [Test]
    public async Task Request_SetsActiveRequestAndOriginalTime()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var newTime = scheduled.AddDays(2);

        var r = await _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, NewScheduledTime = newTime, Reason = "Vacation" }, s.Requester.Id);

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync(x => x.SeasonMatchID == s.Match.SeasonMatchID);
        var saved = await ctx.MatchReschedules.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(sm.ActiveRescheduleRequestId, Is.EqualTo(r.MatchRescheduleID));
            Assert.That(sm.ScheduledTime, Is.EqualTo(scheduled), "scheduled time unchanged until approval");
            Assert.That(saved.OriginalScheduledTime, Is.EqualTo(scheduled));
            Assert.That(saved.NewScheduledTime, Is.EqualTo(newTime));
            Assert.That(saved.RequestedByUserID, Is.EqualTo(s.Requester.Id));
            Assert.That(saved.Status, Is.EqualTo(RescheduleStatus.Pending));
        });
    }

    [Test]
    public void Request_Throws_When_SeasonMatchNotFound()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = 4242, Reason = "x" }, 1));
        Assert.That(ex!.Message, Is.EqualTo("Season match not found"));
    }

    [Test]
    public async Task Request_Throws_When_ActiveRequestExists_AndCreatesNothing()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var first = await _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "one" }, s.Requester.Id);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "two" }, s.Requester.Id));

        Assert.That(ex!.Message, Does.Contain("already an active reschedule request"));
        await using var ctx = _context.NewContextLike();
        Assert.That(await ctx.MatchReschedules.Select(x => x.MatchRescheduleID).ToListAsync(), Is.EqualTo(new[] { first.MatchRescheduleID }));
    }

    // ---------- ProcessRescheduleAsync ----------

    [Test]
    public void Process_Throws_When_NotFound()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.ProcessRescheduleAsync(999, new ProcessRescheduleDto { Approved = true }, 1));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }

    [TestCase(RescheduleStatus.Approved)]
    [TestCase(RescheduleStatus.Rejected)]
    public async Task Process_Throws_When_AlreadyProcessed(RescheduleStatus status)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, status);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request has already been processed"));
    }

    [Test]
    public async Task Process_Approve_UpdatesScheduledTime_AndClearsActiveRequest()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var newTime = scheduled.AddDays(3);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: newTime);
        s.Match.ActiveRescheduleRequestId = r.MatchRescheduleID;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var before = DateTime.UtcNow.AddSeconds(-5);
        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true, CommissionerNotes = "ok" }, s.AwayCaptain.Id);

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync(x => x.SeasonMatchID == s.Match.SeasonMatchID);
        var saved = await ctx.MatchReschedules.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(sm.ScheduledTime, Is.EqualTo(newTime));
            Assert.That(sm.ActiveRescheduleRequestId, Is.Null);
            Assert.That(saved.Status, Is.EqualTo(RescheduleStatus.Approved));
            Assert.That(saved.ApprovedByUserID, Is.EqualTo(s.AwayCaptain.Id));
            Assert.That(saved.CommissionerNotes, Is.EqualTo("ok"));
            Assert.That(saved.ProcessedAt, Is.GreaterThan(before));
        });
        // No discord thread => no message
        await _discordClient.DidNotReceiveWithAnyArgs().SendMessageAsync(0UL, default!, default, default);
    }

    [Test]
    public async Task Process_Approve_WithoutNewTime_KeepsScheduledTime()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: null);
        _context.ChangeTracker.Clear();

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id);

        await using var ctx = _context.NewContextLike();
        Assert.That((await ctx.SeasonMatches.SingleAsync()).ScheduledTime, Is.EqualTo(scheduled));
        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Approved));
    }

    [Test]
    public async Task Process_Reject_KeepsScheduledTime_AndNotifiesThread()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: scheduled.AddDays(1), threadId: 4444UL);
        s.Match.ActiveRescheduleRequestId = r.MatchRescheduleID;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = false, CommissionerNotes = "no" }, s.HomeCaptain.Id);

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(sm.ScheduledTime, Is.EqualTo(scheduled));
            Assert.That(sm.ActiveRescheduleRequestId, Is.Null);
            Assert.That(SentContents(4444UL), Is.EqualTo(new[] { $"Your request has been Rejected by {s.HomeCaptain.Id}" }));
        });
    }

    // ---------- GetPendingReschedulesAsync ----------

    [Test]
    public async Task GetPending_MapsCaptains_FiltersAndOrders()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, requestedAt: t0.AddHours(2), threadId: 99UL, newTime: t0.AddDays(5), original: t0.AddDays(1));
        var earlier = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.HomeCaptain.Id, requestedAt: t0);
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, RescheduleStatus.Approved, requestedAt: t0.AddHours(1));
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, RescheduleStatus.Rejected, requestedAt: t0.AddHours(1));

        var result = await _service.GetPendingReschedulesAsync();

        Assert.That(result.Select(x => x.MatchRescheduleID), Is.EqualTo(new[] { earlier.MatchRescheduleID, later.MatchRescheduleID }));
        var dto = result[1];
        Assert.Multiple(() =>
        {
            Assert.That(dto.SeasonMatchID, Is.EqualTo(s.Match.SeasonMatchID));
            Assert.That(dto.HomeCaptain, Is.EqualTo("GT_homecap"));
            Assert.That(dto.AwayCaptain, Is.EqualTo("GT_awaycap"));
            Assert.That(dto.RequestedByGamertag, Is.EqualTo("GT_requester"));
            Assert.That(dto.Reason, Is.EqualTo("Cannot make it"));
            Assert.That(dto.Status, Is.EqualTo(RescheduleStatus.Pending));
            Assert.That(dto.DiscordThreadID, Is.EqualTo(99UL));
            Assert.That(dto.OriginalScheduledTime, Is.EqualTo(t0.AddDays(1)));
            Assert.That(dto.NewScheduledTime, Is.EqualTo(t0.AddDays(5)));
            Assert.That(dto.RequestedAt, Is.EqualTo(t0.AddHours(2)));
            Assert.That(result[0].RequestedByGamertag, Is.EqualTo("GT_homecap"));
        });
    }

    [Test]
    public async Task GetPending_UsesUnknown_When_TeamsMissing_AndFallsBackToDisplayName()
    {
        var season = new Season { SeasonName = "S" };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var sm = new SeasonMatch { SeasonID = season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.Add(sm);
        await _context.SaveChangesAsync();
        var named = await RescheduleTestData.AddUser(_context, "named", displayName: "Named Person");
        var anonymous = await RescheduleTestData.AddUser(_context, "anon");
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await RescheduleTestData.AddReschedule(_context, sm.SeasonMatchID, named.Id, requestedAt: t0);
        await RescheduleTestData.AddReschedule(_context, sm.SeasonMatchID, anonymous.Id, requestedAt: t0.AddMinutes(1));

        var result = await _service.GetPendingReschedulesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result[0].HomeCaptain, Is.EqualTo("UNKNOWN"));
            Assert.That(result[0].AwayCaptain, Is.EqualTo("UNKNOWN"));
            Assert.That(result[0].RequestedByGamertag, Is.EqualTo("Named Person"));
            Assert.That(result[1].RequestedByGamertag, Is.EqualTo("UNKNOWN"));
        });
    }

    [Test]
    public async Task GetPending_Throws_When_TeamHasNoCaptain()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        s.Home.CaptainID = null;
        await _context.SaveChangesAsync();
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();

        // BUG: RescheduleService.cs:144 uses `HomeTeam?.Captain.User` - a team without a captain (CaptainID is nullable)
        // causes a NullReferenceException instead of falling back to "UNKNOWN".
        Assert.ThrowsAsync<NullReferenceException>(() => _service.GetPendingReschedulesAsync());
    }

    // ---------- GetOverdueMatchesAsync ----------

    [Test]
    public async Task GetOverdue_ReturnsOnlyUndecidedMatchesOlderThan24Hours()
    {
        var now = DateTime.UtcNow;
        var s = await RescheduleTestData.SeedAsync(_context, now.AddHours(-50));
        var older = new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Away.TeamID, AwayTeamID = s.Home.TeamID, BestOf = 1, ScheduledTime = now.AddHours(-100) };
        _context.SeasonMatches.AddRange(
            older,
            new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1, ScheduledTime = now.AddHours(-10) }, // recent
            new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1, ScheduledTime = now.AddDays(2) }, // future
            new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1, ScheduledTime = null }, // unscheduled
            new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1, ScheduledTime = now.AddHours(-60), HomeTeamResult = SeasonMatchResult.Won }, // decided
            new SeasonMatch { SeasonID = s.Season.SeasonID, HomeTeamID = s.Home.TeamID, AwayTeamID = s.Away.TeamID, BestOf = 1, ScheduledTime = now.AddHours(-60), AwayTeamResult = SeasonMatchResult.Forfeit }); // decided
        await _context.SaveChangesAsync();

        var result = await _service.GetOverdueMatchesAsync();

        Assert.That(result.Select(x => x.SeasonMatchID), Is.EqualTo(new[] { older.SeasonMatchID, s.Match.SeasonMatchID }));
        Assert.Multiple(() =>
        {
            Assert.That(result[0].HomeCaptain, Is.EqualTo("GT_awaycap"));
            Assert.That(result[0].AwayCaptain, Is.EqualTo("GT_homecap"));
            Assert.That(result[0].HoursOverdue, Is.InRange(99, 100));
            Assert.That(result[1].HoursOverdue, Is.InRange(49, 50));
            Assert.That(result[1].ScheduledTime, Is.EqualTo(s.Match.ScheduledTime!.Value).Within(TimeSpan.FromMilliseconds(1)));
        });
    }

    [Test]
    public async Task GetOverdue_ReturnsEmpty_When_NothingOverdue()
    {
        await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddHours(1));
        Assert.That(await _service.GetOverdueMatchesAsync(), Is.Empty);
    }

    // ---------- CreateDiscordThreadAsync ----------

    [Test]
    public void CreateThread_Throws_When_NotFound()
    {
        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscordThreadAsync(12345));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }

    [Test]
    public async Task CreateThread_DoesNothing_When_ThreadAlreadyExists()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 55UL);

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task CreateThread_PostsMessage_CreatesThread_AddsCaptainsAndCommissioners()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var commissionerRole = await _context.Roles.SingleAsync(x => x.Name == "Commissioner");
        var commish = await RescheduleTestData.AddUser(_context, "commish", 1004, 5004);
        var commishNoDiscord = await RescheduleTestData.AddUser(_context, "commish2", displayName: "Commish Two");
        _context.UserRoles.AddRange(new UserRole { UserId = commish.Id, RoleId = commissionerRole.Id }, new UserRole { UserId = commishNoDiscord.Id, RoleId = commissionerRole.Id });
        await _context.SaveChangesAsync();
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, original: null, newTime: null);
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);

        var channelMessages = SentContents(Channel);
        Assert.That(channelMessages, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(channelMessages[0], Does.Contain("**Match:** GT_homecap vs GT_awaycap"));
            Assert.That(channelMessages[0], Does.Contain("**Original Time:** Not scheduled"));
            Assert.That(channelMessages[0], Does.Contain("**Requested New Time:** TBD"));
            Assert.That(channelMessages[0], Does.Contain("**Requested By:** GT_requester"));
            Assert.That(channelMessages[0], Does.Contain("**Reason:** Cannot make it"));
        });
        await _discordClient.Received(1).CreateGuildThreadAsync(Channel, 777UL,
            Arg.Is<GuildThreadFromMessageProperties>(p => p.Name == "Reschedule: GT_homecap vs GT_awaycap"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _thread.Received(1).AddUserAsync(5001UL, null, Arg.Any<CancellationToken>());
        await _thread.Received(1).AddUserAsync(5002UL, null, Arg.Any<CancellationToken>());
        await _thread.Received(1).AddUserAsync(5004UL, null, Arg.Any<CancellationToken>());
        Assert.That(SentContents(888UL), Is.EqualTo(new[] { "Could not add Commish Two to thread, missing discord id." + Environment.NewLine }));

        await using var ctx = _context.NewContextLike();
        Assert.That((await ctx.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(888UL));
    }

    [Test]
    public async Task CreateThread_IncludesTimeEmbeds_And_ReportsAddFailures()
    {
        var s = await RescheduleTestData.SeedAsync(_context, captainsHaveDiscord: false);
        var original = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, original: original, newTime: original.AddDays(1));
        // requester is not involved in the thread; make adding fail for any user to exercise the catch branch
        _thread.AddUserAsync(Arg.Any<ulong>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("boom"));
        var commissionerRole = await _context.Roles.SingleAsync(x => x.Name == "Commissioner");
        var commish = await RescheduleTestData.AddUser(_context, "commish", 1004, 5004);
        _context.UserRoles.Add(new UserRole { UserId = commish.Id, RoleId = commissionerRole.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);

        var channelMessage = SentContents(Channel).Single();
        Assert.Multiple(() =>
        {
            Assert.That(channelMessage, Does.Contain($"**Original Time:** <t:{new DateTimeOffset(original).ToUnixTimeSeconds()}"));
            Assert.That(channelMessage, Does.Contain($"**Requested New Time:** <t:{new DateTimeOffset(original.AddDays(1)).ToUnixTimeSeconds()}"));
        });
        var errors = SentContents(888UL).Single();
        Assert.Multiple(() =>
        {
            Assert.That(errors, Does.Contain("Could not add homecap to thread, missing discord id."));
            Assert.That(errors, Does.Contain("Could not add awaycap to thread, missing discord id."));
            Assert.That(errors, Does.Contain("Could not add commish to thread, exception thrown"));
        });
    }
}
