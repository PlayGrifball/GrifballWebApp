using GrifballWebApp.Server.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace GrifballWebApp.Server.Hosting;

/// <summary>
/// The process's one Redis (or Valkey) connection, from <c>ConnectionStrings:Redis</c>: shared by SignalR's backplane
/// and the Halo calls' shared pace (<see cref="HaloInfinite.RedisHaloCallSlots"/>). Made on the first use, in the
/// background and retried by StackExchange.Redis itself (<c>abortConnect=false</c> unless the connection string says
/// otherwise), so Redis being down never stops startup. A connect that failed (<c>abortConnect=true</c> and Redis not
/// there, or a connection string that can't be read) isn't kept: the next use tries again.
/// </summary>
public sealed class AppRedis : IAsyncDisposable, IDisposable
{
    private readonly Func<TextWriter?, Task<IConnectionMultiplexer>> _connect;
    private readonly Lock _lock = new();
    private Task<IConnectionMultiplexer>? _connection;

    public AppRedis(string connectionString, string channelPrefix)
        : this(async log => await ConnectionMultiplexer.ConnectAsync(Options(connectionString, channelPrefix), log))
    {
    }

    /// <summary>With its own way to connect (tests).</summary>
    public AppRedis(Func<TextWriter?, Task<IConnectionMultiplexer>> connect) => _connect = connect;

    /// <summary>
    /// The connection string's options: <c>abortConnect=false</c> unless it says otherwise, and the channels (SignalR's)
    /// under <paramref name="channelPrefix"/>.
    /// </summary>
    public static ConfigurationOptions Options(string connectionString, string channelPrefix)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        if (!connectionString.Contains("abortConnect", StringComparison.OrdinalIgnoreCase))
            options.AbortOnConnectFail = false;
        options.ChannelPrefix = RedisChannel.Literal(channelPrefix);
        return options;
    }

    /// <summary>The connection, made on the first call; made again when the last attempt failed.</summary>
    public Task<IConnectionMultiplexer> ConnectAsync(TextWriter? log = null)
    {
        lock (_lock)
        {
            if (_connection is null || _connection.IsFaulted || _connection.IsCanceled)
                _connection = Connect(log);
            return _connection;
        }
    }

    // Async, so a connection string that can't be read fails the task, not the caller's call
    private async Task<IConnectionMultiplexer> Connect(TextWriter? log) => await _connect(log);

    public async ValueTask DisposeAsync()
    {
        Task<IConnectionMultiplexer>? connection;
        lock (_lock)
            connection = _connection;
        if (connection is { IsCompletedSuccessfully: true })
            await connection.Result.DisposeAsync();
    }

    // A container disposed synchronously (a test's using) closes it too
    public void Dispose()
    {
        Task<IConnectionMultiplexer>? connection;
        lock (_lock)
            connection = _connection;
        if (connection is { IsCompletedSuccessfully: true })
            connection.Result.Dispose();
    }
}

public static class AppRedisExtensions
{
    /// <summary>
    /// <see cref="AppRedis"/>, once, when <c>ConnectionStrings:Redis</c> is set; false (nothing registered) when it isn't.
    /// </summary>
    public static bool TryAddAppRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(SignalRBackplaneExtensions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;
        var prefix = SignalRBackplaneExtensions.ChannelPrefix(configuration);
        services.TryAddSingleton(_ => new AppRedis(connectionString, prefix));
        return true;
    }
}
