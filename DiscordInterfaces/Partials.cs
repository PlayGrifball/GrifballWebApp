using NetCord.Rest;

namespace DiscordInterface.Generated;
public partial interface IDiscordRestClient
{
    Task<IDiscordRestMessage> UpsertMessageAsync(ulong channelId, ulong? messageId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken cancellationToken = default);
}

public partial class DiscordRestClient
{
    /// <summary>
    /// Sends or modifies a message in the specified channel. If messageId is null sends, otherwise modifies the message with the specified ID.
    /// </summary>
    /// <returns></returns>
    public async Task<IDiscordRestMessage> UpsertMessageAsync(ulong channelId, ulong? messageId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken cancellationToken = default)
    {
        if (messageId is null)
        {
            return await SendMessageAsync(channelId, message, properties, cancellationToken);
        }
        else
        {
            return await ModifyMessageAsync(channelId, messageId.Value,
                (x) => x.WithContent(message.Content!) // This generated wrong. It can accept null
                .WithAttachments(message.Attachments)
                .WithEmbeds(message.Embeds)
                .WithAllowedMentions(message.AllowedMentions)
                .WithComponents(message.Components)
                .WithFlags(message.Flags),
                properties, cancellationToken);
        }
    }
}
