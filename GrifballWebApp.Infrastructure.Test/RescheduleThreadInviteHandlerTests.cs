using DiscordInterface.Generated;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Reschedule;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RescheduleThreadInviteHandler, on a real (temp-file SQLite) GrifballContext: no database server, host or network.
/// Adds both captains and every commissioner to the reschedule's thread.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleThreadInviteHandlerTests
{
    private const ulong ThreadId = 888UL;

    private string _path = null!;
    private GrifballContext _context = null!;
    private IDiscordRestClient _discord = null!;
    private RescheduleThreadInviteHandler _handler = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-reschedule-invite-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        try
        {
            await _context.Database.EnsureCreatedAsync();
        }
        catch
        {
            await _context.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleThreadInviteHandler(_discord, NullLogger<RescheduleThreadInviteHandler>.Instance);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private async Task<User> AddUser(string name, long? xboxId = null, long? discordId = null, string? displayName = null)
    {
        var user = User.Register(name, displayName: displayName);
        if (xboxId is { } xuid)
            user.TieToXbox(XboxUser.FromXbox(xuid, $"GT_{name}"));
        if (discordId is { } discordUserId)
            user.LinkDiscord(DiscordUser.FromDiscord(discordUserId, name));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> AddTeamWithCaptain(Season season, string name, User captain)
    {
        var team = Team.Seed(seasonID: season.SeasonID, teamName: name);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        if (team.AddPlayer(captain.Id) is not TeamRosterResult.Done)
            throw new InvalidOperationException("Could not add the captain to the roster");
        await _context.SaveChangesAsync();
        if (team.SetCaptain(captain.Id) is not TeamCaptainResult.Appointed)
            throw new InvalidOperationException("Could not appoint the captain");
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<MatchReschedule> SeedMatchWithReschedule(User homeCaptain, User awayCaptain)
    {
        var season = Season.Seed(seasonName: "Reschedule season");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var home = await AddTeamWithCaptain(season, "Home", homeCaptain);
        var away = await AddTeamWithCaptain(season, "Away", awayCaptain);
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", homeCaptain.Id, DateTime.UtcNow);
        reschedule.AttachThread(ThreadId);
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        return reschedule;
    }

    private static OutboxMessage Message(int rescheduleId) => new()
    {
        Kind = RescheduleOutbox.ThreadInvite,
        Payload = JsonSerializer.Serialize(new ReschedulePayload(rescheduleId), OutboxJson.Options),
    };

    [Test]
    public async Task HandleAsync_BothCaptainsHaveDiscord_AddsBothAndPostsNoErrors()
    {
        var homeCaptain = await AddUser("homecap", 1001, 5001);
        var awayCaptain = await AddUser("awaycap", 1002, 5002);
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
        await _discord.Received(1).AddGuildThreadUserAsync(ThreadId, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.Received(1).AddGuildThreadUserAsync(ThreadId, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.DidNotReceive().SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_CaptainWithoutDiscord_ReportsItInTheThread_ByDisplayName()
    {
        var homeCaptain = await AddUser("homecap", 1001, 5001);
        var awayCaptain = await AddUser("awaycap", displayName: "Away Captain");
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(ThreadId,
            Arg.Is<MessageProperties>(m => m.Content!.Contains("Could not add Away Captain to thread, missing discord id.")),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_CaptainWithNoNameAtAll_IsNamedByUserId_NeverTheirLoginName()
    {
        var homeCaptain = await AddUser("homecap-login", 1001, 5001);
        var awayCaptain = await AddUser("awaycap-login");
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(ThreadId,
            Arg.Is<MessageProperties>(m => m.Content!.Contains($"Could not add user {awayCaptain.Id} to thread") && !m.Content.Contains("awaycap-login")),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_AddingACaptainThrows_IsReportedAndDoesNotStopTheOthers()
    {
        var homeCaptain = await AddUser("homecap", 1001, 5001);
        var awayCaptain = await AddUser("awaycap", 1002, 5002);
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);
        _discord.AddGuildThreadUserAsync(ThreadId, 5001UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).ThrowsAsync(new Exception("boom"));

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        await _discord.Received(1).AddGuildThreadUserAsync(ThreadId, 5002UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.Received(1).SendMessageAsync(ThreadId,
            // Named by their gamertag (ShownName: Xbox, else Discord, else display name), never their login name.
            Arg.Is<MessageProperties>(m => m.Content!.Contains("Could not add GT_homecap to thread, exception thrown")),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_IncludesCommissioners()
    {
        var homeCaptain = await AddUser("homecap", 1001, 5001);
        var awayCaptain = await AddUser("awaycap", 1002, 5002);
        var commissioner = await AddUser("commish", 1003, 5003);
        var role = await _context.Roles.SingleAsync(x => x.Name == "Commissioner");
        _context.UserRoles.Add(new UserRole { UserId = commissioner.Id, RoleId = role.Id });
        await _context.SaveChangesAsync();
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), _context, CancellationToken.None);

        await _discord.Received(1).AddGuildThreadUserAsync(ThreadId, 5003UL, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleAsync_AlreadyPostedErrors_IsDoneAgainWithoutCallingDiscord()
    {
        var homeCaptain = await AddUser("homecap", 1001, 5001);
        var awayCaptain = await AddUser("awaycap", 1002, 5002);
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);
        var message = Message(reschedule.MatchRescheduleID);
        message.Result = JsonSerializer.Serialize(new RescheduleInviteResult(true), OutboxJson.Options);

        var outcome = await _handler.HandleAsync(message, _context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }
}
