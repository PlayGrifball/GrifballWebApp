using System.ComponentModel;

namespace GrifballWebApp.Application.Tools;

/// <summary>One slash command of the Discord bot: "/season standings" and what it does.</summary>
public sealed record SlashCommandDto(string Command, string Description);

/// <summary>The commands the Discord bot has registered: the Discord host reads them from its own registry, any other host has none.</summary>
public interface ISlashCommandCatalog
{
    IReadOnlyList<SlashCommandDto> Commands();
}

/// <summary>The catalog of a host that runs no Discord bot.</summary>
public sealed class NoSlashCommands : ISlashCommandCatalog
{
    public IReadOnlyList<SlashCommandDto> Commands() => [];
}

/// <summary>The Discord bot's slash commands, so a chat can tell people what to type instead of guessing.</summary>
public class SlashCommandTools
{
    private readonly ISlashCommandCatalog _catalog;

    public SlashCommandTools(ISlashCommandCatalog catalog)
    {
        _catalog = catalog;
    }

    [Description("The Discord bot's slash commands with what each does, as /help lists them (for example /setgamertag sets the player's own gamertag, "
        + "/match pull and /player pull pull games from Halo). Use it when someone asks what the bot can do or how to do something in Discord; "
        + "never name a slash command that isn't in this list.")]
    public IReadOnlyList<SlashCommandDto> ListSlashCommands(
        [Description("Only this command and its parts, like season or admin, optional.")] string? command = null)
    {
        var all = _catalog.Commands();
        if (all.Count == 0)
            throw new ToolException("The Discord bot isn't running here, so there is no slash command list.");
        if (string.IsNullOrWhiteSpace(command))
            return all;
        var name = "/" + command.Trim().TrimStart('/').ToLowerInvariant();
        var matching = all.Where(c => c.Command == name || c.Command.StartsWith(name + " ", StringComparison.Ordinal)).ToList();
        return matching.Count > 0 ? matching : throw new ToolException($"There is no {name} command: call this without a command for the whole list.");
    }
}
