using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Outbox;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Events;

/// <summary>
/// A season's announcement in the events channel, sent through the outbox: one message per season, posted once and
/// edited after, its id kept with the sent outbox message (and the channel looked through for it when that is gone).
/// </summary>
public static class EventsOutbox
{
    public const string SeasonUpsert = "events.season-upsert";

    public static string Group(int seasonId) => $"events:season:{seasonId}";

    public static IServiceCollection AddEventsOutboxHandlers(this IServiceCollection services)
        => services.AddOutboxHandler<SeasonAnnouncementHandler>();
}

/// <summary>A season's announcement as it reads: <see cref="SeasonAnnouncementHandler.Message"/> renders it.</summary>
public sealed record SeasonAnnouncement(int SeasonId, string SeasonName, string Description, bool SignupsOpen);

public sealed record SeasonAnnouncementResult(ulong MessageId);

/// <summary>
/// Posts or edits a season's announcement. The message is the one the group's last send saved; failing that (cleaned
/// up, or a retry after a send that wasn't saved), the bot's message in the channel with the season's name, as before
/// the outbox; failing that, a new one. One deleted since is posted again.
/// </summary>
public sealed class SeasonAnnouncementHandler(IDiscordRestClient discord, IOptions<DiscordOptions> options) : OutboxHandler<SeasonAnnouncement>
{
    public override string Kind => EventsOutbox.SeasonUpsert;

    // A later version replaces it anyway.
    public override bool DeadLetterSkipsGroup => false;

    protected override async Task<OutboxOutcome> HandleAsync(OutboxMessage message, SeasonAnnouncement payload, GrifballContext context, CancellationToken ct)
    {
        var channel = options.Value.EventsChannel;
        var messageId = (await context.SentResultAsync<SeasonAnnouncementResult>(EventsOutbox.Group(payload.SeasonId), EventsOutbox.SeasonUpsert, ct))?.MessageId
            ?? (await discord.FindBotPostAsync(channel, x => x.Content == payload.SeasonName, ct, count: 20))?.Id;

        IDiscordRestMessage sent;
        try
        {
            sent = await discord.UpsertMessageAsync(channel, messageId, Message(payload), null, ct);
        }
        catch (Exception ex) when (messageId is not null && DiscordPosts.IsNotFound(ex))
        {
            sent = await discord.UpsertMessageAsync(channel, null, Message(payload), null, ct);
        }
        SetResult(message, new SeasonAnnouncementResult(sent.Id));
        return OutboxOutcome.Done;
    }

    public static MessageProperties Message(SeasonAnnouncement announcement)
    {
        var matchesSummaryEmbed = new EmbedProperties()
        {
            Color = new Color(87, 242, 135), // green
            Title = $"{announcement.SeasonName}",
            Description = announcement.Description,
        };

        var matchEmbeds = new List<EmbedProperties>();
        matchEmbeds.Add(matchesSummaryEmbed);

        var gt = new ButtonProperties(DiscordButtonContants.SetGamertag, "Set Gamertag", ButtonStyle.Primary);

        ActionRowProperties actionRow = new([gt]);

        if (announcement.SignupsOpen)
        {
            var signup = new ButtonProperties(DiscordButtonContants.SignupWithParams(announcement.SeasonId), "Signup", EmojiProperties.Standard("✅"), ButtonStyle.Success);
            actionRow.Add(signup);
        }

        return new MessageProperties()
        {
            Content = announcement.SeasonName,
            Embeds = matchEmbeds,
            Components = [actionRow],
        };
    }
}
