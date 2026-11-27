using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// The commands the gateway has registered, for the chat's list_slash_commands tool: the slash commands (what /help lists), then the
/// right-click user commands (Apps menu on a Discord user), which /help has no entry for.
/// </summary>
public sealed class DiscordSlashCommandCatalog(HelpCommandsHandler help) : ISlashCommandCatalog
{
    private static readonly SlashCommandDto[] UserCommandEntries =
    [
        new(UserCommands.ProfileName, "Right-click a Discord user, Apps: shows their profile."),
        new(UserCommands.PullName, "Right-click a Discord user, Apps: pulls their latest Halo games."),
        new(UserCommands.SetGamertagName, "Right-click a Discord user, Apps: a sysadmin sets their gamertag, making their account if they have none."),
    ];

    public IReadOnlyList<SlashCommandDto> Commands()
        => help.Commands().OrderBy(c => c.Path, StringComparer.Ordinal).Select(c => new SlashCommandDto(c.Path, c.Description)).Concat(UserCommandEntries).ToList();
}
