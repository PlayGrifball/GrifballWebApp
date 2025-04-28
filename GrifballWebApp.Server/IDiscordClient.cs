using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server;

public interface IDiscordClient
{
    Task<CurrentUser> GetCurrentUserAsync(RestRequestProperties? properties = null, CancellationToken ct = default);
    IAsyncEnumerable<RestMessage> GetMessagesAsync(ulong channelId, PaginationProperties<ulong>? paginationProperties = null, RestRequestProperties? properties = null);
    Task DeleteMessagesAsync(ulong channelId, IEnumerable<ulong> messageIds, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<RestMessage> SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken ct = default);
    Task<RestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default);
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

    public async Task<RestMessage> ModifyMessageAsync(ulong channelId, ulong messageId, Action<MessageOptions> action, RestRequestProperties? properties = null, CancellationToken ct = default)
    {
        return await _client.ModifyMessageAsync(channelId, messageId, action, properties, ct);
    }
}