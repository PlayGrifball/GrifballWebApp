using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using GrifballWebApp.Infrastructure.Redis;

namespace GrifballWebApp.Server.SignalR;

public static class SignalRBackplaneExtensions
{
    /// <summary>
    /// With <c>ConnectionStrings:Redis</c> set, SignalR's Redis backplane: a message sent from any process (an API pod,
    /// or grif-discord for a draft pick made in Discord) reaches the browsers connected to every API pod. Unset (the
    /// default, one process), nothing changes. Channels start with <c>SignalR:RedisChannelPrefix</c> (default
    /// <see cref="AppRedisExtensions.DefaultChannelPrefix"/>), so environments can share a server. It uses the process's one Redis
    /// connection (<see cref="AppRedis"/>), made in the background and retried, so Redis being down never stops startup.
    /// </summary>
    public static ISignalRServerBuilder AddAppBackplane(this ISignalRServerBuilder signalR, IConfiguration configuration)
    {
        if (!signalR.Services.TryAddAppRedis(configuration))
            return signalR;

        var connectionString = configuration.GetConnectionString(AppRedisExtensions.ConnectionStringName)!;
        signalR.AddStackExchangeRedis(options => options.Configuration = AppRedis.Options(connectionString, AppRedisExtensions.ChannelPrefix(configuration)));
        signalR.Services.AddOptions<RedisOptions>().Configure<AppRedis>((options, redis) => options.ConnectionFactory = log => redis.ConnectAsync(log));
        return signalR;
    }
}
