using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using GrifballWebApp.Application.Chat;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The bot's voice channels, one a server (Discord allows a bot one per guild): NetCord's voice client, whose audio (Opus, each
/// speaker's own stream, decrypted with DAVE by libdave) is decoded and fed to the channel's <see cref="VoiceConversation"/>,
/// whose replies are encoded and played. Available only with a speech server (<see cref="IChatSpeech"/>).
/// </summary>
public sealed class VoiceSessions(IServiceProvider services, ILogger<VoiceSessions> log) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<ulong, Session?> _sessions = new();

    /// <summary>Whether voice can be had: a speech server is configured.</summary>
    public bool Available => services.GetService<IChatSpeech>() is not null;

    /// <summary>Whether the bot is in a voice channel in <paramref name="guildId"/>.</summary>
    public bool InGuild(ulong guildId) => _sessions.ContainsKey(guildId);

    /// <summary>
    /// The bot joins <paramref name="channelId"/> in <paramref name="guildId"/> and starts listening; false when it is in one there
    /// already.
    /// </summary>
    public async Task<bool> JoinAsync(GatewayClient client, ulong guildId, ulong channelId, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryAdd(guildId, null))
            return false;
        try
        {
            var voice = await client.JoinVoiceChannelAsync(guildId, channelId, cancellationToken: cancellationToken);
            var session = new Session(voice, services);
            _sessions[guildId] = session;
            voice.Disconnect += args =>
            {
                // Not reconnecting: moved out or the channel went
                if (!args.Reconnect && _sessions.TryRemove(guildId, out var gone) && gone is not null)
                    _ = gone.DisposeAsync().AsTask();
                return default;
            };
            await voice.StartAsync(cancellationToken);
            session.Start();
            return true;
        }
        catch (Exception e)
        {
            log.LogWarning(e, "Voice: joining {Channel} in {Guild} failed", channelId, guildId);
            if (_sessions.TryRemove(guildId, out var failed) && failed is not null)
                await failed.DisposeAsync();
            await client.UpdateVoiceStateAsync(new(guildId, null), cancellationToken: CancellationToken.None);
            throw;
        }
    }

    /// <summary>The bot leaves its voice channel in <paramref name="guildId"/>; false when it is in none there.</summary>
    public async Task<bool> LeaveAsync(GatewayClient client, ulong guildId)
    {
        if (!_sessions.TryRemove(guildId, out var session))
            return false;
        if (session is not null)
            await session.DisposeAsync();
        await client.UpdateVoiceStateAsync(new(guildId, null));
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

    /// <summary>One channel's voice client and conversation, the Opus decoder of each speaker's stream, and the replies' playing.</summary>
    private sealed class Session : IAsyncDisposable
    {
        private readonly VoiceClient _voice;
        private readonly ConcurrentDictionary<uint, OpusDecoder> _decoders = new();
        private readonly CancellationTokenSource _stop = new();
        private Task _running = Task.CompletedTask;

        public VoiceConversation Conversation { get; }

        public Session(VoiceClient voice, IServiceProvider services)
        {
            _voice = voice;
            Conversation = new VoiceConversation(services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<GrifChat>(),
                services.GetRequiredService<IChatSpeech>(), services.GetRequiredService<TimeProvider>(),
                services.GetRequiredService<ILogger<VoiceConversation>>(), PlayAsync);
            voice.VoiceReceive += args =>
            {
                // A stream not tied to a user yet (the speaking event comes first): dropped
                if (!voice.Cache.SsrcUsers.TryGetValue(args.Ssrc, out var speaker))
                    return default;
                var decoder = _decoders.GetOrAdd(args.Ssrc, _ => new OpusDecoder(VoiceChannels.Mono));
                var pcm = new short[Opus.GetSamplesPerChannel(Opus.MaxFrameDuration)];
                var samples = decoder.Decode(args.Frame, pcm, pcm.Length, false);
                Conversation.Hear(speaker, pcm[..samples]);
                return default;
            };
        }

        public void Start() => _running = Conversation.RunAsync(_stop.Token);

        /// <summary>A reply (48 kHz stereo) played in the channel.</summary>
        private async Task PlayAsync(short[] pcm, CancellationToken cancellationToken)
        {
            await _voice.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone), cancellationToken: cancellationToken);
            using var stream = _voice.CreateVoiceStream();
            using var opus = new OpusEncodeStream(stream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Voip);
            await opus.WriteAsync(MemoryMarshal.AsBytes(pcm.AsSpan()).ToArray(), cancellationToken);
            await opus.FlushAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            await _running;
            _voice.Dispose();
            foreach (var decoder in _decoders.Values)
                decoder.Dispose();
            _stop.Dispose();
        }
    }
}
