using System.Collections.Concurrent;
using GrifballWebApp.Application.Chat;

namespace GrifballWebApp.Server.Voice;

/// <summary>How a join went (<see cref="VoiceSessions.JoinAsync"/>).</summary>
public enum VoiceJoin
{
    Joined = 1,
    /// <summary>In a voice channel in the guild already, or joining one.</summary>
    AlreadyIn,
    /// <summary>Asked to leave while it was still joining: it left as soon as it had joined.</summary>
    LeftFirst,
}

/// <summary>
/// The bot's voice channels, one a guild (Discord allows a bot one each): each connection (<see cref="IVoiceLink"/>) feeds its
/// channel's <see cref="VoiceConversation"/> and plays its replies. Available only with a speech server (<see cref="IChatSpeech"/>).
/// </summary>
public sealed class VoiceSessions(IVoiceConnector connector, IServiceProvider services, ILogger<VoiceSessions> log, VoiceOptions? options = null) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, Entry> _guilds = new();

    /// <summary>Whether voice can be had: a speech server is configured.</summary>
    public bool Available => services.GetRequiredService<IServiceProviderIsService>().IsService(typeof(IChatSpeech));

    /// <summary>
    /// The bot joins <paramref name="channelId"/> in <paramref name="guildId"/> and starts listening. One connection a guild: while
    /// one is open or opening, another is refused (<see cref="VoiceJoin.AlreadyIn"/>).
    /// </summary>
    public async Task<VoiceJoin> JoinAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken = default)
    {
        var entry = new Entry();
        if (!_guilds.TryAdd(guildId, entry))
            return VoiceJoin.AlreadyIn;
        IVoiceLink link;
        try
        {
            using var joining = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, entry.Leaving.Token);
            link = await connector.ConnectAsync(guildId, channelId, joining.Token);
        }
        catch (OperationCanceledException) when (entry.Leaving.IsCancellationRequested)
        {
            await ForgetAsync(guildId, entry);
            return VoiceJoin.LeftFirst;
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Voice: joining {Channel} in {Guild} failed", channelId, guildId);
            await ForgetAsync(guildId, entry);
            throw;
        }
        // Asked to leave while it connected: leaves now
        if (entry.Leaving.IsCancellationRequested)
        {
            await link.DisposeAsync();
            await ForgetAsync(guildId, entry);
            return VoiceJoin.LeftFirst;
        }
        var session = new Session(link, Conversation(link));
        entry.Session = session;
        // Moved out, or the channel went: over, as if left (nothing to do when it was left already)
        link.Closed += () => _ = CloseAsync(guildId, entry);
        session.Start();
        return VoiceJoin.Joined;
    }

    /// <summary>
    /// The bot leaves its voice channel in <paramref name="guildId"/> (one it is still joining, as soon as it has); false when it is
    /// in none there.
    /// </summary>
    public async Task<bool> LeaveAsync(ulong guildId)
    {
        if (!_guilds.TryGetValue(guildId, out var entry))
            return false;
        if (entry.Session is null)
        {
            // Still joining: the join leaves once it has
            await entry.Leaving.CancelAsync();
            return true;
        }
        await CloseAsync(guildId, entry);
        return true;
    }

    /// <summary>Whether <paramref name="discordUserId"/> may be heard, read again in every channel: they opted in or out.</summary>
    public Task RefreshAsync(ulong discordUserId)
        => Task.WhenAll(_guilds.Values.Select(e => e.Session).OfType<Session>().Select(s => s.Conversation.RefreshAsync(discordUserId)));

    /// <summary>Every channel left (joins still connecting, as soon as they have).</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var (guildId, entry) in _guilds.ToList())
        {
            if (entry.Session is null)
                await entry.Leaving.CancelAsync();
            else
                await CloseAsync(guildId, entry);
        }
    }

    /// <summary><paramref name="guildId"/>'s session over and the bot out of the channel, once whoever asks first.</summary>
    private async Task CloseAsync(ulong guildId, Entry entry)
    {
        if (!_guilds.TryRemove(new KeyValuePair<ulong, Entry>(guildId, entry)))
            return;
        await entry.Session!.DisposeAsync();
        entry.Dispose();
        await connector.DisconnectAsync(guildId);
    }

    /// <summary>A join that didn't stay: the guild free again, and the bot out of any channel Discord put it in.</summary>
    private async Task ForgetAsync(ulong guildId, Entry entry)
    {
        _guilds.TryRemove(new KeyValuePair<ulong, Entry>(guildId, entry));
        entry.Dispose();
        await connector.DisconnectAsync(guildId);
    }

    /// <summary>A channel's conversation: it hears the link's speakers (only those it wants decoded) and plays its replies there.</summary>
    private VoiceConversation Conversation(IVoiceLink link)
    {
        var conversation = new VoiceConversation(services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<GrifChat>(),
            services.GetRequiredService<IChatSpeech>(), services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<VoiceConversation>>(), link.PlayAsync, endOfLine: options?.EndOfLine);
        link.Wants = conversation.Wants;
        link.Heard += conversation.Hear;
        return conversation;
    }

    /// <summary>A guild's voice: its session once joined, and the way to stop a join still connecting.</summary>
    private sealed class Entry : IDisposable
    {
        public Session? Session { get; set; }

        public CancellationTokenSource Leaving { get; } = new();

        public void Dispose() => Leaving.Dispose();
    }

    /// <summary>One channel's link and conversation, the conversation running until it is disposed (by <see cref="CloseAsync"/>, once).</summary>
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

/// <summary>The voice chat's settings: how long a speaker is quiet before their line is over (Speech:EndOfLineMilliseconds).</summary>
public sealed record VoiceOptions(TimeSpan EndOfLine);
