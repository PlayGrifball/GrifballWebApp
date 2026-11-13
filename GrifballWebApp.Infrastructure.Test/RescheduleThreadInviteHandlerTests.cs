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
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// RescheduleThreadInviteHandler, on a real (temp-file SQLite) GrifballContext: no database server, host or network.
/// Adds both captains and every commissioner to the reschedule's thread.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RescheduleThreadInviteHandlerTests : SqliteContextFixture
{
    private const ulong ThreadId = 888UL;

    private IDiscordRestClient _discord = null!;
    private RescheduleThreadInviteHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _discord = Substitute.For<IDiscordRestClient>();
        _handler = new RescheduleThreadInviteHandler(_discord, NullLogger<RescheduleThreadInviteHandler>.Instance);
    }

    private async Task<User> AddUser(string name, long? xboxId = null, long? discordId = null, string? displayName = null)
    {
        var user = User.Register(name, displayName: displayName);
        if (xboxId is { } xuid)
            user.TieToXbox(XboxUser.FromXbox(xuid, $"GT_{name}"));
        if (discordId is { } discordUserId)
            user.LinkDiscord(DiscordUser.FromDiscord(discordUserId, name));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> AddTeamWithCaptain(Season season, string name, User captain)
    {
        var team = Team.Seed(seasonID: season.SeasonID, teamName: name);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        if (team.AddPlayer(captain.Id) is not TeamRosterResult.Done)
            throw new InvalidOperationException("Could not add the captain to the roster");
        await Context.SaveChangesAsync();
        if (team.SetCaptain(captain.Id) is not TeamCaptainResult.Appointed)
            throw new InvalidOperationException("Could not appoint the captain");
        await Context.SaveChangesAsync();
        return team;
    }

    private async Task<MatchReschedule> SeedMatchWithReschedule(User homeCaptain, User awayCaptain)
    {
        var season = Season.Seed(seasonName: "Reschedule season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = await AddTeamWithCaptain(season, "Home", homeCaptain);
        var away = await AddTeamWithCaptain(season, "Away", awayCaptain);
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", homeCaptain.Id, DateTime.UtcNow);
        reschedule.AttachThread(ThreadId);
        Context.MatchReschedules.Add(reschedule);
        await Context.SaveChangesAsync();
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

        var outcome = await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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
        var role = await Context.Roles.SingleAsync(x => x.Name == "Commissioner");
        Context.UserRoles.Add(new UserRole { UserId = commissioner.Id, RoleId = role.Id });
        await Context.SaveChangesAsync();
        var reschedule = await SeedMatchWithReschedule(homeCaptain, awayCaptain);

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

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

        var outcome = await _handler.HandleAsync(message, Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outcome, Is.EqualTo(OutboxOutcome.Done));
            Assert.That(_discord.ReceivedCalls(), Is.Empty);
        }
    }

    [Test]
    public async Task HandleAsync_AMatchMissingATeam_AndATeamWithNoCaptain_AreReportedInTheThread()
    {
        var season = Season.Seed(seasonName: "Reschedule season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Captainless");
        Context.Teams.Add(away);
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, null, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        var requester = await AddUser("requester");
        var reschedule = MatchReschedule.Request(match.SeasonMatchID, null, null, "Can't make it", requester.Id, new DateTime(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc));
        reschedule.AttachThread(ThreadId);
        Context.MatchReschedules.Add(reschedule);
        await Context.SaveChangesAsync();

        await _handler.HandleAsync(Message(reschedule.MatchRescheduleID), Context, CancellationToken.None);

        await _discord.Received(1).SendMessageAsync(ThreadId,
            Arg.Is<MessageProperties>(m => m.Content!.Contains("Could not add a captain to thread, the match is missing a team.")
                && m.Content.Contains("Could not add Captainless captain to thread, team has no captain.")),
            Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        await _discord.DidNotReceiveWithAnyArgs().AddGuildThreadUserAsync(default, default, default, default);
    }
}
