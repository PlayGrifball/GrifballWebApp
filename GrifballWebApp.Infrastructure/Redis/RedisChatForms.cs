using System.Text.Json;
using GrifballWebApp.Application.Chat;
using StackExchange.Redis;

namespace GrifballWebApp.Infrastructure.Redis;

/// <summary>
/// The chat's waiting forms, scaled out (<see cref="IChatForms"/>): in Redis, which every pod shares, under the app's prefix and "form:",
/// each expiring on its own. A take is one GETDEL, so of two submits at once only one gets the form.
/// </summary>
public sealed class RedisChatForms(AppRedis redis, string prefix) : IChatForms
{
    private readonly string _prefix = prefix + ":form:";

    public async Task SaveAsync(string id, HeldForm held, TimeSpan expiresIn, CancellationToken cancellationToken = default)
        => await (await DatabaseAsync()).StringSetAsync(_prefix + id, JsonSerializer.Serialize(held), expiresIn);

    public async Task<HeldForm?> PeekAsync(string id, CancellationToken cancellationToken = default)
        => Read(await (await DatabaseAsync()).StringGetAsync(_prefix + id));

    public async Task<HeldForm?> TakeAsync(string id, CancellationToken cancellationToken = default)
        => Read(await (await DatabaseAsync()).StringGetDeleteAsync(_prefix + id));

    private async Task<IDatabase> DatabaseAsync() => (await redis.ConnectAsync()).GetDatabase();

    private static HeldForm? Read(RedisValue saved) => saved.IsNull ? null : JsonSerializer.Deserialize<HeldForm>(saved.ToString());
}
