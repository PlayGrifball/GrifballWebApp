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
        // First, as every command does: joining takes longer than an interaction's three seconds. For everyone: the reply
        // says the bot listens, to whom, and how to stop it
        await DeferAsync(ephemeral: false);
        var guild = Context.Guild;
        await ReplyAsync(await handler.JoinAsync(guild?.Id, guild?.VoiceStates, Context.User.Id));
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

    /// <summary>The reply deferred: the caller's alone unless <paramref name="ephemeral"/> is false.</summary>
    private Task<InteractionCallbackResponse?> DeferAsync(bool ephemeral = true)
        => Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(ephemeral ? MessageFlags.Ephemeral : null));

    private Task<RestMessage> ReplyAsync(string content) => Context.Interaction.ModifyResponseAsync(m => m.Content = content);
}
