using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server;

public interface IDiscordClient
{
    Task<IRestMessage> GetMessageAsync(ulong channelId, ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default);
    Task<CurrentUser> GetCurrentUserAsync(RestRequestProperties? properties = null, CancellationToken ct = default);
    IAsyncEnumerable<IRestMessage> GetMessagesAsync(ulong channelId, PaginationProperties<ulong>? paginationProperties = null, RestRequestProperties? properties = null);
    Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<IRestMessage> SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<IGuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, GuildThreadFromMessageProperties threadFromMessageProperties, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<IRestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<Channel> DeleteChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IGuildThread>> GetActiveGuildThreadsAsync(ulong guildId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default);
    Task<Channel> GetChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default);
    Task<IRestMessage> UpsertMessageAsync(ulong channelId, ulong? messageId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default);
}

public class DiscordClient : IDiscordClient
{
    private readonly RestClient _client;
    public DiscordClient(RestClient restClient)
    {
        _client = restClient;
    }

    public async Task<IRestMessage> GetMessageAsync(ulong channelId, ulong messageId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default)
    {
        return new RestMessage(await _client.GetMessageAsync(channelId, messageId, properties, cancellationToken));
    }

    public async Task<CurrentUser> GetCurrentUserAsync(RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return await _client.GetCurrentUserAsync(properties, ct);
    }

    public IAsyncEnumerable<IRestMessage> GetMessagesAsync(ulong channelId, PaginationProperties<ulong>? paginationProperties = null, RestRequestProperties? properties = null)
    {
        return _client.GetMessagesAsync(channelId, paginationProperties, properties).Select(x => new RestMessage(x));
    }

    public async Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        await _client.DeleteMessagesAsync(channelId, messageIds, properties, ct);
    }

    public async Task<IRestMessage> SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return new RestMessage(await _client.SendMessageAsync(channelId, message, properties, ct));
    }

    public async Task<IGuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, GuildThreadFromMessageProperties threadFromMessageProperties, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return new GuildThread(await _client.CreateGuildThreadAsync(channelId, messageId, threadFromMessageProperties, properties, cancellationToken));
    }

    public async Task<IRestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return new RestMessage(await _client.ModifyMessageAsync(channelId, messageId, action, properties, ct));
    }

    public async Task<Channel> DeleteChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.DeleteChannelAsync(channelId, properties, cancellationToken);
    }

    public async Task<IReadOnlyList<IGuildThread>> GetActiveGuildThreadsAsync(ulong guildId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return (await _client.GetActiveGuildThreadsAsync(guildId, properties, cancellationToken)).Select(x => new GuildThread(x)).ToList().AsReadOnly();
    }

    public async Task<Channel> GetChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.GetChannelAsync(channelId, properties, cancellationToken);
    }

    /// <summary>
    /// Sends or modifies a message in the specified channel. If messageId is null sends, otherwise modifies the message with the specified ID.
    /// </summary>
    /// <returns></returns>
    public async Task<IRestMessage> UpsertMessageAsync(ulong channelId, ulong? messageId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        if (messageId is null)
        {
            return await SendMessageAsync(channelId, message, properties, ct);
        }
        else
        {
            return await ModifyMessageAsync(channelId, messageId.Value,
                (x) => x.WithContent(message.Content)
                .WithAttachments(message.Attachments)
                .WithEmbeds(message.Embeds)
                .WithAllowedMentions(message.AllowedMentions)
                .WithComponents(message.Components)
                .WithFlags(message.Flags),
                properties, ct);
        }
    }
}

public interface IRestMessage
{
    DateTimeOffset CreatedAt { get; }
    IAuthor Author { get; }
    string Content { get; }
    IReadOnlyList<IEmbed> Embeds { get; }
    ulong Id { get; }
}

public class RestMessage : IRestMessage
{
    public RestMessage()
    {
    }
    public RestMessage(NetCord.Rest.RestMessage message)
    {
        CreatedAt = message.CreatedAt;
        Author = new Author(message.Author)
        {
            Id = message.Author.Id // This is necessary to ensure the Id is set correctly, as the Author class has a required Id property.
        };
        Content = message.Content;
        Embeds = message.Embeds.Select(embed => new Embed(embed)).ToList().AsReadOnly();
        Id = message.Id;
    }

    public DateTimeOffset CreatedAt { get; set; }
    public IAuthor Author { get; set; }
    public string Content { get; set; }
    public IReadOnlyList<IEmbed> Embeds { get; set; }
    public ulong Id { get; set; }
}

public interface IAuthor
{
    ulong Id { get; set; }
    bool IsBot { get; set; }
}

public class Author : IAuthor
{
    public Author()
    {
    }
    public Author(NetCord.User author)
    {
        Id = author.Id;
        IsBot = author.IsBot;
    }
    public ulong Id { get; set; }
    public bool IsBot { get; set; }
}

public interface IEmbed
{
    string? Title { get; }
}


public class Embed : IEmbed
{
    public Embed(NetCord.Embed embed)
    {
        Title = embed.Title;
    }
    public string? Title { get; set; }
}

public interface IGuildThread
{
    ulong Id { get; set; }
    Task AddUserAsync(ulong userId, RestRequestProperties? restRequestProperties = null, CancellationToken cancellationToken = default);
}

public class GuildThread : IGuildThread
{
    
    public ulong Id { get; set; }
    private NetCord.GuildThread _thread;

    public GuildThread()
    {

    }

    public GuildThread(NetCord.GuildThread thread)
    {
        Id = thread.Id;
        _thread = thread;
    }

    public async Task AddUserAsync(ulong userId, RestRequestProperties? restRequestProperties = null, CancellationToken cancellationToken = default)
    {
        if (_thread is not null)
            await _thread.AddUserAsync(userId, restRequestProperties, cancellationToken);
    }
}