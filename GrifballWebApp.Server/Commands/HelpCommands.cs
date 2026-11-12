using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Application;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server.Commands;

/// <summary>
/// /help: the bot's commands, read from NetCord's own registry so it lists exactly what is registered. With a command
/// named, its subcommands. Replies are ephemeral. <see cref="HelpCommands"/> only wraps its context and calls this.
/// </summary>
public class HelpCommandsHandler
{
    private readonly ApplicationCommandService<ApplicationCommandContext> _commands;
    private readonly UrlService _urls;

    public HelpCommandsHandler(ApplicationCommandService<ApplicationCommandContext> commands, UrlService urls)
    {
        _commands = commands;
        _urls = urls;
    }

    /// <summary>Each command, or each subcommand of a group (nested groups' too), with its description: "/season standings", "...".</summary>
    public IEnumerable<(string Path, string Description)> Commands()
    {
        foreach (var command in _commands.GetCommands())
        {
            switch (command)
            {
                case SlashCommandInfo<ApplicationCommandContext> slash:
                    yield return ($"/{slash.Name}", slash.Description);
                    break;
                case SlashCommandGroupInfo<ApplicationCommandContext> group:
                    foreach (var sub in SubCommands($"/{group.Name}", group.SubCommands))
                        yield return sub;
                    break;
            }
        }
    }

    private static IEnumerable<(string, string)> SubCommands(string prefix, IReadOnlyDictionary<string, ISubSlashCommandInfo<ApplicationCommandContext>> subCommands)
        => subCommands.Values.OfType<SubSlashCommandGroupInfo<ApplicationCommandContext>>().SelectMany(group => SubCommands($"{prefix} {group.Name}", group.SubCommands))
            .Concat(subCommands.Values.OfType<SubSlashCommandInfo<ApplicationCommandContext>>().Select(command => ($"{prefix} {command.Name}", command.Description)));

    public async Task Help(IDiscordInteractionContext context, string? command)
    {
        await context.Defer(ephemeral: true);
        var all = Commands().OrderBy(x => x.Path, StringComparer.Ordinal).ToList();
        if (command is not null)
        {
            var name = "/" + command.Trim().TrimStart('/').ToLowerInvariant();
            var matching = all.Where(x => x.Path == name || x.Path.StartsWith(name + " ", StringComparison.Ordinal)).ToList();
            await context.Reply(CommandReplies.Embed(name, _urls.Page(""),
                CommandReplies.Lines(matching.Select(x => $"`{x.Path}` · {x.Description}").ToList(), $"There is no {name} command.")));
            return;
        }

        var groups = all
            .GroupBy(x => x.Path.Split(' ')[0])
            .Select(g => g.Count() == 1 && !g.First().Path.Contains(' ')
                ? $"`{g.Key}` · {g.First().Description}"
                : $"`{g.Key}` · {string.Join(", ", g.Select(x => x.Path[(g.Key.Length + 1)..]))}")
            .ToList();
        await context.Reply(CommandReplies.Embed("Commands", _urls.Page(""),
            CommandReplies.Lines(groups, "No commands.") + "\n\n/help with a command shows what each of its parts does. Staff commands reply only to you."));
    }
}

/// <summary>
/// NetCord entry point for /help: wraps the context and hands off to <see cref="HelpCommandsHandler"/>.
/// Keep logic out of here, it is only tested end to end (DiscordTransportTests).
/// </summary>
public class HelpCommands : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly HelpCommandsHandler _handler;

    public HelpCommands(HelpCommandsHandler handler)
    {
        _handler = handler;
    }

    [SlashCommand("help", "The bot's commands")]
    public Task Help([SlashCommandParameter(Description = "A command, like season or admin, for its parts")] string? command = null)
        => _handler.Help(Context.ToDiscordContext(), command);
}
