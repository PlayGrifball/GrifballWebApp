using GrifballWebApp.Server.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;

namespace GrifballWebApp.Server.SignalR;

public static class SignalRBackplaneExtensions
{
    /// <summary>The setting that turns the backplane on: a StackExchange.Redis connection string (Redis or Valkey).</summary>
    public const string ConnectionStringName = "Redis";

    public const string ChannelPrefixKey = "SignalR:RedisChannelPrefix";
    public const string DefaultChannelPrefix = "GrifballWebApp";

    /// <summary>The app's prefix in Redis (<c>SignalR:RedisChannelPrefix</c>, <see cref="DefaultChannelPrefix"/> unless set): its channels', and its keys'.</summary>
    public static string ChannelPrefix(IConfiguration configuration)
        => configuration[ChannelPrefixKey] is { Length: > 0 } configured ? configured : DefaultChannelPrefix;

    /// <summary>
    /// With <c>ConnectionStrings:Redis</c> set, SignalR's Redis backplane: a message sent from any process (an API pod,
    /// or grif-discord for a draft pick made in Discord) reaches the browsers connected to every API pod. Unset (the
    /// default, one process), nothing changes. Channels start with <c>SignalR:RedisChannelPrefix</c> (default
    /// <see cref="DefaultChannelPrefix"/>), so environments can share a server. It uses the process's one Redis
    /// connection (<see cref="AppRedis"/>), made in the background and retried, so Redis being down never stops startup.
    /// </summary>
    public static ISignalRServerBuilder AddAppBackplane(this ISignalRServerBuilder signalR, IConfiguration configuration)
    {
        if (!signalR.Services.TryAddAppRedis(configuration))
            return signalR;

        var connectionString = configuration.GetConnectionString(ConnectionStringName)!;
        signalR.AddStackExchangeRedis(options => options.Configuration = AppRedis.Options(connectionString, ChannelPrefix(configuration)));
        signalR.Services.AddOptions<RedisOptions>().Configure<AppRedis>((options, redis) => options.ConnectionFactory = log => redis.ConnectAsync(log));
        return signalR;
    }
}
