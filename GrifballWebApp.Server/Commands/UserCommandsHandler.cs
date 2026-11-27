using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Profile;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The Set gamertag user command (right-click a Discord user): a sysadmin names the gamertag in a modal, and it is set the way
/// /setgamertag sets one's own (<see cref="IDiscordGamertagSetter"/>): their account, or a new one made for them.
/// </summary>
public class UserCommandsHandler
{
    public const string NotSysadmin = "Only a sysadmin can set someone else's gamertag.";
    public const string NoGamertag = "Give a gamertag.";
    public const string ModalPrefix = "set_gamertag_for";

    private readonly DiscordCaller _caller;
    private readonly IDiscordGamertagSetter _setter;

    public UserCommandsHandler(DiscordCaller caller, IDiscordGamertagSetter setter)
    {
        _caller = caller;
        _setter = setter;
    }

    private async Task<bool> IsSysadmin(ulong discordUserId)
        => SiteRoles.IsInAnyRole((await _caller.AsToolCaller(discordUserId)).Roles, DiscordCaller.Roles.SysadminOnly);

    /// <summary>Opens the gamertag modal for a sysadmin; anyone else is told they may not. A modal must be the first response, so the role is checked before it.</summary>
    public async Task OpenSetGamertag(IDiscordInteractionContext context, ulong targetDiscordUserId, string targetUsername)
    {
        if (!await IsSysadmin(context.Interaction.User.Id))
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties { Content = NotSysadmin, Flags = MessageFlags.Ephemeral }));
            return;
        }

        var modal = new ModalProperties($"{ModalPrefix}:{targetDiscordUserId}:{targetUsername}", "Set gamertag")
        {
            new LabelProperties("Gamertag", new TextInputProperties("gamertag", TextInputStyle.Short)
            {
                Required = true,
                MaxLength = 20,
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The modal's submit: the sysadmin check again (the submit is a new interaction), then the same flow as /setgamertag.</summary>
    public async Task SetGamertag(IDiscordInteractionContext context, ulong targetDiscordUserId, string targetUsername, string gamertag)
    {
        await context.Defer(ephemeral: true);
        if (!await IsSysadmin(context.Interaction.User.Id))
        {
            await context.Reply(NotSysadmin);
            return;
        }

        gamertag = gamertag.Trim();
        if (gamertag.Length == 0)
        {
            await context.Reply(NoGamertag);
            return;
        }

        var error = await _setter.SetGamertagForDiscordUser(targetDiscordUserId, targetUsername, gamertag);
        await context.Reply(error is null ? $"Set: <@{targetDiscordUserId}> has the gamertag {gamertag}." : error);
    }
}
