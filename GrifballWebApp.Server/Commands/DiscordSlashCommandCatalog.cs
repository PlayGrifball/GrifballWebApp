using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Server.Commands;

/// <summary>The slash commands the gateway has registered (what /help lists), for the chat's list_slash_commands tool.</summary>
public sealed class DiscordSlashCommandCatalog(HelpCommandsHandler help) : ISlashCommandCatalog
{
    public IReadOnlyList<SlashCommandDto> Commands()
        => help.Commands().OrderBy(c => c.Path, StringComparer.Ordinal).Select(c => new SlashCommandDto(c.Path, c.Description)).ToList();
}
