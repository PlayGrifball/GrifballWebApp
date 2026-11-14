using GrifballWebApp.Server.Commands;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace GrifballWebApp.Server;

/// <summary>The choices for a gamertag option (an Xbox account): <see cref="CommandLookups.XboxAccountChoices"/>.</summary>
public class GamertagAutocomplete : IAutocompleteProvider<AutocompleteInteractionContext>
{
    private readonly CommandLookups _lookups;
    public GamertagAutocomplete(CommandLookups lookups)
    {
        _lookups = lookups;
    }

    public async ValueTask<IEnumerable<ApplicationCommandOptionChoiceProperties>?> GetChoicesAsync(ApplicationCommandInteractionDataOption option,
        AutocompleteInteractionContext context)
        => await _lookups.XboxAccountChoices(option.Value);
}
