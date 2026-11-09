using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Reschedule;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
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
[Parallelizable(ParallelScope.All)]
public class RescheduleServiceWorkflowTests
{
    private const ulong Channel = 123UL;
    private GrifballContext _context;
    private RescheduleService _service;
    private IDiscordRestClient _discordClient;
    private ILogger<RescheduleService> _logger;
    private IDiscordGuildThread _thread;
    private OutboxTestDriver _outbox;

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

        _service = new RescheduleService(_context, new ContextOutbox(_context), _logger, options);
        _outbox = new OutboxTestDriver(_context, _discordClient, options.Value);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _outbox.DisposeAsync();
        await _context.DropDatabaseAndDispose();
    }

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
            Assert.That(saved.MatchRescheduleID, Is.EqualTo(r.MatchRescheduleID), "pending: the match's active request");
            Assert.That(sm.ScheduledTime, Is.EqualTo(scheduled), "scheduled time unchanged until approval");
            Assert.That(saved.OriginalScheduledTime, Is.EqualTo(scheduled));
            Assert.That(saved.NewScheduledTime, Is.EqualTo(newTime));
            Assert.That(saved.RequestedByUserID, Is.EqualTo(s.Requester.Id));
            Assert.That(saved.Status, Is.EqualTo(RescheduleStatus.Pending));
        });
    }

    [Test]
    public async Task Request_Throws_When_SeasonMatchNotFound()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = 4242, Reason = "x" }, 1));
        Assert.That(ex!.Message, Is.EqualTo("Season match not found"));
    }

    [Test]
    public async Task Request_Throws_When_ActiveRequestExists_AndCreatesNothing()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var first = await _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "one" }, s.Requester.Id);

        var ex = await Assert.ThrowsAsync<RescheduleAlreadyActiveException>(() =>
            _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "two" }, s.Requester.Id));

        Assert.That(ex!.Message, Does.Contain("already an active reschedule request"));
        await using var ctx = _context.NewContextLike();
        Assert.That(await ctx.MatchReschedules.Select(x => x.MatchRescheduleID).ToListAsync(), Is.EqualTo(new[] { first.MatchRescheduleID }));
    }

    // Decided by its games, a forfeit, or both teams forfeiting (a BYE side has no team: "not both teams" comes first)
    [TestCase(null)]
    [TestCase(Forfeiter.Away)]
    [TestCase(Forfeiter.Both)]
    public async Task Request_Throws_NotAllowed_When_MatchHasAResult(Forfeiter? forfeiter)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        if (forfeiter is { } who)
            s.Match.Forfeited(who);
        else
            s.Match.WonBy(SeasonMatchSide.Home);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<RescheduleNotAllowedException>(() =>
            _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "late" }, s.HomeCaptain.Id));

        Assert.That(ex!.Message, Is.EqualTo("This match already has a result."));
        Assert.That(await _context.MatchReschedules.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Request_Throws_NotAllowed_When_MatchIsMissingATeam()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        // A bracket match still waiting on an earlier result
        s.Match.ClearSide(SeasonMatchSide.Away);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<RescheduleNotAllowedException>(() =>
            _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "late" }, s.HomeCaptain.Id));

        Assert.That(ex!.Message, Is.EqualTo("This match does not have both teams yet."));
    }

    [Test]
    public async Task Request_Throws_Forbidden_When_UserIsOnNeitherTeam_AndNotACommissioner()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var outsider = await RescheduleTestData.AddUser(_context, "outsider", 1009);
        // On a roster, but of another team in the season
        var otherCaptain = await RescheduleTestData.AddUser(_context, "othercap", 1010);
        await RescheduleTestData.AddTeamWithCaptain(_context, s.Season, "Other", otherCaptain);

        foreach (var user in new[] { outsider, otherCaptain })
        {
            await Assert.ThrowsAsync<RescheduleForbiddenException>(() =>
                _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "late" }, user.Id));
        }
        Assert.That(await _context.MatchReschedules.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Request_IsAllowed_ForEitherTeamsPlayers_AndForACommissioner()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var commissioner = await RescheduleTestData.AddUser(_context, "commish", 1011);
        await RescheduleTestData.MakeCommissioner(_context, commissioner);

        // One request at a time per match: each asker gets their own match between the same teams.
        foreach (var user in new[] { s.Requester, s.HomeCaptain, s.AwayCaptain, commissioner })
        {
            var match = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1);
            _context.SeasonMatches.Add(match);
            await _context.SaveChangesAsync();

            var r = await _service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = match.SeasonMatchID, Reason = "late" }, user.Id);

            Assert.That(r.RequestedByUserID, Is.EqualTo(user.Id));
        }
    }

    // ---------- ProcessRescheduleAsync ----------

    [Test]
    public async Task Process_Throws_When_NotFound()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.ProcessRescheduleAsync(999, new ProcessRescheduleDto { Approved = true }, 1));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }

    [TestCase(RescheduleStatus.Approved)]
    [TestCase(RescheduleStatus.Rejected)]
    public async Task Process_Throws_When_AlreadyProcessed(RescheduleStatus status)
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, status);

        var ex = await Assert.ThrowsAsync<RescheduleAlreadyProcessedException>(() => _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request has already been processed"));
    }

    [TestCase(SeasonMatchResult.Won, SeasonMatchResult.Loss)]
    [TestCase(SeasonMatchResult.Forfeit, SeasonMatchResult.Won)]
    public async Task Process_RefusesToApprove_When_TheMatchWasDecidedSinceTheRequest(SeasonMatchResult home, SeasonMatchResult away)
    {
        // #297: a match reported or forfeited while its request waited keeps its time; the request stays pending
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: scheduled.AddDays(3));
        // Decided past the model, as a report racing the request's decision leaves it: the match's own decision would
        // close its pending request (#297)
        var decided = _context.Entry(s.Match);
        decided.Property(m => m.HomeTeamResult).CurrentValue = home;
        decided.Property(m => m.AwayTeamResult).CurrentValue = away;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<RescheduleNotAllowedException>(() =>
            _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id));

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync();
        var saved = await ctx.MatchReschedules.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Is.EqualTo("This match already has a result."));
            Assert.That(sm.ScheduledTime, Is.EqualTo(scheduled));
            Assert.That(saved.Status, Is.EqualTo(RescheduleStatus.Pending));
            Assert.That(saved.ApprovedByUserID, Is.Null);
        });
    }

    [Test]
    public async Task Process_Rejects_When_TheMatchWasDecidedSinceTheRequest()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: scheduled.AddDays(3));
        // Decided past the model, as a report racing the request's decision leaves it: the match's own decision would
        // close its pending request (#297)
        var decided = _context.Entry(s.Match);
        decided.Property(m => m.HomeTeamResult).CurrentValue = SeasonMatchResult.Won;
        decided.Property(m => m.AwayTeamResult).CurrentValue = SeasonMatchResult.Loss;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, s.HomeCaptain.Id);

        await using var ctx = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That((await ctx.SeasonMatches.SingleAsync()).ScheduledTime, Is.EqualTo(scheduled));
        });
    }

    [Test]
    public async Task Request_Throws_AlreadyActive_When_AnotherRequestIsSavedFirst()
    {
        // Two requests at once both pass the check; the unique pending index keeps the first saved, and the other is told
        var s = await RescheduleTestData.SeedAsync(_context);
        var rival = new RivalRequest(_context, s.Match.SeasonMatchID, s.AwayCaptain.Id);
        await using var racing = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).AddInterceptors(rival).Options);
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { ReschedulesChannel = Channel });
        var service = new RescheduleService(racing, new ContextOutbox(racing), _logger, options);

        var ex = await Assert.ThrowsAsync<RescheduleAlreadyActiveException>(() =>
            service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "mine" }, s.Requester.Id));

        await using var ctx = _context.NewContextLike();
        var saved = await ctx.MatchReschedules.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("already an active reschedule request"));
            Assert.That(saved.Reason, Is.EqualTo("first"), "the rival's, saved first");
        });
    }

    [Test]
    public async Task Request_Rethrows_AFailedSave_ThatIsNotAnotherRequest()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        await using var failing = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).AddInterceptors(new FailingSave()).Options);
        var options = Substitute.For<IOptions<DiscordOptions>>();
        options.Value.Returns(new DiscordOptions { ReschedulesChannel = Channel });
        var service = new RescheduleService(failing, new ContextOutbox(failing), _logger, options);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.RequestRescheduleAsync(new RescheduleRequestDto { SeasonMatchID = s.Match.SeasonMatchID, Reason = "mine" }, s.Requester.Id));

        await using var ctx = _context.NewContextLike();
        Assert.That(await ctx.MatchReschedules.AnyAsync(), Is.False);
    }

    /// <summary>Fails the context's save, as the database would for any other reason than a pending request.</summary>
    private sealed class FailingSave : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
            => throw new DbUpdateException("refused");
    }

    /// <summary>Saves another pending request for the match just before the context's own save, once.</summary>
    private sealed class RivalRequest(GrifballContext like, int seasonMatchID, int userID) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!_done)
            {
                _done = true;
                await using var ctx = like.NewContextLike();
                ctx.MatchReschedules.Add(MatchReschedule.Request(seasonMatchID, null, null, "first", userID, DateTime.UtcNow));
                await ctx.SaveChangesAsync(ct);
            }
            return result;
        }
    }

    [Test]
    public async Task Process_Approve_UpdatesScheduledTime_AndEndsTheActiveRequest()
    {
        var scheduled = new DateTime(2025, 6, 1, 20, 0, 0, DateTimeKind.Utc);
        var s = await RescheduleTestData.SeedAsync(_context, scheduled);
        var newTime = scheduled.AddDays(3);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, newTime: newTime);
        _context.ChangeTracker.Clear();

        var before = DateTime.UtcNow.AddSeconds(-5);
        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true, CommissionerNotes = "ok" }, s.AwayCaptain.Id);
        await _outbox.DrainAsync();

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync(x => x.SeasonMatchID == s.Match.SeasonMatchID);
        var saved = await ctx.MatchReschedules.SingleAsync();
        var active = await ctx.MatchReschedules.AnyAsync(MatchReschedule.IsPending);
        Assert.Multiple(() =>
        {
            Assert.That(sm.ScheduledTime, Is.EqualTo(newTime));
            Assert.That(active, Is.False, "no active request");
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
        await _outbox.DrainAsync();

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
        _context.ChangeTracker.Clear();

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = false, CommissionerNotes = "no" }, s.HomeCaptain.Id);
        await _outbox.DrainAsync();

        await using var ctx = _context.NewContextLike();
        var sm = await ctx.SeasonMatches.SingleAsync();
        var active = await ctx.MatchReschedules.AnyAsync(MatchReschedule.IsPending);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Rejected));
            Assert.That(sm.ScheduledTime, Is.EqualTo(scheduled));
            Assert.That(active, Is.False, "no active request");
            // The approver as Discord shows them (a mention: their Discord account is linked), not their user ID
            Assert.That(SentContents(4444UL), Is.EqualTo(new[] { "Your request has been Rejected by <@5001>" }));
        });
    }

    [Test]
    public async Task Process_NamesAnApproverWithNoName_AsACommissioner_NeverByTheirLogin()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var commissioner = await RescheduleTestData.AddUser(_context, "private-login");
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 4445UL);
        _context.ChangeTracker.Clear();

        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, commissioner.Id);
        await _outbox.DrainAsync();

        Assert.That(SentContents(4445UL), Is.EqualTo(new[] { "Your request has been Approved by a commissioner" }));
    }

    [Test]
    public async Task Process_NamesTheApprover_WhenTheyHaveNoDiscordAccount()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var commissioner = await RescheduleTestData.AddUser(_context, "commish", 1011);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 4445UL);
        _context.ChangeTracker.Clear();

        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, commissioner.Id);
        await _outbox.DrainAsync();

        Assert.That(SentContents(4445UL), Is.EqualTo(new[] { "Your request has been Approved by GT_commish" }));
    }

    [Test]
    public async Task Process_StillSavesTheDecision_When_DiscordIsOffInAPreview()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 4446UL);
        _context.ChangeTracker.Clear();
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new GrifballWebApp.Server.Preview.PreviewDiscordDisabledException("Discord is disabled in preview mode"));

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id);
        await _outbox.DrainAsync();

        await using var ctx = _context.NewContextLike();
        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Approved));
        Assert.That((await ctx.MatchReschedules.SingleAsync()).Status, Is.EqualTo(RescheduleStatus.Approved));
        Assert.That(await ctx.MatchReschedules.AnyAsync(MatchReschedule.IsPending), Is.False, "no active request");
        Assert.That((await _outbox.MessagesAsync()).Single().Status, Is.EqualTo(OutboxStatus.Skipped), "Skipped, not retried");
        Assert.That(_outbox.Entries.Where(x => x.Level >= LogLevel.Warning), Is.Empty);
    }

    [Test]
    public async Task Process_StillSavesTheDecision_When_DiscordFails()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 4447UL);
        _context.ChangeTracker.Clear();
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Discord is down"));

        var result = await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, s.HomeCaptain.Id);
        await _outbox.DrainAsync();

        await using var ctx = _context.NewContextLike();
        Assert.That(result.Status, Is.EqualTo(RescheduleStatus.Rejected));
        Assert.That((await ctx.MatchReschedules.SingleAsync()).Status, Is.EqualTo(RescheduleStatus.Rejected));
        var decision = (await _outbox.MessagesAsync()).Single();
        Assert.That(decision.Status, Is.EqualTo(OutboxStatus.DeadLettered), "Tried until given up on");
        Assert.That(decision.Attempts, Is.EqualTo(_outbox.Options.MaxAttempts));
        Assert.That(_outbox.Entries.Count(x => x.Level == LogLevel.Error), Is.EqualTo(1));
    }

    // ---------- GetPendingReschedulesAsync ----------

    [Test]
    public async Task GetPending_MapsCaptains_FiltersAndOrders()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        // A match has one pending request: the earlier one is the rematch's
        var rematch = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1);
        _context.SeasonMatches.Add(rematch);
        await _context.SaveChangesAsync();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, requestedAt: t0.AddHours(2), threadId: 99UL, newTime: t0.AddDays(5), original: t0.AddDays(1));
        var earlier = await RescheduleTestData.AddReschedule(_context, rematch.SeasonMatchID, s.HomeCaptain.Id, requestedAt: t0);
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
    public async Task GetPending_OffersOnlyReject_When_TheMatchWasDecidedSinceTheRequest()
    {
        // A match reported while its request waits can't move: the dashboard offers Reject only, and says why
        var s = await RescheduleTestData.SeedAsync(_context);
        var open = SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1);
        _context.SeasonMatches.Add(open);
        await _context.SaveChangesAsync();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var decided = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, requestedAt: t0);
        var undecided = await RescheduleTestData.AddReschedule(_context, open.SeasonMatchID, s.Requester.Id, requestedAt: t0.AddHours(1));
        // Decided past the model, as a report racing the request's decision leaves it: the match's own decision would
        // close its pending request (#297)
        var entry = _context.Entry(s.Match);
        entry.Property(m => m.HomeTeamResult).CurrentValue = SeasonMatchResult.Won;
        entry.Property(m => m.AwayTeamResult).CurrentValue = SeasonMatchResult.Loss;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = (await _service.GetPendingReschedulesAsync()).ToDictionary(x => x.MatchRescheduleID);

        Assert.Multiple(() =>
        {
            Assert.That(result[decided.MatchRescheduleID].CanApprove, Is.False);
            Assert.That(result[decided.MatchRescheduleID].WhyNotApprovable, Is.EqualTo("This match already has a result."));
            Assert.That(result[undecided.MatchRescheduleID].CanApprove, Is.True);
            Assert.That(result[undecided.MatchRescheduleID].WhyNotApprovable, Is.Null);
        });
    }

    [Test]
    public async Task GetPending_UsesUnknown_When_TeamsMissing_AndFallsBackToDisplayName()
    {
        var season = Season.Seed(seasonName: "S");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var sm = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 1);
        var sm2 = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 1);
        _context.SeasonMatches.AddRange(sm, sm2);
        await _context.SaveChangesAsync();
        var named = await RescheduleTestData.AddUser(_context, "named", displayName: "Named Person");
        var anonymous = await RescheduleTestData.AddUser(_context, "anon");
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await RescheduleTestData.AddReschedule(_context, sm.SeasonMatchID, named.Id, requestedAt: t0);
        await RescheduleTestData.AddReschedule(_context, sm2.SeasonMatchID, anonymous.Id, requestedAt: t0.AddMinutes(1));

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
    public async Task GetPending_UsesUnknown_When_TeamHasNoCaptain()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        s.Home.ClearCaptain();
        s.Away.ClearCaptain();
        await _context.SaveChangesAsync();
        await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();

        var result = await _service.GetPendingReschedulesAsync();

        Assert.That(result[0].HomeCaptain, Is.EqualTo("UNKNOWN"));
        Assert.That(result[0].AwayCaptain, Is.EqualTo("UNKNOWN"));
    }

    // ---------- GetOverdueMatchesAsync ----------

    [Test]
    public async Task GetOverdue_ReturnsOnlyUndecidedMatchesOlderThan24Hours()
    {
        var now = DateTime.UtcNow;
        var s = await RescheduleTestData.SeedAsync(_context, now.AddHours(-50));
        var older = SeasonMatch.Seed(s.Season.SeasonID, s.Away.TeamID, s.Home.TeamID, bestOf: 1, scheduledTime: now.AddHours(-100));
        _context.SeasonMatches.AddRange(
            older,
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1, scheduledTime: now.AddHours(-10)), // recent
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1, scheduledTime: now.AddDays(2)), // future
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1), // unscheduled
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1, scheduledTime: now.AddHours(-60)).WonBy(SeasonMatchSide.Home), // decided
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, s.Away.TeamID, bestOf: 1, scheduledTime: now.AddHours(-60)).Forfeited(Forfeiter.Away)); // decided
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
    public async Task GetOverdue_UsesUnknown_When_TeamHasNoCaptain()
    {
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddHours(-50));
        s.Home.ClearCaptain();
        s.Away.ClearCaptain();
        await _context.SaveChangesAsync();

        var result = await _service.GetOverdueMatchesAsync();

        Assert.That(result[0].HomeCaptain, Is.EqualTo("UNKNOWN"));
        Assert.That(result[0].AwayCaptain, Is.EqualTo("UNKNOWN"));
    }

    [Test]
    public async Task GetOverdue_LeavesOutMatchesMissingATeam()
    {
        var s = await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddHours(-50));
        // Bracket matches given a time before both teams were known: not playable, so not overdue (and no crash)
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(s.Season.SeasonID, s.Home.TeamID, null, bestOf: 1, scheduledTime: DateTime.UtcNow.AddHours(-60)),
            SeasonMatch.Seed(s.Season.SeasonID, null, null, bestOf: 1, scheduledTime: DateTime.UtcNow.AddHours(-70)));
        await _context.SaveChangesAsync();

        var result = await _service.GetOverdueMatchesAsync();

        Assert.That(result.Select(x => x.SeasonMatchID), Is.EqualTo(new[] { s.Match.SeasonMatchID }));
    }

    [Test]
    public async Task GetOverdue_ReturnsEmpty_When_NothingOverdue()
    {
        await RescheduleTestData.SeedAsync(_context, DateTime.UtcNow.AddHours(1));
        Assert.That(await _service.GetOverdueMatchesAsync(), Is.Empty);
    }

    // ---------- CreateDiscordThreadAsync ----------

    [Test]
    public async Task CreateThread_Throws_When_NotFound()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateDiscordThreadAsync(12345));
        Assert.That(ex!.Message, Is.EqualTo("Reschedule request not found"));
    }

    [Test]
    public async Task CreateThread_DoesNothing_When_ThreadAlreadyExists()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 55UL);

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task CreateThread_PostsMessage_CreatesThread_AddsCaptainsAndCommissioners()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var commissionerRole = await _context.Roles.SingleAsync(x => x.Name == "Commissioner");
        var commish = await RescheduleTestData.AddUser(_context, "commish", 1004, 5004);
        var commishNoDiscord = await RescheduleTestData.AddUser(_context, "commish2", displayName: "Commish Two");
        // No name at all: named by their user ID, never their login name
        var commishNameless = await RescheduleTestData.AddUser(_context, "commish3-login");
        _context.UserRoles.AddRange(new UserRole { UserId = commish.Id, RoleId = commissionerRole.Id }, new UserRole { UserId = commishNoDiscord.Id, RoleId = commissionerRole.Id },
            new UserRole { UserId = commishNameless.Id, RoleId = commissionerRole.Id });
        await _context.SaveChangesAsync();
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, original: null, newTime: null);
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

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
        await _discordClient.Received(1).AddGuildThreadUserAsync(888UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discordClient.Received(1).AddGuildThreadUserAsync(888UL, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discordClient.Received(1).AddGuildThreadUserAsync(888UL, 5004UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        var threadErrors = SentContents(888UL).Single();
        Assert.Multiple(() =>
        {
            Assert.That(threadErrors, Does.Contain("Could not add Commish Two to thread, missing discord id."));
            Assert.That(threadErrors, Does.Contain($"Could not add user {commishNameless.Id} to thread, missing discord id."));
            Assert.That(threadErrors, Does.Not.Contain("commish3-login"));
        });

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
        _discordClient.AddGuildThreadUserAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("boom"));
        var commissionerRole = await _context.Roles.SingleAsync(x => x.Name == "Commissioner");
        var commish = await RescheduleTestData.AddUser(_context, "commish", 1004, 5004);
        _context.UserRoles.Add(new UserRole { UserId = commish.Id, RoleId = commissionerRole.Id });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

        var channelMessage = SentContents(Channel).Single();
        Assert.Multiple(() =>
        {
            Assert.That(channelMessage, Does.Contain($"**Original Time:** <t:{new DateTimeOffset(original).ToUnixTimeSeconds()}"));
            Assert.That(channelMessage, Does.Contain($"**Requested New Time:** <t:{new DateTimeOffset(original.AddDays(1)).ToUnixTimeSeconds()}"));
        });
        var errors = SentContents(888UL).Single();
        Assert.Multiple(() =>
        {
            Assert.That(errors, Does.Contain("Could not add GT_homecap to thread, missing discord id."));
            Assert.That(errors, Does.Contain("Could not add GT_awaycap to thread, missing discord id."));
            Assert.That(errors, Does.Contain("Could not add GT_commish to thread, exception thrown"));
        });
    }

    [Test]
    public async Task CreateThread_UsesUnknown_And_ReportsMissingCaptain_When_TeamHasNoCaptain()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        s.Home.ClearCaptain();
        s.Away.ClearCaptain();
        await _context.SaveChangesAsync();
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

        var channelMessage = SentContents(Channel).Single();
        Assert.That(channelMessage, Does.Contain("**Match:** UNKNOWN vs UNKNOWN"));
        await _discordClient.Received(1).CreateGuildThreadAsync(Channel, 777UL,
            Arg.Is<GuildThreadFromMessageProperties>(p => p.Name == "Reschedule: UNKNOWN vs UNKNOWN"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        var errors = SentContents(888UL).Single();
        Assert.Multiple(() =>
        {
            Assert.That(errors, Does.Contain("Could not add Home captain to thread, team has no captain."));
            Assert.That(errors, Does.Contain("Could not add Away captain to thread, team has no captain."));
        });
        await _discordClient.DidNotReceive().AddGuildThreadUserAsync(888UL, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discordClient.DidNotReceive().AddGuildThreadUserAsync(888UL, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateThread_UsesUnknown_When_CaptainHasNoXboxAccount()
    {
        var season = Season.Seed(seasonName: "S");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var homeCaptain = await RescheduleTestData.AddUser(_context, "homecapnoxbox", discordId: 5101, displayName: "Home Cap");
        var awayCaptain = await RescheduleTestData.AddUser(_context, "awaycapnoxbox", discordId: 5102, displayName: "Away Cap");
        var requester = await RescheduleTestData.AddUser(_context, "requester2", 1003, 5003);
        var home = await RescheduleTestData.AddTeamWithCaptain(_context, season, "Home2", homeCaptain);
        var away = await RescheduleTestData.AddTeamWithCaptain(_context, season, "Away2", awayCaptain);
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var r = await RescheduleTestData.AddReschedule(_context, match.SeasonMatchID, requester.Id);
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

        var channelMessage = SentContents(Channel).Single();
        Assert.That(channelMessage, Does.Contain("**Match:** UNKNOWN vs UNKNOWN"));
        await _discordClient.Received(1).CreateGuildThreadAsync(Channel, 777UL,
            Arg.Is<GuildThreadFromMessageProperties>(p => p.Name == "Reschedule: UNKNOWN vs UNKNOWN"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Process_IsQueuedWithTheDecision_AndRolledBackWithIt()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id, threadId: 4448UL);
        _context.ChangeTracker.Clear();
        _context.SavingChanges += (_, _) =>
        {
            if (_context.ChangeTracker.Entries<OutboxMessage>().Any(x => x.State == EntityState.Added))
                throw new InvalidOperationException("database down");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id));

        await using var ctx = _context.NewContextLike();
        Assert.That((await ctx.MatchReschedules.SingleAsync()).Status, Is.EqualTo(RescheduleStatus.Pending));
        Assert.That(await ctx.Outbox.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Process_WhileTheThreadIsStillQueued_TellsItOnceItIsMade()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = false }, s.HomeCaptain.Id);
        await _outbox.DrainAsync();

        var messages = await _outbox.MessagesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(messages.Select(x => x.Kind), Is.EqualTo(new[] { RescheduleOutbox.RequestPost, RescheduleOutbox.ThreadCreate, RescheduleOutbox.ThreadInvite, RescheduleOutbox.DecisionPost }), "The thread queued once");
            Assert.That(messages.Select(x => x.Status), Is.All.EqualTo(OutboxStatus.Sent));
            Assert.That(SentContents(888UL).Last(), Is.EqualTo("Your request has been Rejected by <@5001>"), "After the thread was made");
        });
    }

    [Test]
    public async Task Process_WithoutAThread_QueuesNothing()
    {
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();

        await _service.ProcessRescheduleAsync(r.MatchRescheduleID, new ProcessRescheduleDto { Approved = true }, s.HomeCaptain.Id);

        Assert.That(await _outbox.MessagesAsync(), Is.Empty);
    }

    [Test]
    public async Task CreateThread_RetriedAfterPartialSuccess_PostsAndMakesNothingTwice()
    {
        // The post went out but its try failed; then the thread was made but its try failed too.
        var s = await RescheduleTestData.SeedAsync(_context);
        var r = await RescheduleTestData.AddReschedule(_context, s.Match.SeasonMatchID, s.Requester.Id);
        _context.ChangeTracker.Clear();
        var threads = 0;
        var posted = Substitute.For<IDiscordRestMessage>();
        posted.Id.Returns(777UL);
        posted.StartedThread.Returns(_ => threads > 0 ? _thread : null);
        var me = Substitute.For<IDiscordCurrentUser>();
        _discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(me);
        var posts = 0;
        _discordClient.SendMessageAsync(Channel, Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                posted.Content.Returns(((MessageProperties)c.ArgAt<MessageProperties>(1)).Content);
                return ++posts == 1 ? Task.FromException<IDiscordRestMessage>(new TaskCanceledException("timed out")) : Task.FromResult(posted);
            });
        _discordClient.GetMessagesAsync(Channel, Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>()).Returns(_ => (posts > 0 ? new[] { posted } : []).ToAsyncEnumerable());
        _discordClient.GetMessageAsync(Channel, 777UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(posted);
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(_ => ++threads == 1 ? Task.FromException<IDiscordGuildThread>(new TaskCanceledException("timed out")) : Task.FromResult(_thread));

        await _service.CreateDiscordThreadAsync(r.MatchRescheduleID);
        await _outbox.DrainAsync();

        await using var ctx = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That((posts, threads), Is.EqualTo((1, 1)), "Each found on the retry, not made again");
            Assert.That((await ctx.MatchReschedules.SingleAsync()).DiscordThreadID, Is.EqualTo(888UL));
        });
    }
}
