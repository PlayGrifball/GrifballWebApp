using GrifballWebApp.Server.Voice;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// NetCord entry point for /voice: defers, reads the IDs from the interaction and hands off to <see cref="VoiceCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
[SlashCommand("voice", "Talk to the bot in a voice channel")]
public class VoiceCommands(VoiceCommandsHandler handler) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SubSlashCommand("join", "The bot joins your voice channel and answers when called by name")]
    public async Task Join()
    {
        // First, as every command does: joining takes longer than an interaction's three seconds
        await DeferAsync();
        var guild = Context.Guild;
        ulong? channel = guild is not null && guild.VoiceStates.TryGetValue(Context.User.Id, out var state) ? state.ChannelId : null;
        var reply = await handler.JoinAsync(guild?.Id, channel);
        await ReplyAsync(reply.Reply);
        // Everyone's notice: that it listens, to whom, and how to stop it
        if (reply.Notice is { } notice)
            await Context.Interaction.SendFollowupMessageAsync(notice);
    }

    [SubSlashCommand("leave", "The bot leaves its voice channel")]
    public async Task Leave()
    {
        await DeferAsync();
        await ReplyAsync(await handler.LeaveAsync(Context.Guild?.Id));
    }

    [SubSlashCommand("optin", "Let the bot hear you in voice channels")]
    public async Task OptIn()
    {
        await DeferAsync();
        await ReplyAsync(await handler.OptInAsync(Context.User.Id));
    }

    [SubSlashCommand("optout", "Stop the bot hearing you in voice channels")]
    public async Task OptOut()
    {
        await DeferAsync();
        await ReplyAsync(await handler.OptOutAsync(Context.User.Id));
    }

    /// <summary>The reply deferred, the caller's alone.</summary>
    private Task<InteractionCallbackResponse?> DeferAsync()
        => Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

    private Task<RestMessage> ReplyAsync(string content) => Context.Interaction.ModifyResponseAsync(m => m.Content = content);
}
