using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NetCord.Gateway;
using NetCord.Gateway.Voice;
using NetCord.Gateway.Voice.JsonModels;
using NetCord.Logging;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// Discord's voice connections, through NetCord's voice client on the bot's gateway: each speaker's stream (Opus, decrypted with
/// DAVE by libdave) decoded, replies encoded and played. Kept to Discord's calls alone: it needs a live voice server, which no test
/// has, so it is tried on a deployment (VoiceSessions, all the rest, is tested against a stand-in).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Discord's live voice connection (gateway, UDP, DAVE): no test environment has one")]
public sealed class NetCordVoiceConnector(GatewayClient client, ILogger<NetCordVoiceConnector> log) : IVoiceConnector
{
    public async Task<IVoiceLink> ConnectAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
    {
        var voice = await client.JoinVoiceChannelAsync(guildId, channelId, new VoiceClientConfiguration { Logger = new NetCordLog(log), CacheProvider = KnownMembers(guildId, channelId) }, cancellationToken: cancellationToken);
        var link = new Link(voice, log);
        if (log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
            log.LogInformation("Voice: connected to {Channel} in {Guild}", channelId, guildId);
        try
        {
            await voice.StartAsync(cancellationToken);
            return link;
        }
        catch
        {
            await link.DisposeAsync();
            throw;
        }
    }

    public async Task DisconnectAsync(ulong guildId)
    {
        if (log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
            log.LogInformation("Voice: leaving the channel in {Guild}", guildId);
        await client.UpdateVoiceStateAsync(new(guildId, null));
    }

    // DAVE only accepts an MLS welcome listing users the voice client knows, and NetCord learns of them only from connect events
    // after joining: members already in the channel are seeded from the gateway's voice states
    private ImmutableVoiceClientCacheProvider KnownMembers(ulong guildId, ulong channelId)
    {
        var members = client.Cache.Guilds.TryGetValue(guildId, out var guild)
            ? guild.VoiceStates.Values.Where(s => s.ChannelId == channelId).Select(s => s.UserId).ToArray()
            : [];
        return ImmutableVoiceClientCacheProvider.FromJson(new JsonVoiceClientCache
        {
            Users = members,
            UserSsrcs = new Dictionary<ulong, uint>(),
            SsrcUsers = new Dictionary<uint, ulong>(),
        });
    }

    // NetCord's own voice log (decryption, MLS, UDP failures): warnings and up
    private sealed class NetCordLog(ILogger<NetCordVoiceConnector> log) : IVoiceLogger
    {
        public bool IsEnabled(NetCord.Logging.LogLevel logLevel) => logLevel >= NetCord.Logging.LogLevel.Warning;

        public void Log<TState>(NetCord.Logging.LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var level = logLevel == NetCord.Logging.LogLevel.Warning ? Microsoft.Extensions.Logging.LogLevel.Warning : Microsoft.Extensions.Logging.LogLevel.Error;
            if (IsEnabled(logLevel) && log.IsEnabled(level))
                log.Log(level, exception, "Voice (NetCord): {Message}", formatter(state, exception));
        }
    }

    private sealed class Link : IVoiceLink
    {
        private readonly VoiceClient _voice;
        private readonly ConcurrentDictionary<uint, OpusDecoder> _decoders = new();
        // Each stream's own, reused: one frame decoded at a time a stream
        private readonly ConcurrentDictionary<uint, short[]> _buffers = new();

        public Func<ulong, bool>? Wants { get; set; }

        public event Action<ulong, short[]>? Heard;
        public event Action? Closed;

        public Link(VoiceClient voice, ILogger<NetCordVoiceConnector> log)
        {
            _voice = voice;
            voice.VoiceReceive += args =>
            {
                // A stream not tied to a user yet (its speaking event comes first), or someone not to be heard: not even decoded
                if (!voice.Cache.SsrcUsers.TryGetValue(args.Ssrc, out var speaker) || Wants?.Invoke(speaker) == false)
                    return default;
                try
                {
                    var decoder = _decoders.GetOrAdd(args.Ssrc, _ => new OpusDecoder(VoiceChannels.Mono));
                    var pcm = _buffers.GetOrAdd(args.Ssrc, _ => new short[Opus.GetSamplesPerChannel(Opus.MaxFrameDuration)]);
                    var samples = decoder.Decode(args.Frame, pcm, pcm.Length, false);
                    Heard?.Invoke(speaker, pcm[..samples]);
                }
                catch (Exception e)
                {
                    log.LogWarning(e, "Voice: audio from {Speaker} couldn't be received", speaker);
                }
                return default;
            };
            voice.Disconnect += args =>
            {
                if (log.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
                    log.LogInformation("Voice: disconnected, status {Status} {Description}, reconnecting: {Reconnect}", args.CloseStatus, args.CloseStatusDescription, args.Reconnect);
                if (!args.Reconnect)
                    Closed?.Invoke();
                return default;
            };
        }

        public async Task PlayAsync(short[] pcm, CancellationToken cancellationToken)
        {
            await _voice.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone), cancellationToken: cancellationToken);
            using var stream = _voice.CreateVoiceStream();
            using var opus = new OpusEncodeStream(stream, PcmFormat.Short, VoiceChannels.Stereo, OpusApplication.Voip);
            await opus.WriteAsync(MemoryMarshal.AsBytes(pcm.AsSpan()).ToArray(), cancellationToken);
            await opus.FlushAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _voice.Dispose();
            foreach (var decoder in _decoders.Values)
                decoder.Dispose();
            return default;
        }
    }
}
