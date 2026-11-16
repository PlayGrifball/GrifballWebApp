using System.Collections.Concurrent;
using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The bot's voice channels, one a guild (Discord allows a bot one each): each connection (<see cref="IVoiceLink"/>) feeds its
/// channel's <see cref="VoiceConversation"/> and plays its replies. Available only with a speech server (<see cref="IChatSpeech"/>).
/// </summary>
public sealed class VoiceSessions(IVoiceConnector connector, IServiceProvider services, ILogger<VoiceSessions> log) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, Session?> _sessions = new();

    /// <summary>Whether voice can be had: a speech server is configured.</summary>
    public bool Available => services.GetService<IChatSpeech>() is not null;

    /// <summary>
    /// The bot joins <paramref name="channelId"/> in <paramref name="guildId"/> and starts listening; false when it is in a voice
    /// channel there already (or joining one).
    /// </summary>
    public async Task<bool> JoinAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryAdd(guildId, null))
            return false;
        IVoiceLink link;
        try
        {
            link = await connector.ConnectAsync(guildId, channelId, cancellationToken);
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Voice: joining {Channel} in {Guild} failed", channelId, guildId);
            _sessions.TryRemove(guildId, out _);
            await connector.DisconnectAsync(guildId);
            throw;
        }
        var session = new Session(link, Conversation(link));
        _sessions[guildId] = session;
        // Moved out, or the channel went: over, as if left
        link.Closed += () =>
        {
            if (_sessions.TryRemove(new KeyValuePair<ulong, Session?>(guildId, session)))
                _ = session.DisposeAsync().AsTask();
        };
        session.Start();
        return true;
    }

    /// <summary>The bot leaves its voice channel in <paramref name="guildId"/>; false when it is in none there.</summary>
    public async Task<bool> LeaveAsync(ulong guildId)
    {
        if (!_sessions.TryRemove(guildId, out var session) || session is null)
            return false;
        await session.DisposeAsync();
        await connector.DisconnectAsync(guildId);
        return true;
    }

    /// <summary>Whether <paramref name="discordUserId"/> may be heard, read again in every channel: they opted in or out.</summary>
    public Task RefreshAsync(ulong discordUserId)
        => Task.WhenAll(_sessions.Values.OfType<Session>().Select(s => s.Conversation.RefreshAsync(discordUserId)));

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values.OfType<Session>())
            await session.DisposeAsync();
        _sessions.Clear();
    }

    /// <summary>A channel's conversation: it hears the link's speakers and plays its replies there.</summary>
    private VoiceConversation Conversation(IVoiceLink link)
    {
        var conversation = new VoiceConversation(services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<GrifChat>(),
            services.GetRequiredService<IChatSpeech>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<VoiceConversation>>(), link.PlayAsync);
        link.Heard += conversation.Hear;
        return conversation;
    }

    /// <summary>One channel's link and conversation, the conversation running until it is disposed.</summary>
    private sealed class Session(IVoiceLink link, VoiceConversation conversation) : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private Task _running = Task.CompletedTask;

        public VoiceConversation Conversation => conversation;

        public void Start() => _running = conversation.RunAsync(_stop.Token);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _running;
            await link.DisposeAsync();
            _stop.Dispose();
        }
    }
}
