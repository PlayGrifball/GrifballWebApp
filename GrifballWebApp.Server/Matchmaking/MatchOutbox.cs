using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using System.Net;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>
/// Matchmaking's Discord messages, sent through the outbox: queued with the match change they belong to, in the
/// match's group, so they go out in order. A match is announced in four steps (the "Match Created" post, its thread,
/// the players invited to it, the vote message in it), and ended or canceled in two (the post, then the thread
/// closed).
/// </summary>
public static class MatchOutbox
{
    public const string CreatedPost = "matchmaking.created-post";
    public const string ThreadCreate = "matchmaking.thread-create";
    public const string ThreadInvite = "matchmaking.thread-invite";
    public const string VotePost = "matchmaking.vote-post";
    public const string EndedPost = "matchmaking.ended-post";
    public const string CanceledPost = "matchmaking.canceled-post";
    public const string ThreadClose = "matchmaking.thread-close";

    /// <summary>
    /// How long a match's announcement keeps being tried before the match is canceled and its players go back to the
    /// queue. Long enough to ride out a Discord blip or a short outage, short enough that the players, who can't queue
    /// while it is active, are not left waiting long on a match nobody can see.
    /// </summary>
    public static readonly TimeSpan AnnounceMaxAge = TimeSpan.FromMinutes(5);

    /// <summary>How long the thread close waits between its notice and deleting the thread.</summary>
    public static readonly TimeSpan ThreadDeleteDelay = TimeSpan.FromSeconds(10);

    public static string Group(int matchId) => $"match:{matchId}";

    /// <param name="mayBePosted">The match may have been announced in part before (by the app before the outbox): look for its post first.</param>
    public static async Task EnqueueAnnouncementAsync(this IOutbox outbox, int matchId, bool mayBePosted, CancellationToken ct)
    {
        var group = Group(matchId);
        await outbox.EnqueueAsync(CreatedPost, new MatchCreatedPayload(matchId, mayBePosted), group, ct);
        await outbox.EnqueueAsync(ThreadCreate, new MatchPayload(matchId), group, ct);
        await outbox.EnqueueAsync(ThreadInvite, new MatchPayload(matchId), group, ct);
        await outbox.EnqueueAsync(VotePost, new MatchPayload(matchId), group, ct);
    }

    public static async Task EnqueueEndedAsync(this IOutbox outbox, MatchEndedPayload ended, CancellationToken ct)
    {
        await outbox.EnqueueAsync(EndedPost, ended, Group(ended.MatchId), ct);
        await outbox.EnqueueAsync(ThreadClose, new MatchPayload(ended.MatchId), Group(ended.MatchId), ct);
    }

    public static async Task EnqueueCanceledAsync(this IOutbox outbox, int matchId, bool unannounced, CancellationToken ct)
    {
        await outbox.EnqueueAsync(CanceledPost, new MatchCanceledPayload(matchId, unannounced), Group(matchId), ct);
        await outbox.EnqueueAsync(ThreadClose, new MatchPayload(matchId), Group(matchId), ct);
    }

    public static IServiceCollection AddMatchmakingOutboxHandlers(this IServiceCollection services)
    {
        return services
            .AddOutboxHandler<MatchCreatedPostHandler>()
            .AddOutboxHandler<MatchThreadCreateHandler>()
            .AddOutboxHandler<MatchThreadInviteHandler>()
            .AddOutboxHandler<MatchVotePostHandler>()
            .AddOutboxHandler<MatchEndedPostHandler>()
            .AddOutboxHandler<MatchCanceledPostHandler>()
            .AddOutboxHandler<MatchThreadCloseHandler>();
    }

    internal static async Task<MatchedMatch> LoadMatchAsync(GrifballContext context, int matchId, CancellationToken ct)
    {
        return await context.MatchedMatches
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.HomeTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.XboxUser)
            .Include(x => x.AwayTeam.Players)
                .ThenInclude(x => x.User.DiscordUser)
            .AsSplitQuery()
            .SingleAsync(x => x.Id == matchId, ct);
    }

    internal static IEnumerable<MatchedPlayer> PlayersOf(MatchedMatch match) => match.HomeTeam.Players.Concat(match.AwayTeam.Players);

    internal static bool IsNotFound(Exception ex) => ex is RestException { StatusCode: HttpStatusCode.NotFound };
}

public sealed record MatchPayload(int MatchId);

public sealed record MatchCreatedPayload(int MatchId, bool MayBePosted);

/// <summary>The "Match Created" post: <paramref name="Found"/> when it was found already posted rather than posted.</summary>
public sealed record MatchCreatedResult(ulong MessageId, bool Found);

/// <summary>A match's end post, worked out when it ended (the MMR changes can't be worked out again later).</summary>
public sealed record MatchEndedPayload(int MatchId, int WinnerId, string Duration, string HomeMmrChanges, string AwayMmrChanges);

/// <param name="Unannounced">Canceled because it could not be announced, its players requeued; otherwise by vote or for being stale.</param>
public sealed record MatchCanceledPayload(int MatchId, bool Unannounced);

public sealed record ThreadCloseResult(bool NoticeSent);

/// <summary>The bot's posts in the log channel, for a retry to find what an earlier try posted.</summary>
public abstract class MatchLogChannelHandler<TPayload>(IDiscordRestClient discord, IOptions<DiscordOptions> options) : OutboxHandler<TPayload>
{
    protected IDiscordRestClient Discord { get; } = discord;

    protected ulong LogChannel => options.Value.LogChannel;

    /// <summary>The bot's latest post in the log channel with an embed matching <paramref name="embed"/>, among the last 50.</summary>
    protected async Task<IDiscordRestMessage?> FindBotPostAsync(Func<IDiscordEmbed, bool> embed, CancellationToken ct)
    {
        var me = await Discord.GetCurrentUserAsync(null, ct);
        var messages = await Discord.GetMessagesAsync(LogChannel, new PaginationProperties<ulong>()
        {
            BatchSize = 50,
        }).Take(50).ToListAsync(ct);
        return messages
            .Where(x => x.Author.Id == me.Id)
            .FirstOrDefault(x => x.Embeds.Any(embed));
    }

    protected static bool HasMatchId(IDiscordEmbed embed, int matchId)
        => embed.Fields.Any(f => f.Name == "Match ID" && f.Value == matchId.ToString());
}

/// <summary>A step of a match's announcement: given up on, the match is canceled and its players requeued.</summary>
public abstract class MatchAnnouncementHandler<TPayload>(IDiscordRestClient discord, IOptions<DiscordOptions> options, ILogger logger)
    : MatchLogChannelHandler<TPayload>(discord, options)
    where TPayload : notnull
{
    protected ILogger Logger { get; } = logger;

    public override TimeSpan MaxAge => MatchOutbox.AnnounceMaxAge;

    protected abstract int MatchIdOf(TPayload payload);

    protected override async Task OnDeadLetteredAsync(OutboxMessage message, TPayload payload, GrifballContext context, IOutbox outbox, CancellationToken ct)
    {
        var match = await MatchOutbox.LoadMatchAsync(context, MatchIdOf(payload), ct);
        if (match.Active is false)
            return;

        var requeued = await MatchRequeue.CancelAndRequeueAsync(context, match, ct);
        await outbox.EnqueueCanceledAsync(match.Id, unannounced: true, ct);
        Logger.LogWarning("Canceled match {MatchId}: it could not be announced on Discord within {Deadline}; {Count} players are back in the queue",
            match.Id, MaxAge, requeued);
    }
}

/// <summary>Posts "Match Created" to the log channel. On a retry, the post an earlier try made is found and kept.</summary>
public sealed class MatchCreatedPostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options, ILogger<MatchCreatedPostHandler> logger)
    : MatchAnnouncementHandler<MatchCreatedPayload>(discord, options, logger)
{
    public override string Kind => MatchOutbox.CreatedPost;

    protected override int MatchIdOf(MatchCreatedPayload payload) => payload.MatchId;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchCreatedPayload payload, GrifballContext context, CancellationToken ct)
    {
        var match = await MatchOutbox.LoadMatchAsync(context, payload.MatchId, ct);

        IDiscordRestMessage? found = null;
        if (message.Attempts > 1 || payload.MayBePosted)
            found = await FindBotPostAsync(e => e.Title is "Match Created" && HasMatchId(e, match.Id), ct);

        var messageId = found?.Id ?? (await Discord.SendMessageAsync(LogChannel, MatchMessages.Created(match), null, ct)).Id;
        SetResult(message, new MatchCreatedResult(messageId, found is not null));
        return OutboxOutcome.Done;
    }
}

/// <summary>
/// Makes the match's thread from its "Match Created" post, and saves it on the match. A thread already saved is kept;
/// on a retry (or when the post was found rather than made) one already made from the post is taken.
/// </summary>
public sealed class MatchThreadCreateHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options, ILogger<MatchThreadCreateHandler> logger)
    : MatchAnnouncementHandler<MatchPayload>(discord, options, logger)
{
    public override string Kind => MatchOutbox.ThreadCreate;

    protected override int MatchIdOf(MatchPayload payload) => payload.MatchId;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchPayload payload, GrifballContext context, CancellationToken ct)
    {
        var match = await context.MatchedMatches.SingleAsync(x => x.Id == payload.MatchId, ct);
        if (match.ThreadID is not null)
            return OutboxOutcome.Done;

        var group = MatchOutbox.Group(match.Id);
        var createdResult = await context.Outbox
            .Where(x => x.GroupKey == group && x.Kind == MatchOutbox.CreatedPost && x.Status == OutboxStatus.Sent)
            .OrderByDescending(x => x.Sequence)
            .Select(x => x.Result)
            .FirstOrDefaultAsync(ct);
        var created = createdResult is null ? null : System.Text.Json.JsonSerializer.Deserialize<MatchCreatedResult>(createdResult, OutboxJson.Options);
        if (created is null)
            throw new InvalidOperationException($"Match {match.Id} has no Match Created post to make its thread from");

        if (message.Attempts > 1 || created.Found)
        {
            var post = await Discord.GetMessageAsync(LogChannel, created.MessageId, null, ct);
            if (post.StartedThread is { } startedThread)
            {
                match.ThreadID = startedThread.Id;
                return OutboxOutcome.Done;
            }
        }

        var thread = await Discord.CreateGuildThreadAsync(LogChannel, created.MessageId, new GuildThreadFromMessageProperties($"Match #{match.Id}")
        {
            Name = $"Match #{match.Id}",
            AutoArchiveDuration = ThreadArchiveDuration.OneHour,
        }, null, ct);
        match.ThreadID = thread.Id;
        return OutboxOutcome.Done;
    }
}

/// <summary>Adds the match's players to its thread. One who can't be added (left the guild) is logged and left out.</summary>
public sealed class MatchThreadInviteHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options, ILogger<MatchThreadInviteHandler> logger)
    : MatchAnnouncementHandler<MatchPayload>(discord, options, logger)
{
    public override string Kind => MatchOutbox.ThreadInvite;

    protected override int MatchIdOf(MatchPayload payload) => payload.MatchId;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchPayload payload, GrifballContext context, CancellationToken ct)
    {
        var match = await MatchOutbox.LoadMatchAsync(context, payload.MatchId, ct);
        var threadId = match.ThreadID ?? throw new InvalidOperationException($"Match {match.Id} has no thread");

        var discordUsers = MatchOutbox.PlayersOf(match).Select(x => x.User.DiscordUser)
            .Where(x => x != null)
            .ToArray();
        foreach (var user in discordUsers)
        {
            // Adding a member again does nothing, so a retry adds them all again.
            try
            {
                await Discord.AddGuildThreadUserAsync(threadId, (ulong)user!.DiscordUserID, null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || ct.IsCancellationRequested is false)
            {
                Logger.LogWarning(ex, "Failed to add user to thread {UserId}", user!.DiscordUserID);
            }
        }
        return OutboxOutcome.Done;
    }
}

/// <summary>Posts the vote message in the match's thread and saves it on the match; nothing when one is saved already.</summary>
public sealed class MatchVotePostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options, ILogger<MatchVotePostHandler> logger)
    : MatchAnnouncementHandler<MatchPayload>(discord, options, logger)
{
    public override string Kind => MatchOutbox.VotePost;

    protected override int MatchIdOf(MatchPayload payload) => payload.MatchId;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchPayload payload, GrifballContext context, CancellationToken ct)
    {
        var match = await MatchOutbox.LoadMatchAsync(context, payload.MatchId, ct);
        if (match.VoteMessageID is not null)
            return OutboxOutcome.Done;
        var threadId = match.ThreadID ?? throw new InvalidOperationException($"Match {match.Id} has no thread");

        var kickable = MatchOutbox.PlayersOf(match).Select(x => x.User).Distinct().ToArray();
        var voteMessage = await Discord.SendMessageAsync(threadId, QueueService.CreateVoteMessage(match.Id, kickable), null, ct);
        match.VoteMessageID = voteMessage.Id;
        return OutboxOutcome.Done;
    }
}

/// <summary>Posts "Match Ended" to the log channel; on a retry, not again if an earlier try's post is there.</summary>
public sealed class MatchEndedPostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options)
    : MatchLogChannelHandler<MatchEndedPayload>(discord, options)
{
    public override string Kind => MatchOutbox.EndedPost;

    // The thread is closed even if the post never made it.
    public override bool DeadLetterSkipsGroup => false;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchEndedPayload payload, GrifballContext context, CancellationToken ct)
    {
        if (message.Attempts > 1 && await FindBotPostAsync(e => e.Title is "Match Ended" && HasMatchId(e, payload.MatchId), ct) is not null)
            return OutboxOutcome.Done;

        await Discord.SendMessageAsync(LogChannel, MatchMessages.Ended(payload), null, ct);
        return OutboxOutcome.Done;
    }
}

/// <summary>Posts "Match Canceled" to the log channel; on a retry, not again if an earlier try's post is there.</summary>
public sealed class MatchCanceledPostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options)
    : MatchLogChannelHandler<MatchCanceledPayload>(discord, options)
{
    public override string Kind => MatchOutbox.CanceledPost;

    public override bool DeadLetterSkipsGroup => false;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchCanceledPayload payload, GrifballContext context, CancellationToken ct)
    {
        var description = $"Match #{payload.MatchId} has been canceled";
        if (message.Attempts > 1 && await FindBotPostAsync(e => e.Title is "Match Canceled" && e.Description?.StartsWith(description, StringComparison.Ordinal) is true, ct) is not null)
            return OutboxOutcome.Done;

        await Discord.SendMessageAsync(LogChannel, MatchMessages.Canceled(payload), null, ct);
        return OutboxOutcome.Done;
    }
}

/// <summary>
/// Tells the match's thread it is done, then deletes it <see cref="MatchOutbox.ThreadDeleteDelay"/> later (the
/// message waits in the outbox in between). A thread already gone is done with; a match without one has nothing to
/// close.
/// </summary>
public sealed class MatchThreadCloseHandler(IDiscordRestClient discord, TimeProvider time) : OutboxHandler<MatchPayload>
{
    public const string Notice = "Match Completed. This thread will be deleted in about 10 seconds...";

    public override string Kind => MatchOutbox.ThreadClose;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, MatchPayload payload, GrifballContext context, CancellationToken ct)
    {
        var threadId = await context.MatchedMatches.Where(x => x.Id == payload.MatchId).Select(x => x.ThreadID).SingleAsync(ct);
        if (threadId is null)
            return OutboxOutcome.Done;

        if (GetResult<ThreadCloseResult>(message)?.NoticeSent is not true)
        {
            try
            {
                await discord.SendMessageAsync(threadId.Value, new() { Content = Notice }, null, ct);
            }
            catch (Exception ex) when (MatchOutbox.IsNotFound(ex))
            {
                return OutboxOutcome.Done;
            }
            SetResult(message, new ThreadCloseResult(true));
            return OutboxOutcome.ContinueLater(time.GetUtcNow().UtcDateTime + MatchOutbox.ThreadDeleteDelay);
        }

        try
        {
            // Needs permission to manage threads
            await discord.DeleteChannelAsync(threadId.Value, null, ct);
        }
        catch (Exception ex) when (MatchOutbox.IsNotFound(ex))
        {
        }
        return OutboxOutcome.Done;
    }
}

/// <summary>Cancels a match nobody could see, and puts its players back in the queue.</summary>
public static class MatchRequeue
{
    /// <summary>
    /// The match canceled, and its players put back at the front of the queue, in the order they had in it. Their join
    /// times went with their queue rows, but they were the longest queued (CreateMatches takes those, dealing them out
    /// home, away, home...), so they go back just ahead of everyone queued now. Not saved; returns how many went back.
    /// </summary>
    public static async Task<int> CancelAndRequeueAsync(GrifballContext context, MatchedMatch match, CancellationToken ct)
    {
        match.Active = false;

        var home = match.HomeTeam.Players.OrderBy(x => x.Id).ToList();
        var away = match.AwayTeam.Players.OrderBy(x => x.Id).ToList();
        var inQueueOrder = Enumerable.Range(0, Math.Max(home.Count, away.Count))
            .SelectMany(i => new[] { home.ElementAtOrDefault(i), away.ElementAtOrDefault(i) })
            .Where(x => x is not null && x.Kicked is false)
            .Select(x => x!.UserID)
            .Distinct()
            .ToList();

        var queued = await context.QueuedPlayer.Select(x => new { x.UserID, x.JoinedAt }).ToListAsync(ct);
        var queuedIds = queued.Select(x => x.UserID).ToHashSet();
        var front = queued.Count == 0 || match.StartedAt <= queued.Min(x => x.JoinedAt)
            ? match.StartedAt
            : queued.Min(x => x.JoinedAt);
        var requeue = inQueueOrder.Where(x => queuedIds.Contains(x) is false).ToList();
        context.QueuedPlayer.AddRange(requeue.Select((userId, index) => new QueuedPlayer
        {
            UserID = userId,
            JoinedAt = front.AddSeconds(index - requeue.Count),
        }));
        return requeue.Count;
    }
}

/// <summary>Matchmaking's posts to the log channel, as they have always read.</summary>
public static class MatchMessages
{
    public static MessageProperties Created(MatchedMatch matchedMatch)
    {
        var matchEmbed = new EmbedProperties()
        {
            Title = "Match Created",
            Description = $"Match #{matchedMatch.Id} has been created successfully",
            Fields =
            [
                new EmbedFieldProperties()
                {
                    Name = "Match ID",
                    Value = matchedMatch.Id.ToString(),
                    Inline = true,
                },
                new EmbedFieldProperties()
                {
                    Name = "Players",
                    Value = (matchedMatch.HomeTeam.Players.Count + matchedMatch.AwayTeam.Players.Count).ToString(),
                    Inline = true,
                },
            ],
        };

        return new MessageProperties()
        {
            Content = $"Match #{matchedMatch.Id} has been created successfully",
            Embeds = [matchEmbed],
        };
    }

    public static MessageProperties Ended(MatchEndedPayload ended)
    {
        return new MessageProperties()
        {
            Embeds =
            [
                new EmbedProperties()
                {
                    Title = "Match Ended",
                    Description = $"Match #{ended.MatchId} has ended. Team {ended.WinnerId} is the winner",
                    Color = new Color(0, 255, 0), // Green
                    Fields =
                    [
                        new EmbedFieldProperties()
                        {
                            Name = "Match ID",
                            Value = ended.MatchId.ToString(),
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Winning Team",
                            Value = ended.WinnerId.ToString(),
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Duration",
                            Value = ended.Duration,
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Home Team MMR Changes",
                            Value = ended.HomeMmrChanges,
                            Inline = true,
                        },
                        new EmbedFieldProperties()
                        {
                            Name = "Away Team MMR Changes",
                            Value = ended.AwayMmrChanges,
                            Inline = true,
                        },
                    ],
                },
            ],
        };
    }

    public static MessageProperties Canceled(MatchCanceledPayload canceled)
    {
        if (canceled.Unannounced)
        {
            return new MessageProperties()
            {
                Embeds =
                [
                    new EmbedProperties()
                    {
                        Title = "Match Canceled",
                        Description = $"Match #{canceled.MatchId} has been canceled: it could not be set up on Discord. Its players are back in the queue.",
                        Color = new Color(255, 0, 0), // Red
                    },
                ],
            };
        }

        return new MessageProperties()
        {
            Embeds =
            [
                new EmbedProperties()
                {
                    Title = "Match Canceled",
                    Description = $"Match #{canceled.MatchId} has been canceled.",
                    Color = new Color(255, 0, 0), // Red
                    Fields =
                    [
                        new EmbedFieldProperties()
                        {
                            Name = "Match ID",
                            Value = canceled.MatchId.ToString(),
                            Inline = true,
                        },
                    ],
                },
            ],
        };
    }
}
