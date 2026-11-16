using GrifballWebApp.Application.Voice;
using GrifballWebApp.Server.Voice;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /voice: the bot in a voice channel (<see cref="VoiceSessions"/>), and who it may hear (<see cref="VoiceConsents"/>): only those
/// who opted in. Joining and leaving need the gateway's own voice connection, so they are tested end to end only where they refuse.
/// </summary>
[SlashCommand("voice", "Talk to the bot in a voice channel")]
public class VoiceCommands(VoiceSessions sessions, VoiceConsents consents) : ApplicationCommandModule<ApplicationCommandContext>
{
    public const string Unavailable = "Voice isn't set up here.";
    public const string NoGuild = "Use this in a server.";
    public const string NotInVoice = "Join a voice channel first, then run /voice join.";
    public const string AlreadyIn = "I'm in a voice channel in this server already: /voice leave first.";
    public const string NotIn = "I'm not in a voice channel here.";
    public const string Left = "Left the voice channel.";
    public const string OptedIn = "I'll hear you in voice channels now. Say \"Grif, ...\" to ask me something. Nothing you say is kept. /voice optout to stop.";
    public const string OptedOut = "I won't hear you in voice channels any more.";

    /// <summary>What everyone sees when the bot joins: that it listens, to whom, and how to ask it or stop it.</summary>
    public static string Listening(ulong channelId)
        => $"I'm listening in <#{channelId}>. I only hear people who ran /voice optin, and nothing said is kept. Say \"Grif, ...\" to ask me something; /voice leave to stop.";

    [SubSlashCommand("join", "The bot joins your voice channel and answers \"Grif, ...\"")]
    public async Task Join()
    {
        if (!sessions.Available)
        {
            await Ephemeral(Unavailable);
            return;
        }
        if (Context.Guild is not { } guild)
        {
            await Ephemeral(NoGuild);
            return;
        }
        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out var state) || state.ChannelId is not { } channelId)
        {
            await Ephemeral(NotInVoice);
            return;
        }
        // Joining takes longer than an interaction's three seconds
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());
        var joined = await sessions.JoinAsync(Context.Client, guild.Id, channelId);
        await Context.Interaction.ModifyResponseAsync(m => m.Content = joined ? Listening(channelId) : AlreadyIn);
    }

    [SubSlashCommand("leave", "The bot leaves its voice channel")]
    public async Task Leave()
    {
        if (Context.Guild is not { } guild)
        {
            await Ephemeral(NoGuild);
            return;
        }
        await Ephemeral(await sessions.LeaveAsync(Context.Client, guild.Id) ? Left : NotIn);
    }

    [SubSlashCommand("optin", "Let the bot hear you in voice channels")]
    public async Task OptIn()
    {
        await consents.OptInAsync(Context.User.Id, Context.User.Username);
        await sessions.RefreshAsync(Context.User.Id);
        await Ephemeral(OptedIn);
    }

    [SubSlashCommand("optout", "Stop the bot hearing you in voice channels")]
    public async Task OptOut()
    {
        await consents.OptOutAsync(Context.User.Id);
        await sessions.RefreshAsync(Context.User.Id);
        await Ephemeral(OptedOut);
    }

    private Task Ephemeral(string content)
        => Context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties { Content = content, Flags = MessageFlags.Ephemeral }));
}
