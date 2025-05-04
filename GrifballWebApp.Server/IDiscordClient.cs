using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server;

public interface IDiscordClient
{
    Task<CurrentUser> GetCurrentUserAsync(RestRequestProperties? properties = null, CancellationToken ct = default);
    IAsyncEnumerable<RestMessage> GetMessagesAsync(ulong channelId, PaginationProperties<ulong>? paginationProperties = null, RestRequestProperties? properties = null);
    Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<RestMessage> SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<GuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, GuildThreadFromMessageProperties threadFromMessageProperties, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<RestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<Channel> DeleteChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<IReadOnlyList<GuildThread>> GetActiveGuildThreadsAsync(ulong guildId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
    Task<Channel> GetChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken));
}

public class DiscordClient : IDiscordClient
{
    private readonly RestClient _client;
    public DiscordClient(RestClient restClient)
    {
        _client = restClient;
    }

    public async Task<CurrentUser> GetCurrentUserAsync(RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return await _client.GetCurrentUserAsync(properties, ct);
    }

    public IAsyncEnumerable<RestMessage> GetMessagesAsync(ulong channelId, PaginationProperties<ulong>? paginationProperties = null, RestRequestProperties? properties = null)
    {
        return _client.GetMessagesAsync(channelId, paginationProperties, properties);
    }

    public async Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        await _client.DeleteMessagesAsync(channelId, messageIds, properties, ct);
    }

    public async Task<RestMessage> SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return await _client.SendMessageAsync(channelId, message, properties, ct);
    }

    public async Task<GuildThread> CreateGuildThreadAsync(ulong channelId, ulong messageId, GuildThreadFromMessageProperties threadFromMessageProperties, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.CreateGuildThreadAsync(channelId, messageId, threadFromMessageProperties, properties, cancellationToken);
    }

    public async Task<RestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return await _client.ModifyMessageAsync(channelId, messageId, action, properties, ct);
    }

    public async Task<Channel> DeleteChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.DeleteChannelAsync(channelId, properties, cancellationToken);

    }

    public async Task<IReadOnlyList<GuildThread>> GetActiveGuildThreadsAsync(ulong guildId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.GetActiveGuildThreadsAsync(guildId, properties, cancellationToken);
    }

    public async Task<Channel> GetChannelAsync(ulong channelId, RestRequestProperties? properties = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        return await _client.GetChannelAsync(channelId, properties, cancellationToken);
    }
}