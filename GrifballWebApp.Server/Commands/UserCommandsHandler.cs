using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Application.Profile;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The Set gamertag user command (right-click a Discord user): a sysadmin names the gamertag in a modal, and it is set through
/// the same service as a player's /setgamertag and the sysadmin tool set_user_gamertag, so the same rules refuse it.
/// </summary>
public class UserCommandsHandler
{
    public const string NotSysadmin = "Only a sysadmin can set someone else's gamertag.";
    public const string NoAccount = "No site account is linked to that Discord user. They make one by using /setgamertag themselves or signing in to the site with Discord.";
    public const string ModalPrefix = "set_gamertag_for";

    private readonly DiscordCaller _caller;
    private readonly GrifballContext _context;
    private readonly ISetGamertagService _setGamertag;

    public UserCommandsHandler(DiscordCaller caller, GrifballContext context, ISetGamertagService setGamertag)
    {
        _caller = caller;
        _context = context;
        _setGamertag = setGamertag;
    }

    /// <summary>Opens the gamertag modal for a sysadmin; anyone else is told they may not. A modal must be the first response, so the role is checked before it.</summary>
    public async Task OpenSetGamertag(IDiscordInteractionContext context, ulong targetDiscordUserId)
    {
        var caller = await _caller.Find(context.Interaction.User.Id);
        if (caller is null || !caller.IsInAnyRole(DiscordCaller.Roles.SysadminOnly))
        {
            await context.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties { Content = NotSysadmin, Flags = MessageFlags.Ephemeral }));
            return;
        }

        var modal = new ModalProperties($"{ModalPrefix}:{targetDiscordUserId}", "Set gamertag")
        {
            new LabelProperties("Gamertag", new TextInputProperties("gamertag", TextInputStyle.Short)
            {
                Required = true,
                MaxLength = 20,
            }),
        };
        await context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }

    /// <summary>The modal's submit: the sysadmin check again (the submit is a new interaction), then the player's rules.</summary>
    public async Task SetGamertag(IDiscordInteractionContext context, ulong targetDiscordUserId, string gamertag)
    {
        await context.Defer(ephemeral: true);
        if (await _caller.Require(context, DiscordCaller.Roles.SysadminOnly) is null)
            return;

        gamertag = gamertag.Trim();
        var discordId = (long)targetDiscordUserId;
        var userID = await _context.Users.Where(u => u.DiscordUserID == discordId && !u.IsDummyUser).Select(u => (int?)u.Id).FirstOrDefaultAsync();
        if (userID is null)
        {
            await context.Reply(NoAccount);
            return;
        }

        var error = await _setGamertag.SetGamertag(userID.Value, gamertag);
        await context.Reply(error is null ? $"Set: <@{targetDiscordUserId}> has the gamertag {gamertag}." : error + " A sysadmin can change or take over a gamertag on the site's Edit User page.");
    }
}
