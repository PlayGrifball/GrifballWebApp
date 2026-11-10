using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Infrastructure.Redis;

public static class AppDistributedCacheExtensions
{
    /// <summary>
    /// The app's <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>: scaled out
    /// (<c>ConnectionStrings:Redis</c>), Redis, so every API pod reads and writes the same entries, over the process's one
    /// Redis connection (<see cref="AppRedis"/>) and with keys under the app's prefix
    /// (<see cref="AppRedisExtensions.ChannelPrefix"/>), so environments can share a server. Unset (one process),
    /// the process's memory.
    /// </summary>
    public static IServiceCollection AddAppDistributedCache(this IServiceCollection services, IConfiguration configuration)
    {
        if (!services.TryAddAppRedis(configuration))
            return services.AddDistributedMemoryCache();

        var prefix = AppRedisExtensions.ChannelPrefix(configuration);
        services.AddStackExchangeRedisCache(options => options.InstanceName = prefix + ":");
        services.AddOptions<RedisCacheOptions>().Configure<AppRedis>((options, redis) => options.ConnectionMultiplexerFactory = () => redis.ConnectAsync());
        return services;
    }
}
