using GrifballWebApp.Application.Voice;

namespace GrifballWebApp.Server.Voice;

/// <summary>
/// /voice's work, from Discord's IDs alone (the module, VoiceCommands, reads them from the interaction): the bot in a voice
/// channel (<see cref="VoiceSessions"/>), and who it may hear (<see cref="VoiceConsents"/>).
/// </summary>
public sealed class VoiceCommandsHandler(VoiceSessions sessions, VoiceConsents consents)
{
    public const string Unavailable = "Voice isn't set up here.";
    public const string NoGuild = "Use this in a server.";
    public const string NotInVoice = "Join a voice channel first, then run /voice join.";
    public const string AlreadyIn = "I'm in a voice channel in this server already: /voice leave first.";
    public const string Joined = "Joined.";
    public const string NotIn = "I'm not in a voice channel here.";
    public const string Left = "Left the voice channel.";
    public const string OptedIn = "I'll hear you in voice channels now. Say \"Grif, ...\" to ask me something. Nothing you say is kept. /voice optout to stop.";
    public const string OptedOut = "I won't hear you in voice channels any more.";

    /// <summary>What everyone sees when the bot joins: that it listens, to whom, and how to ask it or stop it.</summary>
    public static string Listening(ulong channelId)
        => $"I'm listening in <#{channelId}>. I only hear people who ran /voice optin, and nothing said is kept. Say \"Grif, ...\" to ask me something; /voice leave to stop.";

    /// <summary>What /voice join answers its caller, and what it tells everyone (when it joined).</summary>
    public sealed record JoinReply(string Reply, string? Notice = null);

    /// <summary>The bot joins <paramref name="voiceChannelId"/> (the caller's) in <paramref name="guildId"/>.</summary>
    public async Task<JoinReply> JoinAsync(ulong? guildId, ulong? voiceChannelId, CancellationToken cancellationToken = default)
    {
        if (!sessions.Available)
            return new(Unavailable);
        if (guildId is not { } guild)
            return new(NoGuild);
        if (voiceChannelId is not { } channel)
            return new(NotInVoice);
        if (!await sessions.JoinAsync(guild, channel, cancellationToken))
            return new(AlreadyIn);
        return new(Joined, Listening(channel));
    }

    /// <summary>The bot leaves its voice channel in <paramref name="guildId"/>.</summary>
    public async Task<string> LeaveAsync(ulong? guildId)
    {
        if (guildId is not { } guild)
            return NoGuild;
        return await sessions.LeaveAsync(guild) ? Left : NotIn;
    }

    /// <summary><paramref name="discordUserId"/> may be heard, from now on, in every channel the bot is in.</summary>
    public async Task<string> OptInAsync(ulong discordUserId, CancellationToken cancellationToken = default)
    {
        await consents.OptInAsync(discordUserId, cancellationToken);
        await sessions.RefreshAsync(discordUserId);
        return OptedIn;
    }

    /// <summary><paramref name="discordUserId"/> is no longer heard: what they were saying is dropped too.</summary>
    public async Task<string> OptOutAsync(ulong discordUserId, CancellationToken cancellationToken = default)
    {
        await consents.OptOutAsync(discordUserId, cancellationToken);
        await sessions.RefreshAsync(discordUserId);
        return OptedOut;
    }
}
