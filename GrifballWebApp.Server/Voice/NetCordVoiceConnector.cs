using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NetCord.Gateway;
using NetCord.Gateway.Voice;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// Discord's voice connections, through NetCord's voice client on the bot's gateway: each speaker's stream (Opus, decrypted with
/// DAVE by libdave) decoded, replies encoded and played. Kept to Discord's calls alone: it needs a live voice server, which no test
/// has, so it is tried on a deployment (VoiceSessions, all the rest, is tested against a stand-in).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Discord's live voice connection (gateway, UDP, DAVE): no test environment has one")]
public sealed class NetCordVoiceConnector(GatewayClient client) : IVoiceConnector
{
    public async Task<IVoiceLink> ConnectAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken)
    {
        var voice = await client.JoinVoiceChannelAsync(guildId, channelId, cancellationToken: cancellationToken);
        var link = new Link(voice);
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

    public async Task DisconnectAsync(ulong guildId) => await client.UpdateVoiceStateAsync(new(guildId, null));

    private sealed class Link : IVoiceLink
    {
        private readonly VoiceClient _voice;
        private readonly ConcurrentDictionary<uint, OpusDecoder> _decoders = new();

        public event Action<ulong, short[]>? Heard;
        public event Action? Closed;

        public Link(VoiceClient voice)
        {
            _voice = voice;
            voice.VoiceReceive += args =>
            {
                // A stream not tied to a user yet (its speaking event comes first): dropped
                if (!voice.Cache.SsrcUsers.TryGetValue(args.Ssrc, out var speaker))
                    return default;
                var decoder = _decoders.GetOrAdd(args.Ssrc, _ => new OpusDecoder(VoiceChannels.Mono));
                var pcm = new short[Opus.GetSamplesPerChannel(Opus.MaxFrameDuration)];
                var samples = decoder.Decode(args.Frame, pcm, pcm.Length, false);
                Heard?.Invoke(speaker, pcm[..samples]);
                return default;
            };
            voice.Disconnect += args =>
            {
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
