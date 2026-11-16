namespace GrifballWebApp.Server.Voice;

/// <summary>
/// The bot's connection to one voice channel, as <see cref="VoiceSessions"/> needs it: each speaker's audio, decoded, as it
/// arrives; replies played; and word when the connection is gone for good. Discord's own (<see cref="NetCordVoiceConnector"/>)
/// needs a live voice server, so <see cref="VoiceSessions"/> is tested against a stand-in.
/// </summary>
public interface IVoiceLink : IAsyncDisposable
{
    /// <summary>Whether a speaker's audio is wanted at all (their Discord ID): when not, it isn't even decoded. Unset, everyone's is.</summary>
    Func<ulong, bool>? Wants { get; set; }

    /// <summary>A speaker's audio (their Discord ID, 48 kHz mono), as it arrives: only the speakers <see cref="Wants"/> wants.</summary>
    event Action<ulong, short[]>? Heard;

    /// <summary>The connection is gone and won't come back (moved out, the channel deleted).</summary>
    event Action? Closed;

    /// <summary><paramref name="pcm"/> (48 kHz stereo, interleaved) played in the channel.</summary>
    Task PlayAsync(short[] pcm, CancellationToken cancellationToken);
}

/// <summary>Opens and closes the bot's voice connections: one a guild, as Discord allows.</summary>
public interface IVoiceConnector
{
    /// <summary>The bot joins <paramref name="channelId"/> in <paramref name="guildId"/>.</summary>
    Task<IVoiceLink> ConnectAsync(ulong guildId, ulong channelId, CancellationToken cancellationToken);

    /// <summary>The bot is out of any voice channel in <paramref name="guildId"/>.</summary>
    Task DisconnectAsync(ulong guildId);
}
