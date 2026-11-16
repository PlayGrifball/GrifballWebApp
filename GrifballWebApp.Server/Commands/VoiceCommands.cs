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
    public const string Joined = "Joined.";
    public const string OptedIn = "I'll hear you in voice channels now. Say \"Grif, ...\" to ask me something. Nothing you say is kept. /voice optout to stop.";
    public const string OptedOut = "I won't hear you in voice channels any more.";

    /// <summary>What everyone sees when the bot joins: that it listens, to whom, and how to ask it or stop it.</summary>
    public static string Listening(ulong channelId)
        => $"I'm listening in <#{channelId}>. I only hear people who ran /voice optin, and nothing said is kept. Say \"Grif, ...\" to ask me something; /voice leave to stop.";

    [SubSlashCommand("join", "The bot joins your voice channel and answers when called by name")]
    public async Task Join()
    {
        // First, as every command does: joining takes longer than an interaction's three seconds
        await DeferAsync();
        if (!sessions.Available)
        {
            await ReplyAsync(Unavailable);
            return;
        }
        if (Context.Guild is not { } guild)
        {
            await ReplyAsync(NoGuild);
            return;
        }
        if (!guild.VoiceStates.TryGetValue(Context.User.Id, out var state) || state.ChannelId is not { } channelId)
        {
            await ReplyAsync(NotInVoice);
            return;
        }
        if (!await sessions.JoinAsync(Context.Client, guild.Id, channelId))
        {
            await ReplyAsync(AlreadyIn);
            return;
        }
        await ReplyAsync(Joined);
        // Everyone's notice: that it listens, to whom, and how to stop it
        await Context.Interaction.SendFollowupMessageAsync(Listening(channelId));
    }

    [SubSlashCommand("leave", "The bot leaves its voice channel")]
    public async Task Leave()
    {
        await DeferAsync();
        if (Context.Guild is not { } guild)
        {
            await ReplyAsync(NoGuild);
            return;
        }
        await ReplyAsync(await sessions.LeaveAsync(Context.Client, guild.Id) ? Left : NotIn);
    }

    [SubSlashCommand("optin", "Let the bot hear you in voice channels")]
    public async Task OptIn()
    {
        await DeferAsync();
        await consents.OptInAsync(Context.User.Id);
        await sessions.RefreshAsync(Context.User.Id);
        await ReplyAsync(OptedIn);
    }

    [SubSlashCommand("optout", "Stop the bot hearing you in voice channels")]
    public async Task OptOut()
    {
        await DeferAsync();
        await consents.OptOutAsync(Context.User.Id);
        await sessions.RefreshAsync(Context.User.Id);
        await ReplyAsync(OptedOut);
    }

    /// <summary>The reply deferred, the caller's alone.</summary>
    private Task<InteractionCallbackResponse?> DeferAsync()
        => Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

    private Task<RestMessage> ReplyAsync(string content) => Context.Interaction.ModifyResponseAsync(m => m.Content = content);
}
