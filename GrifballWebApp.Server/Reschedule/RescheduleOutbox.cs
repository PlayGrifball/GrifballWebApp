using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using System.Text;

namespace GrifballWebApp.Server.Reschedule;

/// <summary>
/// A reschedule request's Discord messages, sent through the outbox in the request's group: its thread made in three
/// steps (the request post in the reschedules channel, the thread from it, the captains and commissioners added to
/// it), then the decision told in the thread.
/// </summary>
public static class RescheduleOutbox
{
    public const string RequestPost = "reschedule.request-post";
    public const string ThreadCreate = "reschedule.thread-create";
    public const string ThreadInvite = "reschedule.thread-invite";
    public const string DecisionPost = "reschedule.decision-post";

    public static string Group(int rescheduleId) => $"reschedule:{rescheduleId}";

    public static async Task EnqueueThreadAsync(this IOutbox outbox, int rescheduleId, string content, string threadName, CancellationToken ct)
    {
        var group = Group(rescheduleId);
        await outbox.EnqueueAsync(RequestPost, new RescheduleRequestPostPayload(rescheduleId, content), group, ct);
        await outbox.EnqueueAsync(ThreadCreate, new RescheduleThreadPayload(rescheduleId, threadName), group, ct);
        await outbox.EnqueueAsync(ThreadInvite, new ReschedulePayload(rescheduleId), group, ct);
    }

    public static Task EnqueueDecisionAsync(this IOutbox outbox, int rescheduleId, string content, CancellationToken ct)
        => outbox.EnqueueAsync(DecisionPost, new RescheduleDecisionPayload(rescheduleId, content), Group(rescheduleId), ct);

    public static IServiceCollection AddRescheduleOutboxHandlers(this IServiceCollection services)
    {
        return services
            .AddOutboxHandler<RescheduleRequestPostHandler>()
            .AddOutboxHandler<RescheduleThreadCreateHandler>()
            .AddOutboxHandler<RescheduleThreadInviteHandler>()
            .AddOutboxHandler<RescheduleDecisionPostHandler>();
    }
}

public sealed record ReschedulePayload(int RescheduleId);

public sealed record RescheduleRequestPostPayload(int RescheduleId, string Content);

public sealed record RescheduleThreadPayload(int RescheduleId, string ThreadName);

public sealed record RescheduleDecisionPayload(int RescheduleId, string Content);

public sealed record RescheduleRequestPostResult(ulong MessageId, bool Found);

public sealed record RescheduleInviteResult(bool ErrorsPosted);

/// <summary>Posts the request to the reschedules channel. On a retry, the post an earlier try made is found and kept.</summary>
public sealed class RescheduleRequestPostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options) : OutboxHandler<RescheduleRequestPostPayload>
{
    public override string Kind => RescheduleOutbox.RequestPost;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, RescheduleRequestPostPayload payload, GrifballContext context, CancellationToken ct)
    {
        var channelId = options.Value.ReschedulesChannel;
        var found = message.Attempts > 1 ? await discord.FindBotPostAsync(channelId, x => x.Content == payload.Content, ct) : null;
        var messageId = found?.Id ?? (await discord.SendMessageAsync(channelId, new MessageProperties { Content = payload.Content }, cancellationToken: ct)).Id;
        SetResult(message, new RescheduleRequestPostResult(messageId, found is not null));
        return OutboxOutcome.Done;
    }
}

/// <summary>
/// Makes the request's thread from its post and saves it on the request. A thread already saved is kept; on a retry
/// (or when the post was found rather than made) one already made from the post is taken.
/// </summary>
public sealed class RescheduleThreadCreateHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options) : OutboxHandler<RescheduleThreadPayload>
{
    public override string Kind => RescheduleOutbox.ThreadCreate;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, RescheduleThreadPayload payload, GrifballContext context, CancellationToken ct)
    {
        var reschedule = await context.MatchReschedules.SingleAsync(x => x.MatchRescheduleID == payload.RescheduleId, ct);
        if (reschedule.DiscordThreadID is not null)
            return OutboxOutcome.Done;

        var group = RescheduleOutbox.Group(payload.RescheduleId);
        var post = await context.SentResultAsync<RescheduleRequestPostResult>(group, RescheduleOutbox.RequestPost, ct)
            ?? throw new InvalidOperationException($"Reschedule {payload.RescheduleId} has no request post to make its thread from");

        var channelId = options.Value.ReschedulesChannel;
        if (message.Attempts > 1 || post.Found)
        {
            var posted = await discord.GetMessageAsync(channelId, post.MessageId, null, ct);
            if (posted.StartedThread is { } startedThread)
            {
                reschedule.DiscordThreadID = startedThread.Id;
                return OutboxOutcome.Done;
            }
        }

        var thread = await discord.CreateGuildThreadAsync(channelId, post.MessageId, new GuildThreadFromMessageProperties(payload.ThreadName), cancellationToken: ct);
        reschedule.DiscordThreadID = thread.Id;
        return OutboxOutcome.Done;
    }
}

/// <summary>
/// Adds both captains and every commissioner to the request's thread, and tells the thread who couldn't be added (once:
/// a retry after that is done).
/// </summary>
public sealed class RescheduleThreadInviteHandler(IDiscordRestClient discord, ILogger<RescheduleThreadInviteHandler> logger) : OutboxHandler<ReschedulePayload>
{
    public override string Kind => RescheduleOutbox.ThreadInvite;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, ReschedulePayload payload, GrifballContext context, CancellationToken ct)
    {
        if (GetResult<RescheduleInviteResult>(message)?.ErrorsPosted is true)
            return OutboxOutcome.Done;

        var reschedule = await context.MatchReschedules
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.HomeTeam.Captain.User)
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.AwayTeam.Captain.User)
            .AsSplitQuery()
            .SingleAsync(mr => mr.MatchRescheduleID == payload.RescheduleId, ct);
        var threadId = reschedule.DiscordThreadID ?? throw new InvalidOperationException($"Reschedule {payload.RescheduleId} has no thread");

        var sb = new StringBuilder();
        await AddCaptainToThread(reschedule.SeasonMatch.HomeTeam, threadId, sb, ct);
        await AddCaptainToThread(reschedule.SeasonMatch.AwayTeam, threadId, sb, ct);

        var commisioners = await context.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Commissioner"))
            .ToListAsync(ct);
        foreach (var commisioner in commisioners)
        {
            await AddUserToThread(commisioner, threadId, sb, ct);
        }
        var finalErrors = sb.ToString();
        if (!string.IsNullOrEmpty(finalErrors))
        {
            await discord.SendMessageAsync(threadId, new MessageProperties { Content = finalErrors }, cancellationToken: ct);
            SetResult(message, new RescheduleInviteResult(true));
        }
        return OutboxOutcome.Done;
    }

    private async Task AddCaptainToThread(Team? team, ulong threadId, StringBuilder sb, CancellationToken ct)
    {
        if (team is null)
        {
            sb.AppendLine("Could not add a captain to thread, the match is missing a team.");
            return;
        }

        if (team.Captain is null)
        {
            sb.AppendLine($"Could not add {team.TeamName} captain to thread, team has no captain.");
            return;
        }

        await AddUserToThread(team.Captain.User, threadId, sb, ct);
    }

    private async Task AddUserToThread(User user, ulong threadId, StringBuilder sb, CancellationToken ct)
    {
        var discordUserId = (ulong?)user.DiscordUserID;
        if (discordUserId.HasValue)
        {
            // Adding a member again does nothing, so a retry adds them all again.
            try
            {
                await discord.AddGuildThreadUserAsync(threadId, discordUserId.Value, null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested is false)
            {
                logger.LogError(ex, "Failed to add {DiscordUserId} to thread {ThreadId}", discordUserId.Value, threadId);
                sb.Append($"Could not add {user.DisplayName ?? user.UserName} to thread, exception thrown");
            }
        }
        else
        {
            sb.AppendLine($"Could not add {user.DisplayName ?? user.UserName} to thread, missing discord id.");
        }
    }
}

/// <summary>
/// Tells the request's thread the decision. Nothing when the request has no thread (none was asked for); on a retry,
/// not again if an earlier try's message is there.
/// </summary>
public sealed class RescheduleDecisionPostHandler(IDiscordRestClient discord) : OutboxHandler<RescheduleDecisionPayload>
{
    public override string Kind => RescheduleOutbox.DecisionPost;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, RescheduleDecisionPayload payload, GrifballContext context, CancellationToken ct)
    {
        var threadId = await context.MatchReschedules
            .Where(x => x.MatchRescheduleID == payload.RescheduleId)
            .Select(x => x.DiscordThreadID)
            .SingleAsync(ct);
        if (threadId is null)
            return OutboxOutcome.Done;

        if (message.Attempts > 1 && await discord.FindBotPostAsync(threadId.Value, x => x.Content == payload.Content, ct) is not null)
            return OutboxOutcome.Done;

        try
        {
            await discord.SendMessageAsync(threadId.Value, new MessageProperties { Content = payload.Content }, cancellationToken: ct);
        }
        catch (Exception ex) when (DiscordPosts.IsNotFound(ex))
        {
            // The thread was deleted: nobody left to tell.
        }
        return OutboxOutcome.Done;
    }
}
