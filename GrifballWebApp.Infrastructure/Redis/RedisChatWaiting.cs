using System.Text.Json;
using GrifballWebApp.Application.Chat;
using StackExchange.Redis;

namespace GrifballWebApp.Infrastructure.Redis;

/// <summary>
/// The waiting changes, scaled out (<see cref="IChatWaiting"/>): in Redis, which every API pod shares, under the app's prefix
/// (<see cref="AppRedisExtensions.ChannelPrefix"/>), each expiring on its own. A take is one GETDEL, so of two answers at once only
/// one gets the change.
/// </summary>
public sealed class RedisChatWaiting(AppRedis redis, string prefix) : IChatWaiting
{
    private readonly string _prefix = prefix + ":";

    public async Task SaveAsync(string key, ChatAction action, TimeSpan expiresIn, CancellationToken cancellationToken = default)
        => await (await DatabaseAsync()).StringSetAsync(_prefix + key, JsonSerializer.Serialize(action), expiresIn);

    public async Task<ChatAction?> PeekAsync(string key, CancellationToken cancellationToken = default)
        => Read(await (await DatabaseAsync()).StringGetAsync(_prefix + key));

    public async Task<ChatAction?> TakeAsync(string key, CancellationToken cancellationToken = default)
        => Read(await (await DatabaseAsync()).StringGetDeleteAsync(_prefix + key));

    public async Task ForgetAsync(string key, CancellationToken cancellationToken = default)
        => await (await DatabaseAsync()).KeyDeleteAsync(_prefix + key);

    private async Task<IDatabase> DatabaseAsync() => (await redis.ConnectAsync()).GetDatabase();

    private static ChatAction? Read(RedisValue saved) => saved.IsNull ? null : JsonSerializer.Deserialize<ChatAction>(saved.ToString());
}
