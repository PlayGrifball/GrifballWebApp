using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Outbox;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams;

/// <summary>
/// The draft channel's messages, sent through the outbox: queued by <see cref="TeamService"/> in the transaction of the
/// draft change they announce, worked out there (names and the on-deck pick menus as of that change), and sent in
/// order in the season's group.
/// </summary>
public static class DraftOutbox
{
    public const string Post = "draft.post";
    public const string OnDeck = "draft.on-deck";

    public static string Group(int seasonId) => $"draft:{seasonId}";

    public static IServiceCollection AddDraftOutboxHandlers(this IServiceCollection services)
        => services
            .AddOutboxHandler<DraftPostHandler>()
            .AddOutboxHandler<DiscordOnDeckMessages>();

    public static Task EnqueuePostAsync(this IOutbox outbox, int seasonId, string content, CancellationToken ct)
        => outbox.EnqueueAsync(Post, new DraftPostPayload(seasonId, content), Group(seasonId), ct);

    public static async Task EnqueuePlayerAddedAsync(this IOutbox outbox, GrifballContext context, TeamService teams, AddPlayerToTeamRequestDto dto, CancellationToken ct)
    {
        await outbox.EnqueuePostAsync(dto.SeasonID, $"{await Name(context, dto.PersonID, ct)} has been added to {await Name(context, dto.CaptainID, ct)}'s team", ct);
        await outbox.EnqueueOnDeckAsync(context, teams, dto.SeasonID, ct);
    }

    public static async Task EnqueuePlayerRemovedAsync(this IOutbox outbox, GrifballContext context, TeamService teams, RemovePlayerFromTeamRequestDto dto, CancellationToken ct)
    {
        await outbox.EnqueuePostAsync(dto.SeasonID, $"{await Name(context, dto.PersonID, ct)} has returned to the player pool", ct);
        await outbox.EnqueueOnDeckAsync(context, teams, dto.SeasonID, ct);
    }

    public static async Task EnqueuePlayerMovedAsync(this IOutbox outbox, GrifballContext context, TeamService teams, MovePlayerToTeamRequestDto dto, CancellationToken ct)
    {
        await outbox.EnqueuePostAsync(dto.SeasonID, $"{await Name(context, dto.PersonID, ct)} has been moved from {await Name(context, dto.PreviousCaptainID, ct)}'s team to {await Name(context, dto.NewCaptainID, ct)}'s team", ct);
        await outbox.EnqueueOnDeckAsync(context, teams, dto.SeasonID, ct);
    }

    public static async Task EnqueueLockChangedAsync(this IOutbox outbox, GrifballContext context, TeamService teams, int seasonId, bool locked, CancellationToken ct)
    {
        if (locked)
        {
            await outbox.EnqueuePostAsync(seasonId, "Captains are now locked", ct);
            await outbox.EnqueueOnDeckAsync(context, teams, seasonId, ct);
        }
        else
        {
            await outbox.EnqueuePostAsync(seasonId, "Captains are now unlocked. Draft is paused until adjustments are complete", ct);
        }
    }

    /// <summary>
    /// Who is on deck, and the pick menus of the player pool (25 players a menu, 5 menus a message), as the saved changes
    /// of <paramref name="context"/> have them; or that the draft is over, or no team was found.
    /// </summary>
    public static async Task EnqueueOnDeckAsync(this IOutbox outbox, GrifballContext context, TeamService teams, int seasonId, CancellationToken ct)
    {
        var onDeck = await teams.OnDeck(seasonId, ct);
        if (onDeck is null)
        {
            await outbox.EnqueueAsync(OnDeck, new DraftOnDeckPayload(seasonId, [new DraftMessage("Failed to find team", [])]), Group(seasonId), ct);
            return;
        }

        var pool = (await teams.GetPlayerPool(seasonId, ct)).OrderBy(x => x.Name).ToList();
        if (pool.Count == 0)
        {
            await outbox.EnqueueAsync(OnDeck, new DraftOnDeckPayload(seasonId, [new DraftMessage("Draft has been completed", [])]), Group(seasonId), ct);
            return;
        }

        // Split pool into groups of 25
        var menus = pool
            .Select((player, index) => new { player, index })
            .GroupBy(x => x.index / 25)
            .Select(g => g.Select(x => x.player).ToList())
            .Select((smallerPool, index) =>
            {
                var startName = smallerPool.First().Name;
                var startNameShort = startName.Substring(0, Math.Min(4, startName.Length));

                var lastName = smallerPool.Last().Name;
                var lastNameShort = lastName.Substring(0, Math.Min(4, lastName.Length));

                return new DraftMenu(
                    DiscordStringMenuContants.DraftPickWithParams(seasonId, onDeck.Captain.UserID, index),
                    $"Make pick - {startNameShort} - {lastNameShort}",
                    [.. smallerPool.Select(x => new DraftMenuOption(x.Name, x.PersonID.ToString()))]);
            })
            .ToList();

        // Split menus into groups of 5: the first message says who is on deck
        var onDeckName = await Name(context, onDeck.Captain.UserID, ct);
        var messages = menus
            .Select((menu, index) => new { menu, index })
            .GroupBy(x => x.index / 5)
            .Select((g, index) => new DraftMessage(index == 0 ? $"{onDeckName} is on deck" : null, [.. g.Select(x => x.menu)]))
            .ToList();
        await outbox.EnqueueAsync(OnDeck, new DraftOnDeckPayload(seasonId, messages), Group(seasonId), ct);
    }

    private static Task<string> Name(GrifballContext context, int userId, CancellationToken ct) => DiscordMention.ForUser(context, userId, ct);
}

public sealed record DraftPostPayload(int SeasonId, string Content);

public sealed record DraftMenuOption(string Label, string Value);

public sealed record DraftMenu(string CustomId, string Placeholder, List<DraftMenuOption> Options);

/// <summary>One message to the draft channel: its content (none for a message of more pick menus), and its menus.</summary>
public sealed record DraftMessage(string? Content, List<DraftMenu> Menus);

public sealed record DraftOnDeckPayload(int SeasonId, List<DraftMessage> Messages);

public sealed record DraftOnDeckResult(int Posted);

/// <summary>Finds what an earlier try sent: the bot's message in the draft channel with that content, since it was queued.</summary>
public abstract class DraftChannelHandler<TPayload>(IDiscordRestClient discord, IOptions<DiscordOptions> options) : OutboxHandler<TPayload>
{
    protected IDiscordRestClient Discord { get; } = discord;

    protected ulong DraftChannel => options.Value.DraftChannel;

    /// <summary>Sends <paramref name="message"/>, unless (on a retry) the bot already sent one with its content after it was queued.</summary>
    protected async Task SendOnceAsync(OutboxMessage outboxMessage, MessageProperties message, CancellationToken ct)
    {
        if (outboxMessage.Attempts > 1 && message.Content is { } content)
        {
            // Draft messages repeat ("Captains are now locked"): only one sent since this was queued counts.
            var since = new DateTimeOffset(outboxMessage.CreatedAt, TimeSpan.Zero).AddSeconds(-5);
            if (await Discord.FindBotPostAsync(DraftChannel, x => x.Content == content && x.CreatedAt >= since, ct, count: 20) is not null)
                return;
        }
        await Discord.SendMessageAsync(DraftChannel, message, cancellationToken: ct);
    }
}

public sealed class DraftPostHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options) : DraftChannelHandler<DraftPostPayload>(discord, options)
{
    public override string Kind => DraftOutbox.Post;

    // The on-deck message after it still goes.
    public override bool DeadLetterSkipsGroup => false;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, DraftPostPayload payload, GrifballContext context, CancellationToken ct)
    {
        await SendOnceAsync(message, new MessageProperties().WithContent(payload.Content), ct);
        return OutboxOutcome.Done;
    }
}

/// <summary>
/// The on-deck messages (<see cref="DraftOutbox.EnqueueOnDeckAsync"/>): sent one at a time, keeping how many went (in the result) between them, so a retry carries on
/// from the first one not sent.
/// </summary>
public sealed class DiscordOnDeckMessages(IDiscordRestClient discord, IOptions<DiscordOptions> options, TimeProvider time) : DraftChannelHandler<DraftOnDeckPayload>(discord, options)
{
    public override string Kind => DraftOutbox.OnDeck;

    public override bool DeadLetterSkipsGroup => false;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, DraftOnDeckPayload payload, GrifballContext context, CancellationToken ct)
    {
        var posted = GetResult<DraftOnDeckResult>(message)?.Posted ?? 0;
        if (posted >= payload.Messages.Count)
            return OutboxOutcome.Done;

        await SendOnceAsync(message, Render(payload.Messages[posted]), ct);
        posted++;
        SetResult(message, new DraftOnDeckResult(posted));
        return posted < payload.Messages.Count ? OutboxOutcome.ContinueLater(time.GetUtcNow().UtcDateTime) : OutboxOutcome.Done;
    }

    public static MessageProperties Render(DraftMessage draftMessage)
    {
        var message = new MessageProperties();
        if (draftMessage.Menus.Count > 0)
        {
            message = message.WithComponents(draftMessage.Menus.Select(menu => new StringMenuProperties(menu.CustomId)
            {
                Placeholder = menu.Placeholder,
                CustomId = menu.CustomId,
                Options = [.. menu.Options.Select(x => new StringMenuSelectOptionProperties(x.Label, x.Value))],
            }).ToList());
        }
        if (draftMessage.Content is not null)
            message = message.WithContent(draftMessage.Content);
        return message;
    }
}
