using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace GrifballWebApp.Server.SignalR;

public static class SignalRBackplaneExtensions
{
    /// <summary>The setting that turns the backplane on: a StackExchange.Redis connection string (Redis or Valkey).</summary>
    public const string ConnectionStringName = "Redis";

    public const string ChannelPrefixKey = "SignalR:RedisChannelPrefix";
    public const string DefaultChannelPrefix = "GrifballWebApp";

    /// <summary>
    /// With <c>ConnectionStrings:Redis</c> set, SignalR's Redis backplane: a message sent from any process (an API pod,
    /// or the worker for a draft pick made in Discord) reaches the browsers connected to every API pod. Unset (the
    /// default, one process), nothing changes. Channels start with <c>SignalR:RedisChannelPrefix</c> (default
    /// <see cref="DefaultChannelPrefix"/>), so environments can share a server. The connection is made in the background
    /// and retried (<c>abortConnect=false</c> unless the connection string says otherwise), so Redis being down never
    /// stops startup.
    /// </summary>
    public static ISignalRServerBuilder AddAppBackplane(this ISignalRServerBuilder signalR, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            return signalR;

        var prefix = configuration[ChannelPrefixKey] is { Length: > 0 } configured ? configured : DefaultChannelPrefix;
        var abortConnectSet = connectionString.Contains("abortConnect", StringComparison.OrdinalIgnoreCase);
        return signalR.AddStackExchangeRedis(connectionString, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal(prefix);
            if (!abortConnectSet)
                options.Configuration.AbortOnConnectFail = false;
        });
    }
}
