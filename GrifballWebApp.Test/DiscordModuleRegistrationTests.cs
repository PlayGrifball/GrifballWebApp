using GrifballWebApp.Server;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Test;

/// <summary>
/// What Discord sees of the server's NetCord modules: every custom ID with its parameters and every slash command with its options,
/// read the way Program.cs registers them (AddModules on the server assembly). Pins them so moving logic out of the modules
/// (issue #42) can't rename or drop an interaction.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DiscordModuleRegistrationTests
{
    private static List<string> ComponentInteractions<TContext>() where TContext : IComponentInteractionContext
    {
        var service = new ComponentInteractionService<TContext>();
        service.AddModules(typeof(Program).Assembly);
        return service.GetComponentInteractions()
            .Select(x => $"{x.Key}({string.Join(", ", x.Value.Parameters.Select(p => $"{p.Type.Name} {p.Name}{(p.IsOptional ? $" = {p.DefaultValue ?? "null"}" : "")}"))})"
                + $" preconditions: {x.Value.Preconditions.Count}")
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    [Test]
    public void Buttons_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<ButtonInteractionContext>(), Is.EqualTo(new[]
        {
            "join_queue() preconditions: 0",
            "leave_queue() preconditions: 0",
            "set_gamertag() preconditions: 0",
            "signup(Int32 seasonId) preconditions: 0",
            "voteforwinner(Int32 matchId, String winner) preconditions: 0",
        }));
    }

    [Test]
    public void StringMenus_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<StringMenuInteractionContext>(), Is.EqualTo(new[]
        {
            "draftpick(Int32 seasonId, Int32 captainId, Int32 _) preconditions: 0",
            "votetokick(Int32 matchId) preconditions: 0",
        }));
    }

    [Test]
    public void Modals_Should_KeepTheirCustomIdsAndParameters()
    {
        Assert.That(ComponentInteractions<ModalInteractionContext>(), Is.EqualTo(new[]
        {
            "set_gamertag() preconditions: 0",
        }));
    }

    [Test]
    public void SlashCommands_Should_KeepTheirNamesAndOptions()
    {
        var service = new ApplicationCommandService<ApplicationCommandContext, AutocompleteInteractionContext>();
        service.AddModules(typeof(Program).Assembly);

        var commands = service.GetCommands()
            .Select(c => c is SlashCommandInfo<ApplicationCommandContext> slash
                ? $"/{slash.Name} \"{slash.Description}\" permissions: {slash.DefaultGuildPermissions?.ToString() ?? "default"}, preconditions: {slash.Preconditions.Count}"
                    + string.Concat(slash.Parameters.Select(p =>
                        $" [{p.Name}: {p.Type.Name}{(p.IsOptional ? $" = {p.DefaultValue ?? "null"}" : "")} \"{p.Description}\""
                        + $" autocomplete: {p.AutocompleteProviderType?.Name ?? "none"}, preconditions: {p.Preconditions.Count}]"))
                : $"{c.Type} {c.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.That(commands, Is.EqualTo(new[]
        {
            "/matches \"Get recent matches\" permissions: default, preconditions: 0"
                + " [gamertag: String = null \"Gamertag to search matches for\" autocomplete: GamertagAutocomplete, preconditions: 0]",
            "/setgamertag \"Set your gamertag\" permissions: default, preconditions: 0"
                + " [gamertag: String \"No description provided.\" autocomplete: none, preconditions: 0]",
        }));
    }
}
